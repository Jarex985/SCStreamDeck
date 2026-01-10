using System.Security;

namespace SCStreamDeck.SCCore.Common;

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

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(baseDirectory)) return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullBase = Path.GetFullPath(baseDirectory);

            normalizedPath = fullPath;

            // Ensure the resolved path starts with the base directory (case-insensitive for Windows)
            return fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            // Path contains invalid characters or is malformed
            return false;
        }
        catch (SecurityException)
        {
            // Caller does not have required permissions
            return false;
        }
        catch (NotSupportedException)
        {
            // Path contains a colon in an invalid position
            return false;
        }
        catch (PathTooLongException)
        {
            // Path exceeds system-defined maximum length
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
        if (!IsValidPath(path, baseDirectory, out var normalized))
            throw new SecurityException($"Invalid or unsafe path detected. Path must be within: {baseDirectory}");

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

        if (string.IsNullOrWhiteSpace(path)) return false;

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