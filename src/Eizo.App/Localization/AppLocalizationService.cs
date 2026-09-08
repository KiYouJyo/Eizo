using Microsoft.Windows.ApplicationModel.Resources;

namespace Eizo.Localization;

internal sealed class AppLocalizationService
{
    private readonly ResourceLoader _loader = new();
    public static AppLocalizationService Default { get; } = new();

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        try
        {
            var value = _loader.GetString(key);
            return string.IsNullOrWhiteSpace(value) ? "!" + key + "!" : value;
        }
        catch { return "!" + key + "!"; }
    }
}
