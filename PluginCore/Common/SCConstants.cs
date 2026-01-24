using SCStreamDeck.Models;

namespace SCStreamDeck.Common;

public static class SCConstants
{
    /// <summary>
    ///     AES encryption key for Star Citizen P4K archives.
    /// </summary>
    public static readonly byte[] EncryptionKey =
    [
        0x5E, 0x7A, 0x20, 0x02, 0x30, 0x2E, 0xEB, 0x1A, 0x3B, 0xB6, 0x17, 0xC3, 0x0F, 0xDE, 0x1E, 0x47, 0x8D, 0x92, 0x3D,
        0x1B, 0xB3, 0xA7, 0x49, 0x8F, 0x4F, 0x1C, 0x82, 0x2C, 0x4E, 0xDA, 0x0A, 0x4C
    ];

    #region Input Pattern Arrays

    internal static readonly string[] s_mouseButtonPatterns =
    [
        Input.Mouse.Button1,
        Input.Mouse.Button2,
        Input.Mouse.Button3,
        Input.Mouse.Button4,
        Input.Mouse.Button5,
        Input.Mouse.LeftButton,
        Input.Mouse.RightButton,
        Input.Mouse.MiddleButton
    ];

    #endregion

    #region Path Constants

    public static class Paths
    {
        /// <summary>
        ///     Data prefix used in P4K entry paths.
        /// </summary>
        public const string DataPrefix = "Data/";

        /// <summary>
        ///     Directory path for keybinding configuration files within P4K archive.
        /// </summary>
        public const string KeybindingConfigDirectory = "Data/Libs/Config";

        /// <summary>
        ///     Directory path for localization files within P4K archive.
        /// </summary>
        public const string LocalizationBaseDirectory = "Data/Localization";

        public const string RsiFolderName = "Roberts Space Industries";
        public const string SCFolderName = "StarCitizen";
        public const string UserProfilePathPattern = "user/client/0/Profiles/default";
    }

    #endregion

    #region File Name Constants

    public static class Files
    {
        public const string DefaultProfileFileName = "defaultProfile.xml";
        public const string GlobalIniFileName = "global.ini";
        public const string UserConfigFileName = "user.cfg";
        public const string ActionMapsFileName = "actionmaps.xml";
        public const string DataP4KFileName = "Data.p4k";
    }

    #endregion


    #region Input Constants

    public static class Input
    {
        public static class Keyboard
        {
            // Modifiers
            public const string LAlt = "LALT";
            public const string RAlt = "RALT";
            public const string LShift = "LSHIFT";
            public const string RShift = "RSHIFT";
            public const string LCtrl = "LCTRL";
            public const string RCtrl = "RCTRL";

            // Special keys
            public const string Escape = "ESCAPE";
            public const string Space = "SPACE";
            public const string Enter = "ENTER";
            public const string Tab = "TAB";
            public const string Backspace = "BACKSPACE";
            public const string CapsLock = "CAPSLOCK";
            public const string NumLock = "NUMLOCK";
            public const string ScrollLock = "SCROLLLOCK";

            // Navigation
            public const string Up = "UP";
            public const string Down = "DOWN";
            public const string Left = "LEFT";
            public const string Right = "RIGHT";
            public const string Home = "HOME";
            public const string End = "END";
            public const string PgUp = "PGUP";
            public const string PgDown = "PGDOWN";
            public const string Insert = "INSERT";
            public const string Delete = "DELETE";

            // Function keys
            public const string F1 = "F1";
            public const string F2 = "F2";
            public const string F3 = "F3";
            public const string F4 = "F4";
            public const string F5 = "F5";
            public const string F6 = "F6";
            public const string F7 = "F7";
            public const string F8 = "F8";
            public const string F9 = "F9";
            public const string F10 = "F10";
            public const string F11 = "F11";
            public const string F12 = "F12";

            // Other
            public const string HmdPrefix = "HMD_";
        }

        public static class Mouse
        {
            public const string WheelPrefix = "MWHEEL";
            public const string WheelUp = "MWHEEL_UP";
            public const string WheelDown = "MWHEEL_DOWN";

            // Mouse Buttons
            public const string Button1 = "MOUSE1";
            public const string Button2 = "MOUSE2";
            public const string Button3 = "MOUSE3";
            public const string Button4 = "MOUSE4";
            public const string Button5 = "MOUSE5";

            public const string LeftButton = "LMB";
            public const string RightButton = "RMB";
            public const string MiddleButton = "MMB";

            // TODO: Compound mouse button support (e.g., MOUSE1_2 for simultaneous left+right)
            //  Current limitation: Only single mouse buttons are supported
            //  Example binding: melee_block uses "mouse1_2" - currently parses as LEFT button only
        }

        public static class MouseAxis
        {
            public const string Prefix = "MAXIS_";
        }
    }

    #endregion

    /// <summary>
    ///     Constants for localization handling.
    /// </summary>
    public static class Localization
    {
        public const string DefaultLanguage = "english";

        /// <summary>
        ///     Configuration key for language setting in user.cfg
        /// </summary>
        public const string LanguageConfigKey = "g_language";

        /// <summary>
        ///     Subdirectory name for localization overrides within channel data folder.
        /// </summary>
        public const string LocalizationSubdirectory = "Localization";

        /// <summary>
        ///     Comment prefixes supported in INI format (Star Citizen global.ini).
        ///     Note: Comment support is uncertain but maintained for robustness.
        /// </summary>
        public static readonly string[] IniCommentPrefixes = ["--", "//", "#"];
    }

    /// <summary>
    ///     Constants for CryXml parsing.
    /// </summary>
    public static class CryXml
    {
        public const string CryXmlSignature = "CryXmlB";
        public const int HeaderSize = 44;
        public const int NodeStructureSize = 28;
        public const int AttributeEntrySize = 8;
        public const int ChildIndexSize = 4;
    }
}

/// <summary>
///     Extension methods for input string validation and classification.
/// </summary>
public static class InputStringExtensions
{
    /// <summary>
    ///     Determines if the input string is a keyboard modifier key only.
    /// </summary>
    public static bool IsModifierOnly(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string upper = input.ToUpperInvariant();
        return upper.Equals(SCConstants.Input.Keyboard.LAlt, StringComparison.Ordinal) ||
               upper.Equals(SCConstants.Input.Keyboard.RAlt, StringComparison.Ordinal) ||
               upper.Equals(SCConstants.Input.Keyboard.LShift, StringComparison.Ordinal) ||
               upper.Equals(SCConstants.Input.Keyboard.RShift, StringComparison.Ordinal) ||
               upper.Equals(SCConstants.Input.Keyboard.LCtrl, StringComparison.Ordinal) ||
               upper.Equals(SCConstants.Input.Keyboard.RCtrl, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Determines if the input string is a mouse wheel binding.
    /// </summary>
    public static bool IsMouseWheel(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Don't treat mouse wheel bindings with modifiers as pure mouse wheel
        if (input.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        string upper = input.ToUpperInvariant();
        return upper.Contains(SCConstants.Input.Mouse.WheelPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Determines input string is a mouse button (mouse1, mouse2, etc.).
    /// </summary>
    public static bool IsMouseButton(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Don't treat mouse buttons with modifiers as pure mouse buttons
        if (input.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        string upper = input.ToUpperInvariant();
        return IsMouseButtonPattern(upper);
    }

    /// <summary>
    ///     Determines the input type from a binding string.
    /// </summary>
    public static InputType GetInputType(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return InputType.Unknown;
        }

        string normalized = input.ToUpperInvariant();

        // Check mouse buttons (exact match for LMB/RMB/MMB, contains for MOUSE1-5)
        if (IsMouseButtonPattern(normalized))
        {
            return InputType.MouseButton;
        }

        // Check mouse wheel (discrete events: mwheel_up/down)
        if (normalized.Contains(SCConstants.Input.Mouse.WheelPrefix))
        {
            return InputType.MouseWheel;
        }

        // Check mouse axis
        if (normalized.Contains(SCConstants.Input.MouseAxis.Prefix))
        {
            return InputType.MouseAxis;
        }

        // Default to keyboard for any other input
        return InputType.Keyboard;
    }

    /// <summary>
    ///     Determines if the normalized input string matches any mouse button pattern.
    ///     LMB/RMB/MMB require exact match, MOUSE1-5 use contains (allows modifiers).
    /// </summary>
    private static bool IsMouseButtonPattern(string normalized) =>
        SCConstants.s_mouseButtonPatterns.Any(p =>
            p is SCConstants.Input.Mouse.LeftButton or
                SCConstants.Input.Mouse.RightButton or
                SCConstants.Input.Mouse.MiddleButton
                ? normalized == p
                : normalized.Contains(p));
}
