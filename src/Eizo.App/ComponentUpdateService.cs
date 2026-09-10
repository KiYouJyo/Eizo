using System.IO.Compression;

namespace Eizo;

internal enum ComponentUpdateState
{
    NotChecked,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    Verifying,
    ReadyForRestart,
    Failed
}

internal sealed record ComponentUpdateProgress(ComponentUpdateState State, double? Fraction = null);

internal sealed record ComponentUpdateResult(
    ComponentDefinition Definition,
    ComponentUpdateState State,
    Version CurrentVersion,
    Version? AvailableVersion = null,
    string? ErrorCode = null,
    string? ErrorDetail = null);

internal sealed class ComponentUpdateService
{
    private readonly ComponentDefinition _definition;
    private GitHubReleaseInfo? _pendingRelease;
    private GitHubReleaseAsset? _pendingArchiveAsset;
    private GitHubReleaseAsset? _pendingManifestAsset;
    private ComponentReleaseManifest? _pendingManifest;
    private Version? _pendingVersion;

    public ComponentUpdateService(ComponentDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public ComponentUpdateResult CreateInitialResult()
    {
        var runtime = ComponentRuntimeBootstrapper.GetStatus(_definition);
        return new(_definition, ComponentUpdateState.NotChecked, runtime.CurrentVersion, runtime.PendingVersion);
    }

    public async Task<ComponentUpdateResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var current = CurrentVersion;
        ResetPendingUpdate();
        try
        {
            var release = await GitHubUpdateService.GetLatestReleaseAsync(_definition.Repository, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (release is null) return Fail(current, null, "NoRelease");
            if (!GitHubUpdateService.TryParseVersionTag(release.TagName, out var parsedVersion))
                return Fail(current, null, "InvalidVersion", release.TagName);

            var available = ComponentRuntimeBootstrapper.NormalizeVersion(parsedVersion);
            if (available <= current) return new(_definition, ComponentUpdateState.UpToDate, current, available);

            var archiveAsset = FindArchiveAsset(release, available);
            if (archiveAsset is null) return Fail(current, available, "MissingAsset");
            var manifestAsset = release.Assets.FirstOrDefault(
                asset => string.Equals(asset.Name, _definition.ManifestAssetName, StringComparison.OrdinalIgnoreCase));
            if (manifestAsset is null) return Fail(current, available, "MissingManifestAsset");

            var manifestResult = await DownloadAndValidateManifestAsync(manifestAsset, available, cancellationToken).ConfigureAwait(false);
            if (manifestResult.Manifest is null)
                return Fail(current, available, manifestResult.ErrorCode ?? "ManifestValidation", manifestResult.ErrorDetail);

            var manifest = manifestResult.Manifest;
            if (!ComponentRuntimeBootstrapper.IsHostContractCompatible(_definition, manifest.HostContract))
                return Fail(
                    current,
                    available,
                    "IncompatibleHostContract",
                    $"Host={_definition.HostContractName} {_definition.HostContractVersion}; package={manifest.HostContract.Name} {manifest.HostContract.MinVersion}..<{manifest.HostContract.MaxVersionExclusive}.");

            _pendingRelease = release;
            _pendingVersion = available;
            _pendingArchiveAsset = archiveAsset;
            _pendingManifestAsset = manifestAsset;
            _pendingManifest = manifest;

            if (ComponentRuntimeBootstrapper.IsPendingVersion(_definition, available))
                return new(_definition, ComponentUpdateState.ReadyForRestart, current, available);
            return new(_definition, ComponentUpdateState.UpdateAvailable, current, available);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(current, null, "Timeout", exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return Fail(current, null, "Network", exception.Message);
        }
        catch (GitHubAssetDownloadException exception)
        {
            return Fail(current, null, exception.Code, exception.Message);
        }
    }

    public async Task<ComponentUpdateResult> DownloadAndStageAsync(
        IProgress<ComponentUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var current = CurrentVersion;
        if (_pendingRelease is null || _pendingArchiveAsset is null || _pendingManifestAsset is null || _pendingManifest is null || _pendingVersion is null)
            return Fail(current, null, "NoPendingUpdate");

        var available = _pendingVersion;
        if (available <= current) return new(_definition, ComponentUpdateState.UpToDate, current, available);

        var componentRoot = ComponentRuntimeBootstrapper.GetComponentRoot(_definition);
        var downloadsRoot = Path.Combine(componentRoot, "downloads");
        var archivePath = Path.Combine(downloadsRoot, $"{_definition.ArchivePrefix}-v{ComponentRuntimeBootstrapper.FormatVersion(available)}-x64.zip");
        var finalDirectory = ComponentRuntimeBootstrapper.GetVersionDirectory(_definition, available);
        var temporaryDirectory = $"{finalDirectory}.staging-{Environment.ProcessId}-{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(downloadsRoot);
            progress?.Report(new(ComponentUpdateState.Downloading, 0d));
            var downloadProgress = new Progress<double>(fraction =>
                progress?.Report(new(ComponentUpdateState.Downloading, fraction)));
            await GitHubUpdateService.DownloadAssetAsync(_pendingArchiveAsset, archivePath, downloadProgress, cancellationToken).ConfigureAwait(false);

            progress?.Report(new(ComponentUpdateState.Verifying));
            Directory.CreateDirectory(temporaryDirectory);
            ExtractSafely(archivePath, temporaryDirectory, cancellationToken);

            if (!ComponentPackageValidator.TryValidate(_definition, temporaryDirectory, out var package, out var validationError) || package is null)
                return Fail(current, available, "PackageValidation", validationError);
            if (package.Version != available)
                return Fail(current, available, "VersionMismatch", $"Package={package.Version}; Release={available}");
            if (package.AbiVersion != _pendingManifest.AbiVersion || package.HostContract != _pendingManifest.HostContract)
                return Fail(current, available, "ManifestMismatch", "The manifest inside the component archive does not match the preflight release manifest.");

            if (Directory.Exists(finalDirectory)) Directory.Delete(finalDirectory, recursive: true);
            Directory.Move(temporaryDirectory, finalDirectory);
            ComponentRuntimeBootstrapper.StageForNextLaunch(_definition, available);
            ComponentUpdateDiagnostics.Write(_definition, "ready", null, $"v{ComponentRuntimeBootstrapper.FormatVersion(available)} staged for next launch.");
            progress?.Report(new(ComponentUpdateState.ReadyForRestart, 1d));
            return new(_definition, ComponentUpdateState.ReadyForRestart, current, available);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GitHubAssetDownloadException exception)
        {
            return Fail(current, available, exception.Code, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Fail(current, available, "StorageAccess", exception.Message);
        }
        catch (IOException exception)
        {
            return Fail(current, available, "StorageIo", exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return Fail(current, available, "PackageInvalid", exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return Fail(current, available, "DownloadNetwork", exception.Message);
        }
        finally
        {
            TryDeleteDirectory(temporaryDirectory);
            TryDeleteFile(archivePath);
        }
    }

    private Version CurrentVersion => ComponentRuntimeBootstrapper.GetStatus(_definition).CurrentVersion;

    private async Task<(ComponentReleaseManifest? Manifest, string? ErrorCode, string? ErrorDetail)> DownloadAndValidateManifestAsync(
        GitHubReleaseAsset asset,
        Version expectedVersion,
        CancellationToken cancellationToken)
    {
        var downloadsRoot = Path.Combine(ComponentRuntimeBootstrapper.GetComponentRoot(_definition), "downloads");
        Directory.CreateDirectory(downloadsRoot);
        var path = Path.Combine(downloadsRoot, $"component-release-{Environment.ProcessId}-{Guid.NewGuid():N}.json");
        try
        {
            await GitHubUpdateService.DownloadAssetAsync(asset, path, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!ComponentReleaseManifestReader.TryReadFile(_definition, path, out var manifest, out var error) || manifest is null)
                return (null, "ManifestValidation", error);
            if (manifest.Version != expectedVersion)
                return (null, "ManifestVersionMismatch", $"Manifest={manifest.Version}; Release={expectedVersion}");
            return (manifest, null, null);
        }
        finally
        {
            TryDeleteFile(path);
        }
    }

    private GitHubReleaseAsset? FindArchiveAsset(GitHubReleaseInfo release, Version version)
    {
        var expected = $"{_definition.ArchivePrefix}-v{ComponentRuntimeBootstrapper.FormatVersion(version)}-x64.zip";
        return release.Assets.FirstOrDefault(asset => string.Equals(asset.Name, expected, StringComparison.OrdinalIgnoreCase));
    }

    private void ResetPendingUpdate()
    {
        _pendingRelease = null;
        _pendingArchiveAsset = null;
        _pendingManifestAsset = null;
        _pendingManifest = null;
        _pendingVersion = null;
    }

    private ComponentUpdateResult Fail(Version current, Version? available, string code, string? detail = null)
    {
        ComponentUpdateDiagnostics.Write(_definition, "failed", code, detail);
        return new(_definition, ComponentUpdateState.Failed, current, available, code, detail);
    }

    private static void ExtractSafely(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        var rootWithSeparator = destinationRoot.EndsWith(Path.DirectorySeparatorChar)
            ? destinationRoot
            : destinationRoot + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var targetPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
            if (!targetPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetPath, destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The component archive contains a path outside the staging directory.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? destinationRoot);
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception) when (Directory.Exists(path))
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception) when (File.Exists(path))
        {
        }
    }
}

internal static class ComponentUpdateDiagnostics
{
    private static readonly object Gate = new();

    public static void Write(ComponentDefinition definition, string stage, string? code, string? detail)
    {
        try
        {
            lock (Gate)
            {
                var root = ComponentRuntimeBootstrapper.GetComponentRoot(definition);
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, "update.log");
                var line = $"{DateTimeOffset.UtcNow:O}\t{stage}\t{code ?? "-"}\t{Sanitize(detail)}{Environment.NewLine}";
                File.AppendAllText(path, line);
            }
        }
        catch (Exception) when (true)
        {
        }
    }

    private static string Sanitize(string? value) => string.IsNullOrWhiteSpace(value)
        ? "-"
        : value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
}
