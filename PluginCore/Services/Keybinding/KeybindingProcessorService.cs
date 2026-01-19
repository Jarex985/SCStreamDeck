using System.Text;
using BarRaider.SdTools;
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
    IKeybindingOutputService outputService)
    : IKeybindingProcessorService
{
    private readonly ICryXmlParserService _cryXmlParser = cryXmlParser ?? throw new ArgumentNullException(nameof(cryXmlParser));

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

            string? xmlText = await ParseCryXmlAsync(xmlBytes, cancellationToken).ConfigureAwait(false);
            if (xmlText == null)
            {
                return KeybindingProcessResult.Failure("Failed to parse CryXml binary data");
            }

            Dictionary<string, ActivationModeMetadata> activationModes = _xmlParser.ParseActivationModes(xmlText);
            List<KeybindingActionData> actions = _xmlParser.ParseXmlToActions(xmlText);
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

        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingProcessorService)}] {ErrorMessages.KeybindingProcessingFailed} {ex.Message}");
            return KeybindingProcessResult.Failure(ex.Message);
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
        try
        {
            if (!SecurePathValidator.TryNormalizePath(dataP4KPath, out string normalizedPath))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingProcessorService)}] {ErrorMessages.InvalidPath} {dataP4KPath}");
                return null;
            }

            await _p4KService.OpenArchiveAsync(normalizedPath, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<P4KFileEntry> entries = await _p4KService.ScanDirectoryAsync(
                SCConstants.Paths.KeybindingConfigDirectory,
                SCConstants.Files.DefaultProfileFileName,
                cancellationToken).ConfigureAwait(false);

            if (entries.Count == 0)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingProcessorService)}] Default profile XML not found in P4K archive");
                return null;
            }

            P4KFileEntry profileEntry = entries[0];
            byte[]? bytes = await _p4KService.ReadFileAsync(profileEntry, cancellationToken).ConfigureAwait(false);

            return bytes == null || bytes.Length == 0
                ? null
                : bytes;
        }

        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingProcessorService)}] Failed to extract default profile: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> ParseCryXmlAsync(byte[] xmlBytes, CancellationToken cancellationToken)
    {
        try
        {
            if (IsPlainXml(xmlBytes))
            {
                return Encoding.UTF8.GetString(xmlBytes);
            }

            string? xmlText = await _cryXmlParser.ConvertCryXmlToTextAsync(xmlBytes, cancellationToken)
                .ConfigureAwait(false);
            return xmlText;
        }

        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingProcessorService)}] Failed to parse XML: {ex.Message}");
            return null;
        }
    }

    private async Task ApplyLocalizationAsync(
        List<KeybindingActionData> actions,
        SCInstallCandidate installation,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyDictionary<string, string>? localization = await _localizationService.LoadGlobalIniAsync(
                installation.ChannelPath,
                language,
                installation.DataP4KPath,
                cancellationToken).ConfigureAwait(false);

            if (localization == null || localization.Count == 0)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN,
                    $"[{nameof(KeybindingProcessorService)}] No localization data loaded, using default labels");
                return;
            }

            foreach (KeybindingActionData action in actions)
            {
                ApplyLocalization(localization, action);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingProcessorService)}] {ErrorMessages.LocalizationLoadFailed} {ex.Message}");
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
        if (string.IsNullOrWhiteSpace(actionMapsPath) || !File.Exists(actionMapsPath))
        {
            return;
        }

        try
        {
            UserOverrides? overrides = UserOverrideParser.Parse(actionMapsPath);

            if (overrides is not { HasOverrides: true })
            {
                Logger.Instance.LogMessage(TracingLevel.WARN,
                    $"[{nameof(KeybindingProcessorService)}] No user overrides found or file not accessible");

                return;
            }

            UserOverrideParser.ApplyOverrides(actions, overrides);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingProcessorService)}] {ErrorMessages.UserOverrideApplyFailed} {ex.Message}");
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
