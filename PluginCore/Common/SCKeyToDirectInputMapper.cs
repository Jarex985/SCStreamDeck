using WindowsInput.Native;

namespace SCStreamDeck.Common;

/// <summary>
///     Maps Star Citizen key names to DirectInput keyboard scan codes (layout-independent).
///     DirectInput codes represent physical key positions and are used as the single source of truth
///     for keyboard input processing.
/// </summary>
internal static class SCKeyToDirectInputMapper
{
    private static readonly Dictionary<string, DirectInputKeyCode> s_scToDik = new(StringComparer.OrdinalIgnoreCase)
    {
        // Letters (QWERTY physical positions)
        ["a"] = DirectInputKeyCode.DikA,
        ["b"] = DirectInputKeyCode.DikB,
        ["c"] = DirectInputKeyCode.DikC,
        ["d"] = DirectInputKeyCode.DikD,
        ["e"] = DirectInputKeyCode.DikE,
        ["f"] = DirectInputKeyCode.DikF,
        ["g"] = DirectInputKeyCode.DikG,
        ["h"] = DirectInputKeyCode.DikH,
        ["i"] = DirectInputKeyCode.DikI,
        ["j"] = DirectInputKeyCode.DikJ,
        ["k"] = DirectInputKeyCode.DikK,
        ["l"] = DirectInputKeyCode.DikL,
        ["m"] = DirectInputKeyCode.DikM,
        ["n"] = DirectInputKeyCode.DikN,
        ["o"] = DirectInputKeyCode.DikO,
        ["p"] = DirectInputKeyCode.DikP,
        ["q"] = DirectInputKeyCode.DikQ,
        ["r"] = DirectInputKeyCode.DikR,
        ["s"] = DirectInputKeyCode.DikS,
        ["t"] = DirectInputKeyCode.DikT,
        ["u"] = DirectInputKeyCode.DikU,
        ["v"] = DirectInputKeyCode.DikV,
        ["w"] = DirectInputKeyCode.DikW,
        ["x"] = DirectInputKeyCode.DikX,
        ["y"] = DirectInputKeyCode.DikY,
        ["z"] = DirectInputKeyCode.DikZ,

        // Numbers
        ["0"] = DirectInputKeyCode.Dik0,
        ["1"] = DirectInputKeyCode.Dik1,
        ["2"] = DirectInputKeyCode.Dik2,
        ["3"] = DirectInputKeyCode.Dik3,
        ["4"] = DirectInputKeyCode.Dik4,
        ["5"] = DirectInputKeyCode.Dik5,
        ["6"] = DirectInputKeyCode.Dik6,
        ["7"] = DirectInputKeyCode.Dik7,
        ["8"] = DirectInputKeyCode.Dik8,
        ["9"] = DirectInputKeyCode.Dik9,

        // Punctuation
        ["minus"] = DirectInputKeyCode.DikMinus,
        ["equals"] = DirectInputKeyCode.DikEquals,
        ["lbracket"] = DirectInputKeyCode.DikLbracket,
        ["rbracket"] = DirectInputKeyCode.DikRbracket,
        ["backslash"] = DirectInputKeyCode.DikBackslash,
        ["semicolon"] = DirectInputKeyCode.DikSemicolon,
        ["apostrophe"] = DirectInputKeyCode.DikApostrophe,
        ["grave"] = DirectInputKeyCode.DikGrave,
        ["comma"] = DirectInputKeyCode.DikComma,
        ["period"] = DirectInputKeyCode.DikPeriod,
        ["slash"] = DirectInputKeyCode.DikSlash,

        // Function keys
        ["f1"] = DirectInputKeyCode.DikF1,
        ["f2"] = DirectInputKeyCode.DikF2,
        ["f3"] = DirectInputKeyCode.DikF3,
        ["f4"] = DirectInputKeyCode.DikF4,
        ["f5"] = DirectInputKeyCode.DikF5,
        ["f6"] = DirectInputKeyCode.DikF6,
        ["f7"] = DirectInputKeyCode.DikF7,
        ["f8"] = DirectInputKeyCode.DikF8,
        ["f9"] = DirectInputKeyCode.DikF9,
        ["f10"] = DirectInputKeyCode.DikF10,
        ["f11"] = DirectInputKeyCode.DikF11,
        ["f12"] = DirectInputKeyCode.DikF12,

        // Special keys
        ["escape"] = DirectInputKeyCode.DikEscape,
        ["space"] = DirectInputKeyCode.DikSpace,
        ["enter"] = DirectInputKeyCode.DikReturn,
        ["tab"] = DirectInputKeyCode.DikTab,
        ["backspace"] = DirectInputKeyCode.DikBackspace,
        ["capslock"] = DirectInputKeyCode.DikCapital,
        ["numlock"] = DirectInputKeyCode.DikNumlock,
        ["scrolllock"] = DirectInputKeyCode.DikScroll,

        // Modifiers
        ["lshift"] = DirectInputKeyCode.DikLshift,
        ["rshift"] = DirectInputKeyCode.DikRshift,
        ["lctrl"] = DirectInputKeyCode.DikLcontrol,
        ["rctrl"] = DirectInputKeyCode.DikRcontrol,
        ["lalt"] = DirectInputKeyCode.DikLalt,
        ["ralt"] = DirectInputKeyCode.DikRalt,

        // Navigation
        ["up"] = DirectInputKeyCode.DikUp,
        ["down"] = DirectInputKeyCode.DikDown,
        ["left"] = DirectInputKeyCode.DikLeft,
        ["right"] = DirectInputKeyCode.DikRight,
        ["home"] = DirectInputKeyCode.DikHome,
        ["end"] = DirectInputKeyCode.DikEnd,
        ["pgup"] = DirectInputKeyCode.DikPageUp,
        ["pgdn"] = DirectInputKeyCode.DikPageDown,
        ["insert"] = DirectInputKeyCode.DikInsert,
        ["delete"] = DirectInputKeyCode.DikDelete,

        // Numpad numbers
        ["np_0"] = DirectInputKeyCode.DikNumpad0,
        ["np_1"] = DirectInputKeyCode.DikNumpad1,
        ["np_2"] = DirectInputKeyCode.DikNumpad2,
        ["np_3"] = DirectInputKeyCode.DikNumpad3,
        ["np_4"] = DirectInputKeyCode.DikNumpad4,
        ["np_5"] = DirectInputKeyCode.DikNumpad5,
        ["np_6"] = DirectInputKeyCode.DikNumpad6,
        ["np_7"] = DirectInputKeyCode.DikNumpad7,
        ["np_8"] = DirectInputKeyCode.DikNumpad8,
        ["np_9"] = DirectInputKeyCode.DikNumpad9,

        // Numpad operators
        ["np_multiply"] = DirectInputKeyCode.DikMultiply,
        ["np_add"] = DirectInputKeyCode.DikAdd,
        ["np_subtract"] = DirectInputKeyCode.DikSubtract,
        ["np_divide"] = DirectInputKeyCode.DikDivide,
        ["np_period"] = DirectInputKeyCode.DikDecimal,
        ["np_enter"] = DirectInputKeyCode.DikNumpadenter
    };

    /// <summary>
    ///     Attempts to get the DirectInput key code for a Star Citizen key name.
    /// </summary>
    /// <param name="scKey">SC key name (e.g. "y", "z", "f1", "apostrophe")</param>
    /// <param name="dik">The DirectInput key code representing the physical key position</param>
    /// <returns>True if mapping found, false otherwise</returns>
    public static bool TryGetDirectInputKeyCode(string scKey, out DirectInputKeyCode dik)
    {
        if (string.IsNullOrWhiteSpace(scKey))
        {
            dik = default;
            return false;
        }

        return s_scToDik.TryGetValue(Normalize(scKey), out dik);
    }

    private static string Normalize(string scKey) => scKey.Trim().ToLowerInvariant();
}
