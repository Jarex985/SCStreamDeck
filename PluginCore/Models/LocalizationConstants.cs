namespace SCStreamDeck.SCCore.Models;

/// <summary>
///     Constants for localization handling.
/// </summary>
internal static class LocalizationConstants
{
    /// <summary>
    ///     Default fallback language.
    /// </summary>
    public const string DefaultLanguage = "english";

    /// <summary>
    ///     Configuration key for language setting in user.cfg
    /// </summary>
    public const string LanguageConfigKey = "g_language";

    /// <summary>
    ///     Subdirectory name for localization overrides within channel data folder.
    /// </summary>
    public const string LocalizationSubdirectory = "Localization";
}
