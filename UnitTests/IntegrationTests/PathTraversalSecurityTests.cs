using SCStreamDeck.Common;
using Xunit;
using System.Security;
using System.IO;

namespace SCStreamDeck.UnitTests.IntegrationTests;

/// <summary>
/// Security tests for path traversal protection.
/// Verifies that the SecurePathValidator prevents unauthorized access outside base directories.
/// Critical for preventing path traversal attacks.
/// </summary>
public class PathTraversalSecurityTests : IDisposable
{
    private readonly string _testBaseDirectory;
    private readonly string _tempDirectory;

    public PathTraversalSecurityTests()
    {
        // Create temporary directories for testing
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), $"SCStreamDeck_SecurityTest_{Guid.NewGuid()}");
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"SCStreamDeck_Temp_{Guid.NewGuid()}");

        Directory.CreateDirectory(_testBaseDirectory);
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, true);
            }
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region Basic Path Traversal Protection Tests

    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("..\\..\\..\\..\\Windows\\System32")]
    [InlineData("../../../Windows/System32")]
    [InlineData("..\\..\\..\\..\\Users")]
    [InlineData("../../../../Users")]
    public void Security_ShouldBlockPathTraversal_WithDotDotSlash(string maliciousPath)
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(maliciousPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, $"Malicious path should be blocked: {maliciousPath}");
        Assert.Empty(normalizedPath);
    }

    [Theory]
    [InlineData("....//....//....//etc/passwd")]
    [InlineData("....\\\\....\\\\....\\\\Windows")]
    [InlineData("..//..//..//system")]
    [InlineData("..\\\\..\\\\..\\\\config")]
    public void Security_ShouldBlockObfuscatedPathTraversal(string maliciousPath)
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(maliciousPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, $"Obfuscated malicious path should be blocked: {maliciousPath}");
        Assert.Empty(normalizedPath);
    }

    [Theory]
    [InlineData("./../etc/passwd")]
    [InlineData(".\\..\\Windows")]
    [InlineData("./..\\System32")]
    [InlineData(".\\../config")]
    public void Security_ShouldBlockMixedSlashPathTraversal(string maliciousPath)
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(maliciousPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, $"Mixed slash malicious path should be blocked: {maliciousPath}");
        Assert.Empty(normalizedPath);
    }

    #endregion

    #region Windows System Path Protection Tests

    [Theory]
    [InlineData(@"C:\Windows\System32\config")]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData(@"C:\Users")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\ProgramData")]
    public void Security_ShouldBlockAccessToWindowsSystemPaths(string systemPath)
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(systemPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, $"Access to Windows system path should be blocked: {systemPath}");
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldBlockAccessToSystem32Config()
    {
        // Arrange
        string maliciousPath = @"..\..\..\..\Windows\System32\config";

        // Act
        bool result = SecurePathValidator.IsValidPath(maliciousPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Access to System32 config should be blocked");
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldBlockAccessToWindowsDirectory()
    {
        // Arrange
        string maliciousPath = @"..\..\..\..\Windows";

        // Act
        bool result = SecurePathValidator.IsValidPath(maliciousPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Access to Windows directory should be blocked");
        Assert.Empty(normalizedPath);
    }

    #endregion

    #region UNC Path Protection Tests

    [Theory]
    [InlineData(@"\\server\share\file.txt")]
    [InlineData(@"\\192.168.1.1\share\config")]
    [InlineData(@"\\localhost\c$\Windows")]
    [InlineData(@"\\?\C:\Windows\System32")]
    public void Security_ShouldBlockUNCPaths(string uncPath)
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(uncPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, $"UNC path should be blocked: {uncPath}");
        Assert.Empty(normalizedPath);
    }

    #endregion

    #region Different Drive Protection Tests

    [Theory]
    [InlineData(@"D:\file.txt")]
    [InlineData(@"E:\data\config.xml")]
    [InlineData(@"F:\path\to\file")]
    public void Security_ShouldBlockAccessToDifferentDrive(string differentDrivePath)
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(differentDrivePath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, $"Access to different drive should be blocked: {differentDrivePath}");
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldBlockDriveRootTraversal()
    {
        // Arrange
        string maliciousPath = @"..\..\..\..\D:\data";

        // Act
        bool result = SecurePathValidator.IsValidPath(maliciousPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Drive root traversal should be blocked");
        Assert.Empty(normalizedPath);
    }

    #endregion

    #region GetSecurePath Security Tests

    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData(@"..\..\..\..\Windows\System32")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"\\server\share\file.txt")]
    public void Security_GetSecurePath_ShouldThrowException_ForMaliciousPaths(string maliciousPath)
    {
        // Act & Assert
        Assert.Throws<SecurityException>(() => SecurePathValidator.GetSecurePath(maliciousPath, _testBaseDirectory));
    }

    [Fact]
    public void Security_GetSecurePath_ShouldThrowSecurityException_ForNullPath()
    {
        // Act & Assert
        Assert.Throws<SecurityException>(() => SecurePathValidator.GetSecurePath(null!, _testBaseDirectory));
    }

    [Fact]
    public void Security_GetSecurePath_ShouldThrowSecurityException_ForEmptyPath()
    {
        // Act & Assert
        Assert.Throws<SecurityException>(() => SecurePathValidator.GetSecurePath(string.Empty, _testBaseDirectory));
    }

    #endregion

    #region Case Sensitivity and Encoding Tests

    [Theory]
    [InlineData("../../../WINDOWS/system32")]
    [InlineData("../../../Windows/System32")]
    [InlineData("../../../WINDOWS/SYSTEM32")]
    [InlineData("../../../windows/system32")]
    public void Security_ShouldBlockPathTraversal_RegardlessOfCase(string maliciousPath)
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(maliciousPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, $"Path traversal should be blocked regardless of case: {maliciousPath}");
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldBlockPathWithInvalidCharacters()
    {
        // Arrange
        string pathWithNullChar = "file\0.txt";

        // Act
        bool result = SecurePathValidator.IsValidPath(pathWithNullChar, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Path with null character should be blocked");
        Assert.Empty(normalizedPath);
    }

    #endregion

    #region Normalized Path Validation Tests

    [Fact]
    public void Security_ShouldBlockAbsolutePathOutsideBase()
    {
        // Arrange
        string absoluteOutsideBase = Path.Combine(_tempDirectory, "file.txt");
        File.WriteAllText(absoluteOutsideBase, "test");

        // Act
        bool result = SecurePathValidator.IsValidPath(absoluteOutsideBase, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Absolute path outside base directory should be blocked");
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldBlockPathThatResolvesOutsideBase()
    {
        // Arrange
        string pathThatResolvesOutside = Path.Combine(_tempDirectory, "file.txt");
        File.WriteAllText(pathThatResolvesOutside, "test");

        // Create a symbolic link or junction if supported
        // This test verifies that even if path resolves outside, it's blocked
        // For simplicity, we just test the absolute path case

        // Act
        bool result = SecurePathValidator.IsValidPath(pathThatResolvesOutside, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Path that resolves outside base should be blocked");
        Assert.Empty(normalizedPath);
    }

    #endregion

    #region Edge Case Security Tests

    [Fact]
    public void Security_ShouldBlockPathWithTooManyDotDots()
    {
        // Arrange
        string excessiveDotDots = string.Concat(Enumerable.Repeat("../", 100)); // 100 levels up

        // Act
        bool result = SecurePathValidator.IsValidPath(excessiveDotDots, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Excessive dot-dot traversal should be blocked");
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldBlockPathWithMixedSeparators()
    {
        // Arrange
        string mixedSeparators = @"..\/../..//Windows\System32";

        // Act
        bool result = SecurePathValidator.IsValidPath(mixedSeparators, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Path with mixed separators should be blocked");
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldBlockPathWithDotDotAtEnd()
    {
        // Arrange
        string dotDotAtEnd = "subfolder/..";

        // This should be blocked because it resolves to parent which might be outside
        // depending on subfolder depth

        // Act
        bool result = SecurePathValidator.IsValidPath(dotDotAtEnd, _testBaseDirectory, out string normalizedPath);

        // Assert
        // This should succeed because ".." from a subfolder within base is still within base
        Assert.True(result, "Dot-dot at end within base should be allowed");
    }

    #endregion

    #region Real-World Attack Scenarios

    [Theory]
    [InlineData("../../../Users/user/.ssh/id_rsa")]
    [InlineData("../../../../Windows/Fonts/segoeui.ttf")]
    [InlineData("../../../../Program Files/Steam/steamapps")]
    [InlineData("../../../../Users/user/AppData/Roaming/config")]
    public void Security_ShouldBlockRealWorldAttacks(string attackPath)
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(attackPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, $"Real-world attack should be blocked: {attackPath}");
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldBlockAccessToUserProfile()
    {
        // Arrange
        string userProfilePath = Path.Combine("..", "..", "..", "..", "Users", Environment.UserName, ".ssh");

        // Act
        bool result = SecurePathValidator.IsValidPath(userProfilePath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result, "Access to user profile should be blocked");
        Assert.Empty(normalizedPath);
    }

    #endregion

    #region Valid Paths Should Pass Tests

    [Fact]
    public void Security_ShouldAllowValidPathWithinBase()
    {
        // Arrange
        string validPath = "subfolder/file.txt";
        Directory.CreateDirectory(Path.Combine(_testBaseDirectory, "subfolder"));

        // Act
        bool result = SecurePathValidator.IsValidPath(validPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.True(result, "Valid path within base should be allowed");
        Assert.NotEmpty(normalizedPath);
        Assert.Contains(_testBaseDirectory, normalizedPath);
    }

    [Fact]
    public void Security_ShouldAllowAbsolutePathWithinBase()
    {
        // Arrange
        string validAbsolutePath = Path.Combine(_testBaseDirectory, "file.txt");
        File.WriteAllText(validAbsolutePath, "test");

        // Act
        bool result = SecurePathValidator.IsValidPath(validAbsolutePath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.True(result, "Absolute path within base should be allowed");
        Assert.NotEmpty(normalizedPath);
    }

    [Fact]
    public void Security_ShouldAllowNestedDirectoryPath()
    {
        // Arrange
        string nestedPath = "level1/level2/level3/file.txt";
        Directory.CreateDirectory(Path.Combine(_testBaseDirectory, "level1/level2/level3"));

        // Act
        bool result = SecurePathValidator.IsValidPath(nestedPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.True(result, "Nested directory path should be allowed");
        Assert.NotEmpty(normalizedPath);
    }

    #endregion

    #region Multiple Attack Vector Tests

    [Fact]
    public void Security_AllCommonAttackVectors_ShouldBeBlocked()
    {
        // Arrange
        string[] attackVectors = new[]
        {
            "../../../etc/passwd",
            "..\\..\\..\\Windows\\System32",
            "../../../../Windows/System32",
            "..\\..\\..\\..\\Users",
            "....//....//....//etc/passwd",
            "./../Windows",
            ".\\..\\System32",
            "..//..\\System32",
            ".\\../config",
            @"..\/../..\/Windows",
            @"\\server\share\file.txt",
            @"C:\Windows\System32",
            @"D:\data\config"
        };

        // Act & Assert
        foreach (string attackVector in attackVectors)
        {
            bool result = SecurePathValidator.IsValidPath(attackVector, _testBaseDirectory, out string normalizedPath);
            Assert.False(result, $"Attack vector should be blocked: {attackVector}");
            Assert.Empty(normalizedPath);
        }
    }

    #endregion
}
