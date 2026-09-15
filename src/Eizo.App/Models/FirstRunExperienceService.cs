using System.Text.Json;

namespace Eizo.Models;

internal sealed class FirstRunExperienceService
{
    public const int CurrentVersion = 1;
    private const int CurrentSchemaVersion = 1;

    private static readonly string DefaultStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "first-run-guide.json");

    private readonly object _gate = new();
    private readonly string _statePath;
    private FirstRunGuideState? _state;

    public static FirstRunExperienceService Default { get; } = new();

    internal FirstRunExperienceService(string? statePath = null) =>
        _statePath = string.IsNullOrWhiteSpace(statePath) ? DefaultStatePath : statePath;

    public void PrepareForLaunch()
    {
        lock (_gate) _ = LoadCore();
    }

    public bool ShouldShowAutomatically()
    {
        lock (_gate) return LoadCore().CompletedGuideVersion < CurrentVersion;
    }

    public int GetResumeStep()
    {
        lock (_gate)
            return Math.Clamp(LoadCore().LastStep, 0, 5);
    }

    public void RecordStep(int step)
    {
        lock (_gate)
        {
            var state = LoadCore();
            var normalized = Math.Clamp(step, 0, 5);
            if (state.LastStep == normalized || state.CompletedGuideVersion >= CurrentVersion)
                return;

            state.LastStep = normalized;
            _ = TrySave(state, out _);
        }
    }

    public bool TryMarkCompleted(out string? error)
    {
        lock (_gate)
        {
            var state = LoadCore();
            state.CompletedGuideVersion = CurrentVersion;
            state.LastStep = 5;
            return TrySave(state, out error);
        }
    }

    private FirstRunGuideState LoadCore()
    {
        if (_state is not null) return _state;
        try
        {
            if (!File.Exists(_statePath))
                return _state = NewPendingState();

            var loaded = JsonSerializer.Deserialize<FirstRunGuideState>(
                File.ReadAllText(_statePath));
            if (loaded is null ||
                loaded.StateSchemaVersion <= 0 ||
                loaded.CompletedGuideVersion < 0 ||
                loaded.LastStep < 0 ||
                loaded.LastStep > 5 ||
                loaded.StateSchemaVersion > CurrentSchemaVersion)
            {
                return _state = NewPendingState();
            }

            loaded.StateSchemaVersion = CurrentSchemaVersion;
            loaded.CompletedGuideVersion = Math.Min(
                loaded.CompletedGuideVersion,
                CurrentVersion);
            return _state = loaded;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return _state = NewPendingState();
        }
    }

    private bool TrySave(FirstRunGuideState state, out string? error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            var temp = $"{_statePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(
                temp,
                JsonSerializer.Serialize(
                    state,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, _statePath, overwrite: true);
            _state = state;
            error = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static FirstRunGuideState NewPendingState() => new()
    {
        StateSchemaVersion = CurrentSchemaVersion,
        CompletedGuideVersion = 0,
        LastStep = 0
    };
}
