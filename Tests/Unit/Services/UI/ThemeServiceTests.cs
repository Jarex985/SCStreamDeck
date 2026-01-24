using FluentAssertions;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.UI;

namespace Tests.Unit.Services.UI;

public sealed class ThemeServiceTests
{
    [Fact]
    public void GetAvailableThemes_ReturnsEmpty_WhenThemesDirectoryMissing()
    {
        using TempDir temp = new();
        TestPathProviderService pathProvider = new(temp.Path);
        pathProvider.EnsureCacheDirExists();
        ThemeService service = new(pathProvider);

        IReadOnlyList<ThemeInfo> themes = service.GetAvailableThemes();

        themes.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableThemes_FiltersUnderscore_AndSortsByDisplayName()
    {
        using TempDir temp = new();
        TestPathProviderService pathProvider = new(temp.Path);
        pathProvider.EnsureCacheDirExists();
        ThemeService service = new(pathProvider);

        Directory.CreateDirectory(service.ThemesDirectory);
        File.WriteAllText(Path.Combine(service.ThemesDirectory, "_template.css"), "/* ignored */");
        File.WriteAllText(Path.Combine(service.ThemesDirectory, "zulu-theme.css"), "/* ok */");
        File.WriteAllText(Path.Combine(service.ThemesDirectory, "bravo_theme.css"), "/* ok */");
        File.WriteAllText(Path.Combine(service.ThemesDirectory, "alpha.css"), "/* ok */");

        IReadOnlyList<ThemeInfo> themes = service.GetAvailableThemes();

        themes.Select(t => t.File).Should().Equal("alpha.css", "bravo_theme.css", "zulu-theme.css");
        themes.Select(t => t.Name).Should().Equal("Alpha", "Bravo Theme", "Zulu Theme");
    }

    [Fact]
    public void IsValidThemeFile_RejectsSubdirectoriesAndNonCss_AndRequiresFileExists()
    {
        using TempDir temp = new();
        TestPathProviderService pathProvider = new(temp.Path);
        pathProvider.EnsureCacheDirExists();
        ThemeService service = new(pathProvider);
        Directory.CreateDirectory(service.ThemesDirectory);
        File.WriteAllText(Path.Combine(service.ThemesDirectory, "alpha.css"), "/* ok */");

        service.IsValidThemeFile(null).Should().BeFalse();
        service.IsValidThemeFile("").Should().BeFalse();
        service.IsValidThemeFile("sub\\alpha.css").Should().BeFalse();
        service.IsValidThemeFile("..\\alpha.css").Should().BeFalse();
        service.IsValidThemeFile("alpha.txt").Should().BeFalse();
        service.IsValidThemeFile("missing.css").Should().BeFalse();
        service.IsValidThemeFile("alpha.css").Should().BeTrue();
    }

    private sealed class TestPathProviderService(string baseDir) : PathProviderService(baseDir, Path.Combine(baseDir, "cache"))
    {
        public void EnsureCacheDirExists() => Directory.CreateDirectory(CacheDirectory);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SCStreamDeck.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
