using System.Collections.Immutable;
using BarRaider.SdTools;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Data;

namespace SCStreamDeck.Services.Core;

/// <summary>
///     Provides localization services for Star Citizen UI strings.
/// </summary>
public sealed class LocalizationService(IP4KArchiveService p4KService) : ILocalizationService
{
    private static readonly ImmutableHashSet<string> s_supportedLanguages = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "chinese_(simplified)", "chinese_(traditional)", "english", "french_(france)",
        "german_(germany)", "italian_(italy)", "japanese_(japan)", "korean_(south_korea)",
        "polish_(poland)", "portuguese_(brazil)", "spanish_(latin_america)", "spanish_(spain)");

    private readonly Dictionary<(string channelPath, string language), Dictionary<string, string>> _cache = new();
    private readonly object _cacheLock = new();

    private readonly IP4KArchiveService _p4KService = p4KService ?? throw new ArgumentNullException(nameof(p4KService));

    public async Task<IReadOnlyDictionary<string, string>> LoadGlobalIniAsync(
        string channelPath,
        string language,
        string dataP4kPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataP4kPath);

        language = NormalizeLanguage(language);
        (string channelPath, string language) cacheKey = (channelPath, language);

        // Check cache
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cached))
            {
                return cached;
            }
        }


        // Load from sources
        string? content = await TryLoadContentAsync(channelPath, language, dataP4kPath, cancellationToken)
            .ConfigureAwait(false);

        if (content != null)
        {
            Dictionary<string, string> parsed = ParseGlobalIni(content);
            lock (_cacheLock)
            {
                _cache[cacheKey] = parsed;
            }

            return parsed;
        }

        // Fallback to English
        if (!language.Equals(SCConstants.Localization.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[Localization] Language '{language}' not found, falling back to {SCConstants.Localization.DefaultLanguage}");
            return await LoadGlobalIniAsync(channelPath, SCConstants.Localization.DefaultLanguage, dataP4kPath,
                cancellationToken).ConfigureAwait(false);
        }

        Logger.Instance.LogMessage(TracingLevel.ERROR, "[Localization] Failed to load any global.ini, returning empty");
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
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[Localization] Invalid {SCConstants.Files.UserConfigFileName} path: {userConfigPath}");
            return SCConstants.Localization.DefaultLanguage;
        }

        if (!File.Exists(validPath))
        {
            return SCConstants.Localization.DefaultLanguage;
        }

        try
        {
            string[] lines = await File.ReadAllLinesAsync(validPath, cancellationToken).ConfigureAwait(false);
            return ParseLanguageFromLines(lines);
        }

        catch (IOException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[Localization] Failed to read {SCConstants.Files.UserConfigFileName} (I/O error): {ex.Message}");
            return SCConstants.Localization.DefaultLanguage;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[Localization] Failed to read {SCConstants.Files.UserConfigFileName} (access denied): {ex.Message}");
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
                Logger.Instance.LogMessage(TracingLevel.INFO, $"[Localization] Cleared cache for {language}");
            }
        }
    }

    private static string NormalizeLanguage(string language)
    {
        language = language.Trim().ToUpperInvariant();
        if (s_supportedLanguages.Contains(language))
        {
            return language;
        }
        Logger.Instance.LogMessage(TracingLevel.WARN,
            $"[Localization] Unsupported language '{language}', using {SCConstants.Localization.DefaultLanguage}");
        return SCConstants.Localization.DefaultLanguage;
    }

    private async Task<string?> TryLoadContentAsync(
        string channelPath,
        string language,
        string dataP4kPath,
        CancellationToken cancellationToken)
    {
        // Priority 1: Override folder
        string? content = await LoadFromOverrideFolderAsync(channelPath, language, cancellationToken).ConfigureAwait(false);
        if (content != null)
        {
            return content;
        }

        // Priority 2: P4K
        return await LoadFromP4KAsync(dataP4kPath, language, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> LoadFromOverrideFolderAsync(
        string channelPath,
        string language,
        CancellationToken cancellationToken)
    {
        string overridePath = Path.Combine(channelPath, "data", SCConstants.Localization.LocalizationSubdirectory, language,
            SCConstants.Files.GlobalIniFileName);

        if (!SecurePathValidator.TryNormalizePath(overridePath, out string validPath))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"[Localization] Invalid override path: {overridePath}");
            return null;
        }

        if (!File.Exists(validPath))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(validPath, cancellationToken).ConfigureAwait(false);
        }

        catch (IOException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[Localization] Failed to read override (I/O error): {ex.Message}");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[Localization] Failed to read override (access denied): {ex.Message}");
            return null;
        }
    }

    private async Task<string?> LoadFromP4KAsync(
        string dataP4kPath,
        string language,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(dataP4kPath))
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"[Localization] Data.p4k not found: {dataP4kPath}");
            return null;
        }

        try
        {
            bool opened = await _p4KService.OpenArchiveAsync(dataP4kPath, cancellationToken).ConfigureAwait(false);
            if (!opened)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "[Localization] Failed to open Data.p4k");
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
                    Logger.Instance.LogMessage(TracingLevel.WARN,
                        $"[Localization] {SCConstants.Files.GlobalIniFileName} not found in P4K for {language}");
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

        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"[Localization] Failed to extract from P4K: {ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, string> ParseGlobalIni(string content)
    {
        Dictionary<string, string> dictionary = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(content))
        {
            return dictionary;
        }

        using StringReader reader = new(content);

        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();

            // Skip empty and comments
            if (string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("--", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith('#'))
            {
                continue;
            }

            int equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            string key = trimmed[..equalsIndex].Trim();
            string value = trimmed[(equalsIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            // Add "@" prefix for ui_ keys to match defaultProfile.xml format
            if (key.StartsWith("ui_", StringComparison.OrdinalIgnoreCase))
            {
                key = "@" + key;
            }

            dictionary[key] = value;
        }

        return dictionary;
    }

    private static string ParseLanguageFromLines(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("--", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!trimmed.StartsWith(SCConstants.Localization.LanguageConfigKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            string value = trimmed[(equalsIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string normalized = value.ToUpperInvariant();
            if (s_supportedLanguages.Contains(normalized))
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"[Localization] Detected language: {normalized}");
                return normalized;
            }

            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[Localization] Invalid language '{value}', using {SCConstants.Localization.DefaultLanguage}");
            return SCConstants.Localization.DefaultLanguage;
        }

        return SCConstants.Localization.DefaultLanguage;
    }
}
