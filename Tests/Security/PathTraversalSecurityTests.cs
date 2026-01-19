using System.Security;
using FluentAssertions;
using SCStreamDeck.Common;

namespace Tests.Security;

public sealed class PathTraversalSecurityTests
{
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData(@"..\..\Windows\System32\drivers\etc\hosts")]
    [InlineData("folder/../../Windows/System32/config")]
    [InlineData("....//evil.txt")]
    [InlineData(@"..\mixed/..\Windows")]
    [InlineData(@"..\..\..\escape.txt")]
    [InlineData(@"\\server\share\secret.txt")]
    [InlineData(@"\\?\C:\Windows\System32\config\SAM")]
    [InlineData(@"C:\Windows\System32\kernel32.dll")]
    [InlineData(@"D:\Windows\System32\drivers\etc\hosts")]
    [InlineData("/etc/passwd")]
    [InlineData("/Windows/System32/config")]
    [InlineData("%2e%2e/encoded.txt")]
    [InlineData("..%5c..%5cSystem32\\evil.dll")]
    public void IsValidPath_BlocksTraversalVectors(string attackPath)
    {
        string baseDir = CreateSafeBaseDirectory();

        bool result = SecurePathValidator.IsValidPath(attackPath, baseDir, out string normalizedPath);

        if (attackPath.Contains("%", StringComparison.Ordinal))
        {
            result.Should().BeTrue();
            normalizedPath.Should().StartWith(baseDir);
        }
        else
        {
            result.Should().BeFalse();
            normalizedPath.Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("....//evil.txt")]
    [InlineData(@"\\server\share\secret.txt")]
    [InlineData(@"C:\Windows\System32\config\SAM")]
    [InlineData("/etc/passwd")]
    public void GetSecurePath_ThrowsOnTraversal(string attackPath)
    {
        string baseDir = CreateSafeBaseDirectory();

        Action act = () => SecurePathValidator.GetSecurePath(attackPath, baseDir);

        act.Should().Throw<SecurityException>();
    }

    [Fact]
    public void IsValidPath_AllowsSafeRelativePath()
    {
        string baseDir = CreateSafeBaseDirectory();
        Directory.CreateDirectory(Path.Combine(baseDir, "safe"));

        bool result = SecurePathValidator.IsValidPath(Path.Combine("safe", "file.txt"), baseDir, out string normalized);

        result.Should().BeTrue();
        normalized.Should().Be(Path.Combine(baseDir, "safe", "file.txt"));
    }

    private static string CreateSafeBaseDirectory()
    {
        string tempBase = Path.Combine(Path.GetTempPath(), $"scsd-secure-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempBase);
        return tempBase;
    }
}
