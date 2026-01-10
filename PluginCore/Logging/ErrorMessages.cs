namespace SCStreamDeck.SCCore.Logging;

/// <summary>
///     Centralized error messages.
/// </summary>
internal static class ErrorMessages
{
    // P4K Archive Service errors
    public const string P4KArchiveOpenFailed = "Failed to open P4K archive";
    public const string P4KArchiveNotFound = "P4K archive not found";
    public const string P4KScanFailed = "Failed to scan P4K directory";
    public const string P4KReadFailed = "Failed to read P4K file";
    public const string P4KEncryptionKey = "Failed to set encryption key";
    public const string P4KDecodeTextFailed = "Failed to decode P4K file as text";
    public const string P4KEntryNotFound = "P4K entry not found";

    // Initialization/Channel/Installation Service errors
    public const string InitializationFailed = "Initialization failed";
    
    public const string InstallDetectionFailed = "Failed to detect Star Citizen installations";
    public const string ChannelSwitchFailed = "Failed to switch channel";
    
    // Keybinding Processing errors
    public const string KeybindingProcessingFailed = "Failed to process keybindings";
    
    public const string LanguageDetectionFailed = "Failed to detect language:";
    public const string LocalizationLoadFailed = "Failed to load localization:";
    public const string UserOverrideApplyFailed = "Failed to apply user overrides:";

    // RSI Launcher Config Reader errors
    public const string RsiLauncherDirNotFound = "RSI Launcher directory not found";
    public const string RsiLauncherLogsNotFound = "RSI Launcher logs directory not found";

    // Generic File/Directory Operation errors
    public const string FileReadFailed = "Failed to read file";
    public const string FileAccessDenied = "Access denied";

    // JSON Operation errors
    public const string JsonMetadataCheckFailed = "Failed to check JSON metadata:";
    public const string JsonParseFailed = "Failed to parse JSON";
    public const string JsonSerializationFailed = "Failed to serialize to JSON";

    // XML Operation errors
    public const string XmlParseFailed = "Failed to parse XML:";
    public const string XmlExtractFailed = "Failed to extract XML:";
    public const string XmlFormatInvalid = "Invalid XML format";

    // Validation errors
    public const string InvalidPath = "Invalid path or path format:";
    public const string ArgumentInvalid = "Invalid argument";

    // State Management errors
    public const string StateLoadFailed = "Failed to load state";
    public const string StateSaveFailed = "Failed to save state";
    public const string StateInvalidateFailed = "Failed to invalidate state:";
    public const string KeybindingsDeleteFailed = "Failed to delete keybindings";
    public const string EventHandlerFailed = "Event handler failed";

    // Generic operation errors
    public const string OperationFailedFor = "Operation failed for:";
}