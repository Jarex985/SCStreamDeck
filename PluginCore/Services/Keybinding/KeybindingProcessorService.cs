using System.Text;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for processing Star Citizen keybindings.
/// </summary>
public sealed class KeybindingProcessorService(
    IP4KArchiveService p4KService,
    ICryXmlParserService cryXmlParser,
    ILocalizationService localizationService,
    IKeybindingXmlParserService xmlParser,
    IKeybindingMetadataService metadataService,
    IKeybindingOutputService outputService,
    IFileSystem fileSystem)
{
    private readonly ICryXmlParserService _cryXmlParser = cryXmlParser ?? throw new ArgumentNullException(nameof(cryXmlParser));

    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly ILocalizationService _localizationService =
        localizationService ?? throw new ArgumentNullException(nameof(localizationService));

    private readonly IKeybindingMetadataService _metadataService =
        metadataService ?? throw new ArgumentNullException(nameof(metadataService));

    private readonly IKeybindingOutputService _outputService =
        outputService ?? throw new ArgumentNullException(nameof(outputService));

    private readonly IP4KArchiveService _p4KService = p4KService ?? throw new ArgumentNullException(nameof(p4KService));
    private readonly IKeybindingXmlParserService _xmlParser = xmlParser ?? throw new ArgumentNullException(nameof(xmlParser));

    public async Task<KeybindingProcessResult> ProcessKeybindingsAsync(
        SCInstallCandidate installation,
        string? actionMapsPath,
        string outputJsonPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputJsonPath);

        try
        {
            byte[]? xmlBytes = await ExtractDefaultProfileAsync(installation.DataP4KPath, cancellationToken)
                .ConfigureAwait(false);
            if (xmlBytes == null)
            {
                return KeybindingProcessResult.Failure("Failed to extract defaultProfile.xml from P4K");
            }

            CryXmlConversionResult xmlResult = await ParseCryXmlAsync(xmlBytes, cancellationToken).ConfigureAwait(false);
            if (!xmlResult.IsSuccess || string.IsNullOrWhiteSpace(xmlResult.Xml))
            {
                string reason = string.IsNullOrWhiteSpace(xmlResult.ErrorMessage)
                    ? "Failed to parse CryXml binary data"
                    : $"Failed to parse CryXml binary data: {xmlResult.ErrorMessage}";

                return KeybindingProcessResult.Failure(reason);
            }

            Dictionary<string, ActivationModeMetadata> activationModes = _xmlParser.ParseActivationModes(xmlResult.Xml);
            List<KeybindingActionData> actions = _xmlParser.ParseXmlToActions(xmlResult.Xml);
            if (actions.Count == 0)
            {
                return KeybindingProcessResult.Failure("No actions found in defaultProfile.xml");
            }

            string detectedLanguage = _metadataService.DetectLanguage(installation.ChannelPath);
            await ApplyLocalizationAsync(actions, installation, detectedLanguage, cancellationToken)
                .ConfigureAwait(false);

            ApplyOverridesIfPresent(actions, actionMapsPath);

            List<KeybindingActionData> filteredActions = FilterActionsWithBindings(actions);

            await _outputService.WriteKeybindingsJsonAsync(
                installation,
                actionMapsPath,
                detectedLanguage,
                outputJsonPath,
                filteredActions,
                activationModes,
                cancellationToken).ConfigureAwait(false);

            return KeybindingProcessResult.Success(detectedLanguage);
        }

        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(KeybindingProcessorService)}] Failed to process keybindings", ex);
            return KeybindingProcessResult.Failure("Failed to process keybindings. See logs for details.");
        }
    }


    public bool NeedsRegeneration(string jsonPath, SCInstallCandidate installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return _metadataService.NeedsRegeneration(jsonPath, installation);
    }

    #region Pipeline Steps

    private async Task<byte[]?> ExtractDefaultProfileAsync(string dataP4KPath, CancellationToken cancellationToken)
    {
        bool opened = false;

        try
        {
            if (!SecurePathValidator.TryNormalizePath(dataP4KPath, out string normalizedPath))
            {
                Log.Err($"[{nameof(KeybindingProcessorService)}] Invalid path '{dataP4KPath}'");
                return null;
            }

            opened = await _p4KService.OpenArchiveAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
            if (!opened)
            {
                return null;
            }

            IReadOnlyList<P4KFileEntry> entries = await _p4KService.ScanDirectoryAsync(
                SCConstants.Paths.KeybindingConfigDirectory,
                SCConstants.Files.DefaultProfileFileName,
                cancellationToken).ConfigureAwait(false);

            if (entries.Count == 0)
            {
                Log.Err($"[{nameof(KeybindingProcessorService)}] Default profile XML not found in P4K archive");
                return null;
            }

            P4KFileEntry profileEntry = entries[0];
            byte[]? bytes = await _p4KService.ReadFileAsync(profileEntry, cancellationToken).ConfigureAwait(false);

            return bytes == null || bytes.Length == 0
                ? null
                : bytes;
        }

        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(KeybindingProcessorService)}] Failed to extract default profile", ex);
            return null;
        }

        finally
        {
            if (opened)
            {
                _p4KService.CloseArchive();
            }
        }
    }

    private async Task<CryXmlConversionResult> ParseCryXmlAsync(byte[] xmlBytes, CancellationToken cancellationToken)
    {
        if (IsPlainXml(xmlBytes))
        {
            return CryXmlConversionResult.Success(Encoding.UTF8.GetString(xmlBytes));
        }

        return await _cryXmlParser.ConvertCryXmlToTextAsync(xmlBytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyLocalizationAsync(
        List<KeybindingActionData> actions,
        SCInstallCandidate installation,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyDictionary<string, string> localization = await _localizationService.LoadGlobalIniAsync(
                installation.ChannelPath,
                language,
                installation.DataP4KPath,
                cancellationToken).ConfigureAwait(false);

            if (localization.Count == 0)
            {
                Log.Warn($"[{nameof(KeybindingProcessorService)}] No localization data loaded, using default labels");
                return;
            }

            foreach (KeybindingActionData action in actions)
            {
                ApplyLocalization(localization, action);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(KeybindingProcessorService)}] Failed to load localization", ex);
        }
    }

    private static void ApplyLocalization(
        IReadOnlyDictionary<string, string> localization,
        KeybindingActionData action)
    {
        if (localization.TryGetValue(action.Label, out string? localizedLabel))
        {
            action.Label = localizedLabel;
        }

        if (!string.IsNullOrEmpty(action.Description) &&
            localization.TryGetValue(action.Description, out string? localizedDesc))
        {
            action.Description = localizedDesc;
        }

        if (!string.IsNullOrEmpty(action.MapLabel) &&
            localization.TryGetValue(action.MapLabel, out string? localizedMapLabel))
        {
            action.MapLabel = localizedMapLabel;
        }

        if (!string.IsNullOrEmpty(action.Category) &&
            localization.TryGetValue(action.Category, out string? localizedCategory))
        {
            action.Category = localizedCategory;
        }
    }

    private void ApplyOverridesIfPresent(List<KeybindingActionData> actions, string? actionMapsPath)
    {
        if (string.IsNullOrWhiteSpace(actionMapsPath) || !_fileSystem.FileExists(actionMapsPath))
        {
            return;
        }

        try
        {
            UserOverrides? overrides = UserOverrideParser.Parse(actionMapsPath);

            if (overrides is not { HasOverrides: true })
            {
                Log.Warn($"[{nameof(KeybindingProcessorService)}] No user overrides found or file not accessible");

                return;
            }

            UserOverrideParser.ApplyOverrides(actions, overrides);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Err($"[{nameof(KeybindingProcessorService)}] Failed to apply user overrides", ex);
        }
    }

    private static bool IsPlainXml(byte[] xmlBytes) =>
        xmlBytes[0] == (byte)'<' || xmlBytes.Take(Math.Min(64, xmlBytes.Length)).Any(b => b == (byte)'<');


    private static List<KeybindingActionData> FilterActionsWithBindings(List<KeybindingActionData> actions) =>
        actions.Where(a => HasBindingsOrValidLabel(a) && !a.Bindings.Keyboard.IsModifierOnly()).ToList();

    private static bool HasBindingsOrValidLabel(KeybindingActionData action)
    {
        bool hasBinding = !string.IsNullOrWhiteSpace(action.Bindings.Keyboard) ||
                          !string.IsNullOrWhiteSpace(action.Bindings.Mouse) ||
                          !string.IsNullOrWhiteSpace(action.Bindings.Joystick) ||
                          !string.IsNullOrWhiteSpace(action.Bindings.Gamepad);

        bool isValidAction = !string.IsNullOrWhiteSpace(action.Label) &&
                             !string.IsNullOrWhiteSpace(action.Category);

        return hasBinding || isValidAction;
    }

    #endregion
}

/// <summary>
///     Result of keybinding processing.
/// </summary>
public sealed class KeybindingProcessResult
{
    public bool IsSuccess { get; private init; }
    public string? DetectedLanguage { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static KeybindingProcessResult Success(string detectedLanguage) =>
        new() { IsSuccess = true, DetectedLanguage = detectedLanguage };

    public static KeybindingProcessResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
