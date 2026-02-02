using SCStreamDeck.Common;
using SCStreamDeck.Models;
using WindowsInput.Native;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for parsing keybinding strings into executable inputs.
/// </summary>
public static class KeybindingParserService
{
    private static readonly Dictionary<string, DirectInputKeyCode> s_modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        { SCConstants.Input.Keyboard.LAlt, DirectInputKeyCode.DikLalt },
        { SCConstants.Input.Keyboard.RAlt, DirectInputKeyCode.DikRalt },
        { SCConstants.Input.Keyboard.LShift, DirectInputKeyCode.DikLshift },
        { SCConstants.Input.Keyboard.RShift, DirectInputKeyCode.DikRshift },
        { SCConstants.Input.Keyboard.LCtrl, DirectInputKeyCode.DikLcontrol },
        { SCConstants.Input.Keyboard.RCtrl, DirectInputKeyCode.DikRcontrol }
    };

    private static readonly Dictionary<string, DirectInputKeyCode> s_specialKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        { SCConstants.Input.Keyboard.F1, DirectInputKeyCode.DikF1 },
        { SCConstants.Input.Keyboard.F2, DirectInputKeyCode.DikF2 },
        { SCConstants.Input.Keyboard.F3, DirectInputKeyCode.DikF3 },
        { SCConstants.Input.Keyboard.F4, DirectInputKeyCode.DikF4 },
        { SCConstants.Input.Keyboard.F5, DirectInputKeyCode.DikF5 },
        { SCConstants.Input.Keyboard.F6, DirectInputKeyCode.DikF6 },
        { SCConstants.Input.Keyboard.F7, DirectInputKeyCode.DikF7 },
        { SCConstants.Input.Keyboard.F8, DirectInputKeyCode.DikF8 },
        { SCConstants.Input.Keyboard.F9, DirectInputKeyCode.DikF9 },
        { SCConstants.Input.Keyboard.F10, DirectInputKeyCode.DikF10 },
        { SCConstants.Input.Keyboard.F11, DirectInputKeyCode.DikF11 },
        { SCConstants.Input.Keyboard.F12, DirectInputKeyCode.DikF12 },
        { SCConstants.Input.Keyboard.Space, DirectInputKeyCode.DikSpace },
        { SCConstants.Input.Keyboard.Enter, DirectInputKeyCode.DikReturn },
        { SCConstants.Input.Keyboard.Tab, DirectInputKeyCode.DikTab },
        { SCConstants.Input.Keyboard.Escape, DirectInputKeyCode.DikEscape },
        { SCConstants.Input.Keyboard.Backspace, DirectInputKeyCode.DikBackspace },
        { SCConstants.Input.Keyboard.CapsLock, DirectInputKeyCode.DikCapital },
        { SCConstants.Input.Keyboard.NumLock, DirectInputKeyCode.DikNumlock },
        { SCConstants.Input.Keyboard.ScrollLock, DirectInputKeyCode.DikScroll },
        { SCConstants.Input.Keyboard.Up, DirectInputKeyCode.DikUp },
        { SCConstants.Input.Keyboard.Down, DirectInputKeyCode.DikDown },
        { SCConstants.Input.Keyboard.Left, DirectInputKeyCode.DikLeft },
        { SCConstants.Input.Keyboard.Right, DirectInputKeyCode.DikRight },
        { SCConstants.Input.Keyboard.Home, DirectInputKeyCode.DikHome },
        { SCConstants.Input.Keyboard.End, DirectInputKeyCode.DikEnd },
        { SCConstants.Input.Keyboard.PgUp, DirectInputKeyCode.DikPageUp },
        { SCConstants.Input.Keyboard.PgDown, DirectInputKeyCode.DikPageDown },
        { SCConstants.Input.Keyboard.Insert, DirectInputKeyCode.DikInsert },
        { SCConstants.Input.Keyboard.Delete, DirectInputKeyCode.DikDelete }
    };


    public static ParsedInputResult? ParseBinding(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return null;
        }

        string normalized = binding.Trim().ToUpperInvariant();

        if (TryParseMouseWheel(normalized, out ParsedInputResult? mouseWheelResult))
        {
            return mouseWheelResult;
        }

        if (TryParseMouseButton(normalized, out VirtualKeyCode mouseButton))
        {
            return new ParsedInputResult(InputType.MouseButton, mouseButton);
        }

        if (TryParseKeyboard(normalized, out DirectInputKeyCode[] kbModifiers, out DirectInputKeyCode[] keys))
        {
            return new ParsedInputResult(InputType.Keyboard, (kbModifiers, keys));
        }

        return null;
    }

    private static bool TryParseMouseWheel(string normalized, out ParsedInputResult? result)
    {
        result = null;

        if (!normalized.Contains(SCConstants.Input.Mouse.WheelPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized.Contains('+', StringComparison.Ordinal))
        {
            if (!TryParseMouseWheelWithModifiers(normalized, out DirectInputKeyCode[] modifiers, out int direction))
            {
                return false;
            }

            result = new ParsedInputResult(InputType.MouseWheel, (modifiers, direction));
            return true;
        }

        if (normalized.Contains(SCConstants.Input.Mouse.WheelUp, StringComparison.Ordinal))
        {
            result = new ParsedInputResult(InputType.MouseWheel, 1);
            return true;
        }

        if (normalized.Contains(SCConstants.Input.Mouse.WheelDown, StringComparison.Ordinal))
        {
            result = new ParsedInputResult(InputType.MouseWheel, -1);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Parses mouse wheel with modifiers (e.g., "lalt+mwheel_up").
    ///     Returns tuple of (modifiers[], wheelDirection).
    /// </summary>
    private static bool TryParseMouseWheelWithModifiers(
        string binding,
        out DirectInputKeyCode[] modifiers,
        out int wheelDirection)
    {
        modifiers = [];
        wheelDirection = 0;

        if (!binding.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        if (!binding.Contains(SCConstants.Input.Mouse.WheelPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        List<DirectInputKeyCode> modifierList = [];

        foreach (string token in SplitTokens(binding))
        {
            if (TryParseModifier(token, out DirectInputKeyCode modifier))
            {
                modifierList.Add(modifier);
                continue;
            }

            if (token == SCConstants.Input.Mouse.WheelUp)
            {
                wheelDirection = 1;
                continue;
            }

            if (token == SCConstants.Input.Mouse.WheelDown)
            {
                wheelDirection = -1;
            }
        }

        if (modifierList.Count == 0 || wheelDirection == 0)
        {
            return false;
        }

        modifiers = modifierList.ToArray();
        return true;
    }

    private static bool TryParseMouseButton(string normalized, out VirtualKeyCode button)
    {
        button = VirtualKeyCode.LBUTTON;

        if (normalized.Contains(SCConstants.Input.Mouse.Button1) || normalized == SCConstants.Input.Mouse.LeftButton)
        {
            button = VirtualKeyCode.LBUTTON;
            return true;
        }

        if (normalized.Contains(SCConstants.Input.Mouse.Button2) || normalized == SCConstants.Input.Mouse.RightButton)
        {
            button = VirtualKeyCode.RBUTTON;
            return true;
        }

        if (normalized.Contains(SCConstants.Input.Mouse.Button3) || normalized == SCConstants.Input.Mouse.MiddleButton)
        {
            button = VirtualKeyCode.MBUTTON;
            return true;
        }

        if (normalized.Contains(SCConstants.Input.Mouse.Button4))
        {
            button = VirtualKeyCode.XBUTTON1;
            return true;
        }

        if (normalized.Contains(SCConstants.Input.Mouse.Button5))
        {
            button = VirtualKeyCode.XBUTTON2;
            return true;
        }

        return false;
    }

    private static bool TryParseKeyboard(string scBinding, out DirectInputKeyCode[] modifiers, out DirectInputKeyCode[] keys)
    {
        modifiers = [];
        keys = [];

        List<DirectInputKeyCode> modifierList = [];
        List<DirectInputKeyCode> keyList = [];

        foreach (string token in SplitTokens(scBinding))
        {
            if (TryParseModifier(token, out DirectInputKeyCode modifier))
            {
                modifierList.Add(modifier);
                continue;
            }

            if (TryParseKey(token, out DirectInputKeyCode key))
            {
                keyList.Add(key);
            }
        }

        if (keyList.Count == 0)
        {
            // Support modifier-only bindings (e.g., "lctrl" or "lctrl+lalt") by treating the last modifier as the key.
            if (modifierList.Count == 0)
            {
                return false;
            }

            DirectInputKeyCode lastModifierAsKey = modifierList[^1];
            modifierList.RemoveAt(modifierList.Count - 1);
            keyList.Add(lastModifierAsKey);
        }

        modifiers = modifierList.ToArray();
        keys = keyList.ToArray();
        return true;
    }

    private static bool TryParseModifier(string token, out DirectInputKeyCode modifier)
    {
        modifier = default;

        return s_modifiers.TryGetValue(token, out modifier);
    }

    private static bool TryParseKey(string token, out DirectInputKeyCode key)
    {
        key = default;

        if (s_specialKeys.TryGetValue(token, out DirectInputKeyCode result))
        {
            key = result;
            return true;
        }

        return SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(token, out key);
    }

    private static string[] SplitTokens(string binding) =>
        binding.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
///     Result of parsing a binding string.
/// </summary>
public sealed record ParsedInputResult(InputType Type, object Value);
