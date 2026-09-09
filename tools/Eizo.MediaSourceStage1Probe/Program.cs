using System.Text.Json;
using Eizo.Models;

var localData = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Eizo");

Directory.CreateDirectory(localData);

foreach (var fileName in new[] { "sources.json", "catalog.json" })
{
    var path = Path.Combine(localData, fileName);
    if (File.Exists(path))
        File.Delete(path);
}

var probeRoot = Path.Combine(
    Path.GetTempPath(),
    "Eizo-Stage1Probe-" + Guid.NewGuid().ToString("N"));

Directory.CreateDirectory(probeRoot);
Directory.CreateDirectory(Path.Combine(probeRoot, "Season 1"));

var videoA = Path.Combine(probeRoot, "episode-01.mp4");
var videoB = Path.Combine(probeRoot, "Season 1", "episode-02.mkv");
var audio = Path.Combine(probeRoot, "theme.flac");

File.WriteAllBytes(videoA, new byte[4096]);
File.WriteAllBytes(videoB, new byte[8192]);
File.WriteAllBytes(audio, new byte[1024]);

try
{
    var sourceStore = MediaSourceStore.Default;
    var catalog = MediaCatalogStore.Default;

    var builtIn = sourceStore.Find(
        MediaSourceDefinition.OpenedLocalFilesSourceId);

    Assert(builtIn is { IsBuiltIn: true }, "Built-in local source missing.");

    var source = sourceStore.AddLocalFolder(
        probeRoot,
        "Stage 1 Probe");

    Assert(source.Kind == MediaSourceKind.Local, "Local source kind mismatch.");
    Assert(
        string.Equals(
            Path.GetFullPath(source.RootLocation!),
            Path.GetFullPath(probeRoot),
            StringComparison.OrdinalIgnoreCase),
        "Local source root mismatch.");

    var discovered = catalog.ScanLocalSource(source);
    Assert(discovered == 2, $"Expected 2 videos, got {discovered}.");

    var items = catalog.SnapshotForSource(source.Id);
    Assert(items.Count == 2, "Source snapshot does not contain two videos.");
    Assert(
        items.All(item => item.Location is
        {
            Kind: MediaLocationKind.LocalFile
        }),
        "Stage 1 catalog contains a non-local location.");

    Assert(
        items.Any(item =>
            string.Equals(item.LocalPath, videoA, StringComparison.OrdinalIgnoreCase)),
        "episode-01.mp4 was not cataloged.");

    Assert(
        items.Any(item =>
            string.Equals(item.LocalPath, videoB, StringComparison.OrdinalIgnoreCase)),
        "episode-02.mkv was not cataloged.");

    Assert(
        items.All(item =>
            !string.Equals(
                Path.GetExtension(item.LocalPath),
                ".flac",
                StringComparison.OrdinalIgnoreCase)),
        "Audio file leaked into the video catalog.");

    var sourcesPath = Path.Combine(localData, "sources.json");
    var catalogPath = Path.Combine(localData, "catalog.json");

    Assert(File.Exists(sourcesPath), "sources.json was not persisted.");
    Assert(File.Exists(catalogPath), "catalog.json was not persisted.");

    using (var document = JsonDocument.Parse(File.ReadAllText(sourcesPath)))
    {
        Assert(
            document.RootElement.GetProperty("SchemaVersion").GetInt32() == 1,
            "sources.json schemaVersion mismatch.");
    }

    using (var document = JsonDocument.Parse(File.ReadAllText(catalogPath)))
    {
        Assert(
            document.RootElement.GetProperty("SchemaVersion").GetInt32() == 1,
            "catalog.json schemaVersion mismatch.");
    }

    Assert(
        !File.ReadAllText(sourcesPath).Contains(
            "Password",
            StringComparison.OrdinalIgnoreCase),
        "sources.json contains a password field.");

    File.Delete(videoA);

    var afterDelete = catalog.SnapshotForSource(source.Id);
    Assert(
        afterDelete.Count == 1 &&
        string.Equals(
            afterDelete[0].LocalPath,
            videoB,
            StringComparison.OrdinalIgnoreCase),
        "Missing local files are not hidden from live catalog snapshots.");

    Assert(
        MediaSourceProviderRegistry.TryGet(
            MediaSourceKind.Local,
            out var provider),
        "Local provider was not registered.");

    var connection = await provider.TestConnectionAsync(source);
    Assert(connection.IsAvailable, "Local provider connection check failed.");

    var listed = new List<MediaSourceEntry>();
    await foreach (var entry in provider.ListAsync(source))
        listed.Add(entry);

    Assert(
        listed.Any(entry =>
            string.Equals(entry.Name, "Season 1", StringComparison.Ordinal)),
        "Local provider did not enumerate the source folder.");

    Console.WriteLine(
        "WebDAV Stage 1 runtime probe PASS. " +
        $"Source={source.Id}; CatalogItems={items.Count}; " +
        $"RemainingAfterDelete={afterDelete.Count}");
}
finally
{
    try
    {
        Directory.Delete(probeRoot, recursive: true);
    }
    catch
    {
    }
}

return;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
