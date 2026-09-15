namespace Eizo.Models;

internal sealed class FirstRunGuideState
{
    public int StateSchemaVersion { get; set; } = 1;
    public int CompletedGuideVersion { get; set; }
    public int LastStep { get; set; }
}
