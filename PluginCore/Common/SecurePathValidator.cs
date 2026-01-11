using System.Security;
using BarRaider.SdTools;

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

        catch (Exception ex) when (ex is ArgumentException or SecurityException or NotSupportedException or PathTooLongException)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"[{nameof(SecurePathValidator)}] '{path}': {ex.Message}");
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
    public static string GetSecurePath(string path, string baseDirectory) =>
        !IsValidPath(path, baseDirectory, out string normalized)
            ? throw new SecurityException($"Invalid or unsafe path detected. Path must be within: {baseDirectory}")
            : normalized;

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

        catch (Exception ex) when (ex is ArgumentException or SecurityException or NotSupportedException or PathTooLongException)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"[{nameof(SecurePathValidator)}] '{path}': {ex.Message}");
            return false;
        }
    }
}
