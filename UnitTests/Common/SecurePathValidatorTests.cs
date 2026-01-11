using SCStreamDeck.Common;
using Xunit;
using System;
using System.IO;
using System.Security;

namespace SCStreamDeck.UnitTests.Common;

/// <summary>
/// Unit tests for SecurePathValidator to ensure path traversal protection.
/// Security-critical component - prevents access outside base directory.
/// </summary>
public class SecurePathValidatorTests : IDisposable
{
    private readonly string _testBaseDirectory;
    private readonly string _tempBaseDirectory;

    public SecurePathValidatorTests()
    {
        // Create temporary directories for testing
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), $"SCStreamDeck_Test_{Guid.NewGuid()}");
        _tempBaseDirectory = Path.Combine(Path.GetTempPath(), $"SCStreamDeck_Temp_{Guid.NewGuid()}");

        Directory.CreateDirectory(_testBaseDirectory);
        Directory.CreateDirectory(_tempBaseDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, true);
            }
            if (Directory.Exists(_tempBaseDirectory))
            {
                Directory.Delete(_tempBaseDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }


    #region IsValidPath - Valid Paths Tests

    [Fact]
    public void IsValidPath_ShouldReturnTrue_ForValidPathWithinBaseDirectory()
    {
        // Arrange
        string relativePath = Path.Combine("subfolder", "file.txt");
        Directory.CreateDirectory(Path.Combine(_testBaseDirectory, "subfolder"));

        // Act
        bool result = SecurePathValidator.IsValidPath(relativePath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.NotEmpty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnTrue_ForAbsolutePathWithinBaseDirectory()
    {
        // Arrange
        string absolutePath = Path.Combine(_testBaseDirectory, "file.txt");
        File.WriteAllText(absolutePath, "test");

        // Act
        bool result = SecurePathValidator.IsValidPath(absolutePath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.NotEmpty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnTrue_ForCurrentDirectoryPath()
    {
        // Arrange
        string currentDirPath = ".";

        // Act
        bool result = SecurePathValidator.IsValidPath(currentDirPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(_testBaseDirectory, normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnTrue_ForParentDirectoryWithinBase()
    {
        // Arrange
        string subDir = Path.Combine(_testBaseDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        string parentPath = Path.Combine(subDir, "..");

        // Act
        bool result = SecurePathValidator.IsValidPath(parentPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(_testBaseDirectory, normalizedPath);
    }

    #endregion

    #region IsValidPath - Path Traversal Protection Tests

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForPathTraversalWithDotDot()
    {
        // Arrange
        string traversalPath = Path.Combine("..", "..", "Windows", "System32");

        // Act
        bool result = SecurePathValidator.IsValidPath(traversalPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForUnixStylePathTraversal()
    {
        // Arrange
        string traversalPath = "../../../etc/passwd";

        // Act
        bool result = SecurePathValidator.IsValidPath(traversalPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForAbsolutePathOutsideBaseDirectory()
    {
        // Arrange
        string windowsPath = @"C:\Windows\System32\config";

        // Act
        bool result = SecurePathValidator.IsValidPath(windowsPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForDifferentDrive()
    {
        // Arrange
        string differentDrivePath = @"D:\Some\Path\file.txt";

        // Act
        bool result = SecurePathValidator.IsValidPath(differentDrivePath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForUNCPath()
    {
        // Arrange
        string uncPath = @"\\server\share\file.txt";

        // Act
        bool result = SecurePathValidator.IsValidPath(uncPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    #endregion

    #region IsValidPath - Edge Cases and Invalid Inputs Tests

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForNullPath()
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(null!, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForEmptyPath()
    {
        // Act
        bool result = SecurePathValidator.IsValidPath(string.Empty, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForWhitespacePath()
    {
        // Act
        bool result = SecurePathValidator.IsValidPath("   ", _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForNullBaseDirectory()
    {
        // Arrange
        string validPath = "file.txt";

        // Act
        bool result = SecurePathValidator.IsValidPath(validPath, null!, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForInvalidCharactersInPath()
    {
        // Arrange
        string invalidPath = "file\0.txt"; // Null character is invalid

        // Act
        bool result = SecurePathValidator.IsValidPath(invalidPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForPathWithInvalidColon()
    {
        // Arrange
        string invalidPath = "C:invalid:file.txt";

        // Act
        bool result = SecurePathValidator.IsValidPath(invalidPath, _testBaseDirectory, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void IsValidPath_ShouldBeCaseInsensitive()
    {
        // Arrange
        string path = Path.Combine(_testBaseDirectory, "File.TXT");
        File.WriteAllText(path, "test");

        // Act
        bool result1 = SecurePathValidator.IsValidPath("FILE.TXT", _testBaseDirectory, out string norm1);
        bool result2 = SecurePathValidator.IsValidPath("file.txt", _testBaseDirectory, out string norm2);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
    }

    #endregion

    #region GetSecurePath Tests

    [Fact]
    public void GetSecurePath_ShouldReturnNormalizedPath_ForValidInput()
    {
        // Arrange
        string relativePath = Path.Combine("subfolder", "file.txt");
        Directory.CreateDirectory(Path.Combine(_testBaseDirectory, "subfolder"));

        // Act
        string result = SecurePathValidator.GetSecurePath(relativePath, _testBaseDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_testBaseDirectory, result);
        Assert.EndsWith("file.txt", result);
    }

    [Fact]
    public void GetSecurePath_ShouldThrowSecurityException_ForPathTraversal()
    {
        // Arrange
        string traversalPath = Path.Combine("..", "..", "Windows", "System32");

        // Act & Assert
        Assert.Throws<SecurityException>(() => SecurePathValidator.GetSecurePath(traversalPath, _testBaseDirectory));
    }

    [Fact]
    public void GetSecurePath_ShouldThrowSecurityException_ForNullPath()
    {
        // Act & Assert
        Assert.Throws<SecurityException>(() => SecurePathValidator.GetSecurePath(null!, _testBaseDirectory));
    }

    [Fact]
    public void GetSecurePath_ShouldThrowSecurityException_ForEmptyPath()
    {
        // Act & Assert
        Assert.Throws<SecurityException>(() => SecurePathValidator.GetSecurePath(string.Empty, _testBaseDirectory));
    }

    [Fact]
    public void GetSecurePath_ShouldThrowSecurityException_ForPathOutsideBaseDirectory()
    {
        // Arrange
        string externalPath = @"C:\Windows\System32";

        // Act & Assert
        Assert.Throws<SecurityException>(() => SecurePathValidator.GetSecurePath(externalPath, _testBaseDirectory));
    }

    #endregion

    #region TryNormalizePath Tests

    [Fact]
    public void TryNormalizePath_ShouldReturnTrue_ForValidAbsolutePath()
    {
        // Arrange
        string absolutePath = Path.Combine(_testBaseDirectory, "file.txt");

        // Act
        bool result = SecurePathValidator.TryNormalizePath(absolutePath, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.NotEmpty(normalizedPath);
    }

    [Fact]
    public void TryNormalizePath_ShouldReturnTrue_ForRelativePath()
    {
        // Arrange
        string relativePath = Path.Combine("..", "Temp", "file.txt");

        // Act
        bool result = SecurePathValidator.TryNormalizePath(relativePath, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.NotEmpty(normalizedPath);
    }

    [Fact]
    public void TryNormalizePath_ShouldReturnFalse_ForNullPath()
    {
        // Act
        bool result = SecurePathValidator.TryNormalizePath(null!, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void TryNormalizePath_ShouldReturnFalse_ForEmptyPath()
    {
        // Act
        bool result = SecurePathValidator.TryNormalizePath(string.Empty, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void TryNormalizePath_ShouldReturnFalse_ForWhitespacePath()
    {
        // Act
        bool result = SecurePathValidator.TryNormalizePath("   ", out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void TryNormalizePath_ShouldReturnFalse_ForInvalidPath()
    {
        // Arrange
        string invalidPath = "file\0.txt";

        // Act
        bool result = SecurePathValidator.TryNormalizePath(invalidPath, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void TryNormalizePath_ShouldReturnFalse_ForPathWithInvalidColon()
    {
        // Arrange
        string invalidPath = "C:invalid:path";

        // Act
        bool result = SecurePathValidator.TryNormalizePath(invalidPath, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    [Fact]
    public void TryNormalizePath_ShouldHandleCurrentDirectory()
    {
        // Arrange
        string currentDir = ".";

        // Act
        bool result = SecurePathValidator.TryNormalizePath(currentDir, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.NotEmpty(normalizedPath);
    }

    #endregion

    #region Security Scenarios - Combined Tests

    [Fact]
    public void Security_MultiplePathTraversalAttempts_ShouldAllFail()
    {
        // Arrange
        string[] attackVectors = new[]
        {
            "../../../etc/passwd",
            "..\\..\\..\\Windows\\System32",
            "../../../../Windows/System32",
            "..\\..\\..\\..\\Users",
            "....//....//....//etc/passwd",
            Path.Combine("..", "..", "..", "config")
        };

        // Act & Assert
        foreach (string attackVector in attackVectors)
        {
            bool result = SecurePathValidator.IsValidPath(attackVector, _testBaseDirectory, out string normalizedPath);
            Assert.False(result, $"Attack vector should fail: {attackVector}");
            Assert.Empty(normalizedPath);
        }
    }

    [Fact]
    public void Security_VariousInvalidBaseDirectories_ShouldAllFail()
    {
        // Arrange
        string[] invalidBases = new[]
        {
            null!,
            string.Empty,
            "   ",
            @"C:\Windows\System32", // System directory
            @"\\network\share", // UNC path
            "D:\nonexistent" // Different drive
        };

        string validPath = "file.txt";

        // Act & Assert
        foreach (string invalidBase in invalidBases)
        {
            bool result = SecurePathValidator.IsValidPath(validPath, invalidBase, out string normalizedPath);
            Assert.False(result, $"Invalid base should fail: {invalidBase ?? "null"}");
        }
    }

    #endregion

    #region Real-World Scenarios

    [Fact]
    public void RealWorld_SCInstallationPath_ShouldBeValid()
    {
        // Arrange
        string scBase = _testBaseDirectory;
        string relativePath = @"Data\Data.p4k";

        // Act
        bool result = SecurePathValidator.IsValidPath(relativePath, scBase, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.Contains("Data.p4k", normalizedPath);
    }

    [Fact]
    public void RealWorld_KeybindingFile_ShouldBeValid()
    {
        // Arrange
        string scBase = _testBaseDirectory;
        string keybindingPath = @"USER\Client\0\Profiles\default\actionmaps.xml";

        // Act
        bool result = SecurePathValidator.IsValidPath(keybindingPath, scBase, out string normalizedPath);

        // Assert
        Assert.True(result);
        Assert.Contains("actionmaps.xml", normalizedPath);
    }

    [Fact]
    public void RealWorld_ConfigDirectoryTraversal_ShouldBeInvalid()
    {
        // Arrange
        string scBase = @"C:\Programs\Roberts Space Industries\StarCitizen";
        string traversalPath = @"..\..\..\..\Windows\System32\config";

        // Act
        bool result = SecurePathValidator.IsValidPath(traversalPath, scBase, out string normalizedPath);

        // Assert
        Assert.False(result);
        Assert.Empty(normalizedPath);
    }

    #endregion
}
