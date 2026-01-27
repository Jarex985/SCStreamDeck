using System.Globalization;
using SCStreamDeck.Logging;
using SCStreamDeck.Services.Installation;

namespace SCStreamDeck.Services.UI;

public sealed record ThemeInfo(string File, string Name);

public sealed class ThemeService(PathProviderService pathProvider)
{
    private readonly PathProviderService _pathProvider =
        pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

    public string ThemesDirectory => Path.Combine(_pathProvider.BaseDirectory, "PropertyInspector", "css", "themes");

    public IReadOnlyList<ThemeInfo> GetAvailableThemes()
    {
        try
        {
            string dir = ThemesDirectory;
            if (!Directory.Exists(dir))
            {
                return [];
            }

            string[] files = Directory.GetFiles(dir, "*.css", SearchOption.TopDirectoryOnly);

            List<ThemeInfo> themes = [];
            themes.AddRange(from fullPath in files
                select Path.GetFileName(fullPath)
                into fileName
                where !fileName.StartsWith('_')
                select new ThemeInfo(fileName, ToDisplayName(fileName)));

            return themes
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Err($"[{nameof(ThemeService)}] Failed to enumerate themes: {ex.Message}", ex);
            return [];
        }
    }

    public bool IsValidThemeFile(string? themeFile)
    {
        if (string.IsNullOrWhiteSpace(themeFile))
        {
            return false;
        }

        // No subdirectories, no path traversal.
        if (!string.Equals(Path.GetFileName(themeFile), themeFile, StringComparison.Ordinal))
        {
            return false;
        }

        if (!themeFile.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string fullPath = Path.Combine(ThemesDirectory, themeFile);
        return File.Exists(fullPath);
    }

    private static string ToDisplayName(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        name = name.Replace('_', ' ').Replace('-', ' ').Trim();
        if (name.Length == 0)
        {
            return fileName;
        }

        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
        return textInfo.ToTitleCase(name);
    }
}
