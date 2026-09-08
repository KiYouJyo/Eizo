using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;

namespace Eizo.Localization;

internal sealed record AppLanguageChangedEventArgs(string PreviousLanguage, string CurrentLanguage);

internal sealed class AppLocalizationService
{
    private readonly object _gate = new();
    private ResourceLoader _loader = new();
    private string _currentLanguage = "zh-CN";
    private int _switchInProgress;

    public static AppLocalizationService Default { get; } = new();

    public string CurrentLanguage => _currentLanguage;
    public event EventHandler<AppLanguageChangedEventArgs>? LanguageChanged;

    public void ApplyPersistedLanguage(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ApplyLanguageCore(ResolveEffectiveLanguage(settings.Language));
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        try
        {
            ResourceLoader loader;
            lock (_gate) loader = _loader;
            var value = loader.GetString(key);
            return string.IsNullOrWhiteSpace(value) ? "!" + key + "!" : value;
        }
        catch
        {
            return "!" + key + "!";
        }
    }

    public async Task<bool> SwitchLanguageAsync(
        AppLanguagePreference preference,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _switchInProgress, 1) != 0) return false;

        var previousLanguage = _currentLanguage;
        var previousOverride = ApplicationLanguages.PrimaryLanguageOverride;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var effective = ResolveEffectiveLanguage(preference);
            if (string.Equals(effective, previousLanguage, StringComparison.OrdinalIgnoreCase) &&
                AppSettingsStore.Current.Language == preference)
            {
                return true;
            }

            ApplicationLanguages.PrimaryLanguageOverride = effective;
            var culture = CultureInfo.GetCultureInfo(effective);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            var replacementLoader = new ResourceLoader();

            AppSettingsStore.Update(settings => settings with { Language = preference });
            lock (_gate)
            {
                _loader = replacementLoader;
                _currentLanguage = effective;
            }

            await Task.Yield();
            LanguageChanged?.Invoke(this, new AppLanguageChangedEventArgs(previousLanguage, effective));
            return true;
        }
        catch
        {
            ApplicationLanguages.PrimaryLanguageOverride = previousOverride;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            lock (_gate)
            {
                _loader = new ResourceLoader();
                _currentLanguage = previousLanguage;
            }
            return false;
        }
        finally
        {
            Volatile.Write(ref _switchInProgress, 0);
        }
    }

    private void ApplyLanguageCore(string effective)
    {
        ApplicationLanguages.PrimaryLanguageOverride = effective;
        var culture = CultureInfo.GetCultureInfo(effective);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        lock (_gate)
        {
            _loader = new ResourceLoader();
            _currentLanguage = effective;
        }
    }

    private static string ResolveEffectiveLanguage(AppLanguagePreference preference)
    {
        if (preference != AppLanguagePreference.System)
        {
            return preference switch
            {
                AppLanguagePreference.Japanese => "ja-JP",
                AppLanguagePreference.English => "en-US",
                _ => "zh-CN"
            };
        }

        var languages = GlobalizationPreferences.Languages;
        var systemLanguage = languages.Count > 0 ? languages[0] : "zh-CN";
        if (systemLanguage.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        if (systemLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en-US";
        if (systemLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        return "en-US";
    }
}
