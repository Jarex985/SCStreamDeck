using System.Collections.Immutable;
using BarRaider.SdTools;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Models;
using SCStreamDeck.SCCore.Services.Data;

namespace SCStreamDeck.SCCore.Services.Core;

/// <summary>
///     Provides localization services for Star Citizen UI strings.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly ImmutableHashSet<string> SupportedLanguages = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "chinese_(simplified)", "chinese_(traditional)", "english", "french_(france)",
        "german_(germany)", "italian_(italy)", "japanese_(japan)", "korean_(south_korea)",
        "polish_(poland)", "portuguese_(brazil)", "spanish_(latin_america)", "spanish_(spain)");

    private readonly Dictionary<(string channelPath, string language), Dictionary<string, string>> _cache = new();
    private readonly object _cacheLock = new();

    private readonly IP4KArchiveService _p4kService;

    public LocalizationService(IP4KArchiveService p4kService)
    {
        _p4kService = p4kService ?? throw new ArgumentNullException(nameof(p4kService));
    }

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
        var cacheKey = (channelPath, language);

        // Check cache
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;
        }


        // Load from sources
        var content = await TryLoadContentAsync(channelPath, language, dataP4kPath, cancellationToken)
            .ConfigureAwait(false);

        if (content != null)
        {
            var parsed = ParseGlobalIni(content);
            lock (_cacheLock)
            {
                _cache[cacheKey] = parsed;
            }

            return parsed;
        }

        // Fallback to English
        if (!language.Equals(LocalizationConstants.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[Localization] Language '{language}' not found, falling back to {LocalizationConstants.DefaultLanguage}");
            return await LoadGlobalIniAsync(channelPath, LocalizationConstants.DefaultLanguage, dataP4kPath,
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
        var userConfigPath = Path.Combine(channelPath, P4KConstants.UserConfigFileName);

        if (!SecurePathValidator.TryNormalizePath(userConfigPath, out var validPath))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[Localization] Invalid {P4KConstants.UserConfigFileName} path: {userConfigPath}");
            return LocalizationConstants.DefaultLanguage;
        }

        if (!File.Exists(validPath)) return LocalizationConstants.DefaultLanguage;

        try
        {
            var lines = await File.ReadAllLinesAsync(validPath, cancellationToken).ConfigureAwait(false);
            return ParseLanguageFromLines(lines);
        }

        catch (IOException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[Localization] Failed to read {P4KConstants.UserConfigFileName} (I/O error): {ex.Message}");
            return LocalizationConstants.DefaultLanguage;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[Localization] Failed to read {P4KConstants.UserConfigFileName} (access denied): {ex.Message}");
            return LocalizationConstants.DefaultLanguage;
        }
    }

    public void ClearCache(string channelPath, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var cacheKey = (channelPath, language.ToUpperInvariant());

        lock (_cacheLock)
        {
            if (_cache.Remove(cacheKey))
                Logger.Instance.LogMessage(TracingLevel.INFO, $"[Localization] Cleared cache for {language}");
        }
    }

    private static string NormalizeLanguage(string language)
    {
        language = language.Trim().ToUpperInvariant();
        if (!SupportedLanguages.Contains(language))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[Localization] Unsupported language '{language}', using {LocalizationConstants.DefaultLanguage}");
            return LocalizationConstants.DefaultLanguage;
        }

        return language;
    }

    private async Task<string?> TryLoadContentAsync(
        string channelPath,
        string language,
        string dataP4kPath,
        CancellationToken cancellationToken)
    {
        // Priority 1: Override folder
        var content = await LoadFromOverrideFolderAsync(channelPath, language, cancellationToken).ConfigureAwait(false);
        if (content != null) return content;

        // Priority 2: P4K
        return await LoadFromP4KAsync(dataP4kPath, language, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> LoadFromOverrideFolderAsync(
        string channelPath,
        string language,
        CancellationToken cancellationToken)
    {
        var overridePath = Path.Combine(channelPath, "data", LocalizationConstants.LocalizationSubdirectory, language,
            P4KConstants.GlobalIniFileName);

        if (!SecurePathValidator.TryNormalizePath(overridePath, out var validPath))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"[Localization] Invalid override path: {overridePath}");
            return null;
        }

        if (!File.Exists(validPath)) return null;

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
            var opened = await _p4kService.OpenArchiveAsync(dataP4kPath, cancellationToken).ConfigureAwait(false);
            if (!opened)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "[Localization] Failed to open Data.p4k");
                return null;
            }

            try
            {
                var directory = $"{P4KConstants.LocalizationBaseDirectory}/{language}";
                var entries = await _p4kService
                    .ScanDirectoryAsync(directory, P4KConstants.GlobalIniFileName, cancellationToken)
                    .ConfigureAwait(false);

                if (entries.Count == 0)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN,
                        $"[Localization] {P4KConstants.GlobalIniFileName} not found in P4K for {language}");
                    return null;
                }

                var content = await _p4kService.ReadFileAsTextAsync(entries[0], cancellationToken)
                    .ConfigureAwait(false);
                return content;
            }

            finally
            {
                _p4kService.CloseArchive();
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
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(content)) return dictionary;

        using var reader = new StringReader(content);

        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();

            // Skip empty and comments
            if (string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("--", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith('#'))
                continue;

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex <= 0) continue;

            var key = trimmed[..equalsIndex].Trim();
            var value = trimmed[(equalsIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(key)) continue;

            // Add "@" prefix for ui_ keys to match defaultProfile.xml format
            if (key.StartsWith("ui_", StringComparison.OrdinalIgnoreCase)) key = "@" + key;

            dictionary[key] = value;
        }

        return dictionary;
    }

    private static string ParseLanguageFromLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("--", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith('#'))
                continue;

            if (!trimmed.StartsWith(LocalizationConstants.LanguageConfigKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex <= 0) continue;

            var value = trimmed[(equalsIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(value)) continue;

            var normalized = value.ToUpperInvariant();
            if (SupportedLanguages.Contains(normalized))
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"[Localization] Detected language: {normalized}");
                return normalized;
            }

            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[Localization] Invalid language '{value}', using {LocalizationConstants.DefaultLanguage}");
            return LocalizationConstants.DefaultLanguage;
        }

        return LocalizationConstants.DefaultLanguage;
    }
}