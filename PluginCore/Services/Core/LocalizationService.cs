using System.Collections.Immutable;
using System.Security;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Data;

namespace SCStreamDeck.Services.Core;

/// <summary>
///     Provides localization services for Star Citizen UI strings.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly ImmutableHashSet<string> s_supportedLanguages = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "chinese_(simplified)", "chinese_(traditional)", "english", "french_(france)",
        "german_(germany)", "italian_(italy)", "japanese_(japan)", "korean_(south_korea)",
        "polish_(poland)", "portuguese_(brazil)", "spanish_(latin_america)", "spanish_(spain)");

    private readonly Dictionary<(string channelPath, string language), Dictionary<string, string>> _cache = new();
    private readonly object _cacheLock = new();
    private readonly IFileSystem _fileSystem;

    private readonly IP4KArchiveService _p4KService;

    public LocalizationService(IP4KArchiveService p4KService, IFileSystem fileSystem)
    {
        _p4KService = p4KService ?? throw new ArgumentNullException(nameof(p4KService));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadGlobalIniAsync(
        string channelPath,
        string language,
        string dataP4KPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataP4KPath);

        language = NormalizeLanguage(language);
        (string channelPath, string language) cacheKey = (channelPath, language);

        Dictionary<string, string>? cached = GetFromCache(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        string? content = await TryLoadContentAsync(channelPath, language, dataP4KPath, cancellationToken)
            .ConfigureAwait(false);

        if (content != null)
        {
            Dictionary<string, string> parsed = ParseGlobalIni(content);
            SetCache(cacheKey, parsed);
            return parsed;
        }

        if (!language.Equals(SCConstants.Localization.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warn(
                $"[{nameof(LocalizationService)}] Language '{language}' not found, falling back to {SCConstants.Localization.DefaultLanguage}");
            return await LoadGlobalIniAsync(channelPath, SCConstants.Localization.DefaultLanguage, dataP4KPath,
                cancellationToken).ConfigureAwait(false);
        }

        Log.Err($"[{nameof(LocalizationService)}] Failed to load any global.ini, returning empty");
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }


    public async Task<string> ReadLanguageSettingAsync(
        string channelPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelPath);
        string userConfigPath = Path.Combine(channelPath, SCConstants.Files.UserConfigFileName);

        if (!SecurePathValidator.TryNormalizePath(userConfigPath, out string validPath))
        {
            Log.Warn(
                $"[{nameof(LocalizationService)}] Invalid {SCConstants.Files.UserConfigFileName} path: {userConfigPath}");
            return SCConstants.Localization.DefaultLanguage;
        }

        if (!_fileSystem.FileExists(validPath))
        {
            return SCConstants.Localization.DefaultLanguage;
        }

        try
        {
            string[] lines = await _fileSystem.ReadAllLinesAsync(validPath, cancellationToken).ConfigureAwait(false);
            return ParseLanguageFromLines(lines);
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
        {
            Log.Err($"[{nameof(LocalizationService)}] Failed to read {SCConstants.Files.UserConfigFileName}", ex);
            return SCConstants.Localization.DefaultLanguage;
        }
    }


    public void ClearCache(string channelPath, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        (string channelPath, string) cacheKey = (channelPath, language.ToUpperInvariant());

        lock (_cacheLock)
        {
            if (_cache.Remove(cacheKey))
            {
                Log.Debug($"[{nameof(LocalizationService)}] Cleared cache for {language}");
            }
        }
    }

    private string NormalizeLanguage(string language)
    {
        language = language.Trim().ToUpperInvariant();
        if (s_supportedLanguages.Contains(language))
        {
            return language;
        }

        Log.Warn(
            $"[{nameof(LocalizationService)}] Unsupported language '{language}', using {SCConstants.Localization.DefaultLanguage}");
        return SCConstants.Localization.DefaultLanguage;
    }

    private Dictionary<string, string>? GetFromCache((string channelPath, string language) cacheKey)
    {
        lock (_cacheLock)
        {
            return _cache.TryGetValue(cacheKey, out Dictionary<string, string>? cached) ? cached : null;
        }
    }

    private void SetCache((string channelPath, string language) cacheKey, Dictionary<string, string> values)
    {
        lock (_cacheLock)
        {
            _cache[cacheKey] = values;
        }
    }

    private async Task<string?> TryLoadContentAsync(
        string channelPath,
        string language,
        string dataP4KPath,
        CancellationToken cancellationToken)
    {
        string? content = await LoadFromOverrideFolderAsync(channelPath, language, cancellationToken).ConfigureAwait(false);
        if (content != null)
        {
            return content;
        }

        return await LoadFromP4KAsync(dataP4KPath, language, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> LoadFromOverrideFolderAsync(
        string channelPath,
        string language,
        CancellationToken cancellationToken)
    {
        string overridePath = Path.Combine(channelPath, "data", SCConstants.Localization.LocalizationSubdirectory, language,
            SCConstants.Files.GlobalIniFileName);

        if (!SecurePathValidator.TryNormalizePath(overridePath, out string validPath))
        {
            Log.Warn($"[{nameof(LocalizationService)}] Invalid override path: {overridePath}");
            return null;
        }

        if (!_fileSystem.FileExists(validPath))
        {
            return null;
        }

        try
        {
            return await _fileSystem.ReadAllTextAsync(validPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException)
        {
            Log.Err($"[{nameof(LocalizationService)}] Failed to read override", ex);
            return null;
        }
    }

    private async Task<string?> LoadFromP4KAsync(
        string dataP4KPath,
        string language,
        CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(dataP4KPath))
        {
            Log.Err($"[{nameof(LocalizationService)}] Data.p4k not found: {dataP4KPath}");
            return null;
        }

        try
        {
            bool opened = await _p4KService.OpenArchiveAsync(dataP4KPath, cancellationToken).ConfigureAwait(false);
            if (!opened)
            {
                Log.Err($"[{nameof(LocalizationService)}] Failed to open Data.p4k");
                return null;
            }

            try
            {
                string directory = $"{SCConstants.Paths.LocalizationBaseDirectory}/{language}";
                IReadOnlyList<P4KFileEntry> entries = await _p4KService
                    .ScanDirectoryAsync(directory, SCConstants.Files.GlobalIniFileName, cancellationToken)
                    .ConfigureAwait(false);

                if (entries.Count == 0)
                {
                    Log.Warn(
                        $"[{nameof(LocalizationService)}] {SCConstants.Files.GlobalIniFileName} not found in P4K for {language}");
                    return null;
                }

                string? content = await _p4KService.ReadFileAsTextAsync(entries[0], cancellationToken)
                    .ConfigureAwait(false);
                return content;
            }

            finally
            {
                _p4KService.CloseArchive();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(LocalizationService)}] Failed to extract from P4K", ex);
            return null;
        }
    }

    private static bool IsCommentOrEmptyLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return true;
        }

        string trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return true;
        }

        foreach (string prefix in SCConstants.Localization.IniCommentPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static (string? key, string? value) ParseKeyValueLine(string line)
    {
        int equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return (null, null);
        }

        string key = line[..equalsIndex].Trim();
        string value = line[(equalsIndex + 1)..].Trim();

        if (string.IsNullOrEmpty(key))
        {
            return (null, null);
        }

        return (key, value);
    }

    private static string TransformIniKey(string key)
    {
        if (key.StartsWith("ui_", StringComparison.OrdinalIgnoreCase))
        {
            return "@" + key;
        }

        return key;
    }

    private static string NormalizeLanguageFromConfig(string value)
    {
        string normalized = value.ToUpperInvariant();
        if (s_supportedLanguages.Contains(normalized))
        {
            return normalized;
        }

        return SCConstants.Localization.DefaultLanguage;
    }


    internal static Dictionary<string, string> ParseGlobalIni(string content)
    {
        Dictionary<string, string> dictionary = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(content))
        {
            return dictionary;
        }

        using StringReader reader = new(content);

        while (reader.ReadLine() is { } line)
        {
            if (IsCommentOrEmptyLine(line))
            {
                continue;
            }

            (string? key, string? value) = ParseKeyValueLine(line);
            if (key == null)
            {
                continue;
            }

            string transformedKey = TransformIniKey(key);
            dictionary[transformedKey] = value ?? string.Empty;
        }

        return dictionary;
    }

    internal static string ParseLanguageFromLines(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            if (IsCommentOrEmptyLine(line))
            {
                continue;
            }

            string trimmed = line.Trim();

            if (!trimmed.StartsWith(SCConstants.Localization.LanguageConfigKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (string? key, string? value) = ParseKeyValueLine(trimmed);
            if (value == null || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string normalized = NormalizeLanguageFromConfig(value);

            if (normalized.Equals(SCConstants.Localization.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                Log.Warn(
                    $"[{nameof(LocalizationService)}] Invalid language '{value}', using {SCConstants.Localization.DefaultLanguage}");
            }

            return normalized;
        }

        return SCConstants.Localization.DefaultLanguage;
    }
}
