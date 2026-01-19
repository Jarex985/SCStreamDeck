namespace SCStreamDeck.Logging;

/// <summary>
///     Centralized error messages.
/// </summary>
internal static class ErrorMessages
{
    // P4K Archive Service errors
    public const string P4KEncryptionKey = "Failed to set encryption key";
    public const string P4KDecodeTextFailed = "Failed to decode P4K file as text";

    // Initialization/Channel/Installation Service errors
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

    // Validation errors
    public const string InvalidPath = "Invalid path or path format:";
    public const string ArgumentInvalid = "Invalid argument";

    // State Management errors
    public const string KeybindingsDeleteFailed = "Failed to delete keybindings";

    // Generic operation errors
    public const string OperationFailedFor = "Operation failed for:";
}
