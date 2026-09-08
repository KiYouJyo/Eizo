using Microsoft.UI.Xaml;
using System.Text.Json;

namespace Eizo;

internal static class ThemePreferenceStore
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "theme-preference.json");

    public static ElementTheme Load()
    {
        try
        {
            if (!File.Exists(PreferencePath)) return ElementTheme.Default;
            var preference = JsonSerializer.Deserialize<ThemePreference>(
                File.ReadAllText(PreferencePath));

            return preference?.Theme is "Light" ? ElementTheme.Light
                : preference?.Theme is "Dark" ? ElementTheme.Dark
                : ElementTheme.Default;
        }
        catch (IOException)
        {
            return ElementTheme.Default;
        }
        catch (JsonException)
        {
            return ElementTheme.Default;
        }
    }

    public static void Save(ElementTheme theme)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            var temporaryPath =
                $"{PreferencePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

            var value = theme switch
            {
                ElementTheme.Light => "Light",
                ElementTheme.Dark => "Dark",
                _ => "Default"
            };

            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(new ThemePreference(value)));

            File.Move(temporaryPath, PreferencePath, overwrite: true);
        }
        catch (IOException)
        {
            // A settings write must never break an interactive theme switch.
        }
    }

    private sealed record ThemePreference(string Theme);
}
