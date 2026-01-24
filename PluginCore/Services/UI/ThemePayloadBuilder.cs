using Newtonsoft.Json.Linq;

namespace SCStreamDeck.Services.UI;

internal static class ThemePayloadBuilder
{
    public static (JArray Themes, string SelectedTheme) Build(
        IReadOnlyList<ThemeInfo> themes,
        string? selectedThemeFile,
        Func<string?, bool> isValidThemeFile)
    {
        ArgumentNullException.ThrowIfNull(themes);
        ArgumentNullException.ThrowIfNull(isValidThemeFile);

        const string defaultThemeFile = "default.css";

        if (string.IsNullOrWhiteSpace(selectedThemeFile) || !isValidThemeFile(selectedThemeFile))
        {
            ThemeInfo? defaultTheme = themes.FirstOrDefault(t =>
                string.Equals(t.File, defaultThemeFile, StringComparison.OrdinalIgnoreCase));

            if (defaultTheme != null && isValidThemeFile(defaultTheme.File))
            {
                selectedThemeFile = defaultTheme.File;
            }
            else
            {
                selectedThemeFile = themes.Count > 0 ? themes[0].File : null;
            }
        }

        string selectedTheme = selectedThemeFile ?? string.Empty;

        JArray payload = [];
        foreach (ThemeInfo t in themes)
        {
            payload.Add(new JObject { ["file"] = t.File, ["name"] = t.Name });
        }

        return (payload, selectedTheme);
    }
}
