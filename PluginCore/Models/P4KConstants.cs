namespace SCStreamDeck.SCCore.Models;

/// <summary>
///     Constants for P4K archive handling.
/// </summary>
internal static class P4KConstants
{
    /// <summary>
    ///     Data prefix used in P4K entry paths.
    /// </summary>
    public const string DataPrefix = "Data/";

    // ===== Keybinding Paths =====

    /// <summary>
    ///     Directory path for keybinding configuration files within P4K archive.
    /// </summary>
    public const string KeybindingConfigDirectory = "Data/Libs/Config";

    /// <summary>
    ///     Default keybinding profile filename.
    /// </summary>
    public const string DefaultProfileFileName = "defaultProfile.xml";

    // ===== Localization Paths =====

    /// <summary>
    ///     Base directory for localization files within P4K archive.
    /// </summary>
    public const string LocalizationBaseDirectory = "Data/Localization";

    /// <summary>
    ///     Global localization INI filename.
    /// </summary>
    public const string GlobalIniFileName = "global.ini";

    // ===== User Configuration =====

    /// <summary>
    ///     User configuration filename (stored in game channel directory).
    /// </summary>
    public const string UserConfigFileName = "user.cfg";

    /// <summary>
    ///     Custom actionmaps override filename.
    /// </summary>
    public const string ActionMapsFileName = "actionmaps.xml";

    /// <summary>
    ///     AES encryption key for Star Citizen P4K archives.
    ///     This is publicly known and required for reading encrypted P4K files.
    /// </summary>
    public static readonly byte[] EncryptionKey =
    {
        0x5E, 0x7A, 0x20, 0x02, 0x30, 0x2E, 0xEB, 0x1A, 0x3B, 0xB6, 0x17, 0xC3, 0x0F, 0xDE, 0x1E, 0x47, 0x8D, 0x92, 0x3D,
        0x1B, 0xB3, 0xA7, 0x49, 0x8F, 0x4F, 0x1C, 0x82, 0x2C, 0x4E, 0xDA, 0x0A, 0x4C
    };
}
