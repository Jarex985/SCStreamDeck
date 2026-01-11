using System.Security;

namespace SCStreamDeck.Common;

/// <summary>
///     Provides secure path validation to prevent path traversal attacks.
///     Validates that file paths stay within expected base directories.
/// </summary>
public static class SecurePathValidator
{
    /// <summary>
    ///     Validates that a path is safe and within the specified base directory.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="baseDirectory">The base directory that the path must be within.</param>
    /// <param name="normalizedPath">The normalized full path if valid, empty string otherwise.</param>
    /// <returns>True if the path is valid and safe, false otherwise.</returns>
    public static bool IsValidPath(string path, string baseDirectory, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return false;
        }

        // Prevent using system directories as base
        if (baseDirectory.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            baseDirectory.Contains("System32", StringComparison.OrdinalIgnoreCase) ||
            baseDirectory.StartsWith(@"\\", StringComparison.Ordinal) ||
            !Directory.Exists(baseDirectory))
        {
            return false;
        }

        // Prevent path traversal attacks
        if (path.Contains("...."))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, path));
            string fullBase = Path.GetFullPath(baseDirectory);

            if (fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = fullPath;
                return true;
            }

            normalizedPath = string.Empty;
            return false;
        }
        catch (ArgumentException)
        {
            // Path contains invalid characters or is malformed
            normalizedPath = string.Empty;
            return false;
        }
        catch (SecurityException)
        {
            // Caller does not have required permissions
            normalizedPath = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            // Path contains a colon in an invalid position
            normalizedPath = string.Empty;
            return false;
        }
        catch (PathTooLongException)
        {
            // Path exceeds system-defined maximum length
            normalizedPath = string.Empty;
            return false;
        }
    }

    /// <summary>
    ///     Gets a secure, validated path or throws a SecurityException if validation fails.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="baseDirectory">The base directory that the path must be within.</param>
    /// <returns>The normalized full path.</returns>
    /// <exception cref="SecurityException">Thrown when path traversal is detected or path is invalid.</exception>
    public static string GetSecurePath(string path, string baseDirectory)
    {
        if (!IsValidPath(path, baseDirectory, out string normalized))
        {
            throw new SecurityException($"Invalid or unsafe path detected. Path must be within: {baseDirectory}");
        }

        return normalized;
    }

    /// <summary>
    ///     Validates a path without requiring a specific base directory.
    ///     Only ensures the path is well-formed and resolves to an absolute path.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="normalizedPath">The normalized full path if valid.</param>
    /// <returns>True if the path is well-formed, false otherwise.</returns>
    public static bool TryNormalizePath(string path, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Check for invalid colon usage (more than one colon indicates malformed path)
        if (path.Count(c => c == ':') > 1)
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }
}
