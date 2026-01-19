using System.Security;
using FluentAssertions;
using SCStreamDeck.Common;

namespace Tests.Unit.Common;

public sealed class SecurePathValidatorTests
{
    [Fact]
    public void IsValidPath_WithinBase_ReturnsTrueAndNormalized()
    {
        string baseDir = CreateTempDirectory();
        string relativePath = Path.Combine("sub", "file.txt");
        Directory.CreateDirectory(Path.Combine(baseDir, "sub"));

        bool result = SecurePathValidator.IsValidPath(relativePath, baseDir, out string normalized);

        result.Should().BeTrue();
        normalized.Should().Be(Path.Combine(baseDir, "sub", "file.txt"));
    }

    [Theory]
    [InlineData(null, "C:/tmp")]
    [InlineData(" ", "C:/tmp")]
    [InlineData("file.txt", null)]
    [InlineData("file.txt", " ")]
    public void IsValidPath_MissingInput_ReturnsFalse(string? path, string? baseDir)
    {
        bool result = SecurePathValidator.IsValidPath(path!, baseDir!, out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void IsValidPath_BlockedBaseDirectory_ReturnsFalse()
    {
        bool result = SecurePathValidator.IsValidPath("file.txt", "C:/Windows/System32", out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void IsValidPath_UncBase_ReturnsFalse()
    {
        bool result = SecurePathValidator.IsValidPath("file.txt", @"\\server\\share", out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void IsValidPath_TraversalSequence_ReturnsFalse()
    {
        string baseDir = CreateTempDirectory();

        bool result = SecurePathValidator.IsValidPath("....//evil.txt", baseDir, out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void IsValidPath_PathEscapesBase_ReturnsFalse()
    {
        string baseDir = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(baseDir, "safe"));

        bool result = SecurePathValidator.IsValidPath(Path.Combine("..", "outside.txt"), baseDir, out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void IsValidPath_PathCausesException_ReturnsFalse()
    {
        string baseDir = CreateTempDirectory();
        string relativePath = "bad\0file.txt";

        bool result = SecurePathValidator.IsValidPath(relativePath, baseDir, out string normalized);

        result.Should().BeFalse("invalid characters should fail validation");
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void GetSecurePath_Invalid_ThrowsSecurityException()
    {
        Action act = () => SecurePathValidator.GetSecurePath("file.txt", "C:/Windows");

        act.Should().Throw<SecurityException>();
    }

    [Fact]
    public void GetSecurePath_Valid_ReturnsNormalized()
    {
        string baseDir = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(baseDir, "child"));
        string relativePath = Path.Combine("child", "doc.txt");

        string normalized = SecurePathValidator.GetSecurePath(relativePath, baseDir);

        normalized.Should().Be(Path.Combine(baseDir, "child", "doc.txt"));
    }

    [Fact]
    public void TryNormalizePath_ValidRelative_ReturnsFullPath()
    {
        string relativePath = Path.Combine("folder", "file.txt");

        bool result = SecurePathValidator.TryNormalizePath(relativePath, out string normalized);

        result.Should().BeTrue();
        normalized.Should().NotBeNullOrWhiteSpace();
        Path.IsPathRooted(normalized).Should().BeTrue();
    }

    [Fact]
    public void TryNormalizePath_InvalidColonUsage_ReturnsFalse()
    {
        bool result = SecurePathValidator.TryNormalizePath("C::/bad/path", out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void TryNormalizePath_InvalidCharacters_ReturnsFalse()
    {
        string path = "C:/bad\0path/file.txt";

        bool result = SecurePathValidator.TryNormalizePath(path, out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void TryNormalizePath_ValidAbsolute_ReturnsTrue()
    {
        string fullPath = Path.Combine(Path.GetTempPath(), "valid.txt");

        bool result = SecurePathValidator.TryNormalizePath(fullPath, out string normalized);

        result.Should().BeTrue();
        normalized.Should().Be(fullPath);
    }

    private static string CreateTempDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
