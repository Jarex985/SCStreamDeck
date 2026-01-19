using SCStreamDeck.Common;
using SCStreamDeck.Models;
using WindowsInput.Native;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for parsing keybinding strings into executable inputs.
/// </summary>
public sealed class KeybindingParserService : IKeybindingParserService
{
    public ParsedInputResult? ParseBinding(string binding)
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
        modifiers = Array.Empty<DirectInputKeyCode>();
        wheelDirection = 0;

        if (!binding.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        if (!binding.Contains(SCConstants.Input.Mouse.WheelPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        List<DirectInputKeyCode> modifierList = new();

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
        modifiers = Array.Empty<DirectInputKeyCode>();
        keys = Array.Empty<DirectInputKeyCode>();

        List<DirectInputKeyCode> modifierList = new();
        List<DirectInputKeyCode> keyList = new();

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
            return false;
        }

        modifiers = modifierList.ToArray();
        keys = keyList.ToArray();
        return true;
    }

    private static bool TryParseModifier(string token, out DirectInputKeyCode modifier)
    {
        modifier = default;

        return token switch
        {
            SCConstants.Input.Keyboard.LAlt => SetModifier(DirectInputKeyCode.DikLalt, out modifier),
            SCConstants.Input.Keyboard.RAlt => SetModifier(DirectInputKeyCode.DikRalt, out modifier),
            SCConstants.Input.Keyboard.LShift => SetModifier(DirectInputKeyCode.DikLshift, out modifier),
            SCConstants.Input.Keyboard.RShift => SetModifier(DirectInputKeyCode.DikRshift, out modifier),
            SCConstants.Input.Keyboard.LCtrl => SetModifier(DirectInputKeyCode.DikLcontrol, out modifier),
            SCConstants.Input.Keyboard.RCtrl => SetModifier(DirectInputKeyCode.DikRcontrol, out modifier),
            _ => false
        };

        static bool SetModifier(DirectInputKeyCode value, out DirectInputKeyCode output)
        {
            output = value;
            return true;
        }
    }

    private static bool TryParseKey(string token, out DirectInputKeyCode key)
    {
        key = default;

        bool specialKeyResult = token switch
        {
            SCConstants.Input.Keyboard.F1 => SetKey(DirectInputKeyCode.DikF1, out key),
            SCConstants.Input.Keyboard.F2 => SetKey(DirectInputKeyCode.DikF2, out key),
            SCConstants.Input.Keyboard.F3 => SetKey(DirectInputKeyCode.DikF3, out key),
            SCConstants.Input.Keyboard.F4 => SetKey(DirectInputKeyCode.DikF4, out key),
            SCConstants.Input.Keyboard.F5 => SetKey(DirectInputKeyCode.DikF5, out key),
            SCConstants.Input.Keyboard.F6 => SetKey(DirectInputKeyCode.DikF6, out key),
            SCConstants.Input.Keyboard.F7 => SetKey(DirectInputKeyCode.DikF7, out key),
            SCConstants.Input.Keyboard.F8 => SetKey(DirectInputKeyCode.DikF8, out key),
            SCConstants.Input.Keyboard.F9 => SetKey(DirectInputKeyCode.DikF9, out key),
            SCConstants.Input.Keyboard.F10 => SetKey(DirectInputKeyCode.DikF10, out key),
            SCConstants.Input.Keyboard.F11 => SetKey(DirectInputKeyCode.DikF11, out key),
            SCConstants.Input.Keyboard.F12 => SetKey(DirectInputKeyCode.DikF12, out key),
            SCConstants.Input.Keyboard.Space => SetKey(DirectInputKeyCode.DikSpace, out key),
            SCConstants.Input.Keyboard.Enter => SetKey(DirectInputKeyCode.DikReturn, out key),
            SCConstants.Input.Keyboard.Tab => SetKey(DirectInputKeyCode.DikTab, out key),
            SCConstants.Input.Keyboard.Escape => SetKey(DirectInputKeyCode.DikEscape, out key),
            SCConstants.Input.Keyboard.Backspace => SetKey(DirectInputKeyCode.DikBackspace, out key),
            SCConstants.Input.Keyboard.CapsLock => SetKey(DirectInputKeyCode.DikCapital, out key),
            SCConstants.Input.Keyboard.NumLock => SetKey(DirectInputKeyCode.DikNumlock, out key),
            SCConstants.Input.Keyboard.ScrollLock => SetKey(DirectInputKeyCode.DikScroll, out key),
            SCConstants.Input.Keyboard.Up => SetKey(DirectInputKeyCode.DikUp, out key),
            SCConstants.Input.Keyboard.Down => SetKey(DirectInputKeyCode.DikDown, out key),
            SCConstants.Input.Keyboard.Left => SetKey(DirectInputKeyCode.DikLeft, out key),
            SCConstants.Input.Keyboard.Right => SetKey(DirectInputKeyCode.DikRight, out key),
            SCConstants.Input.Keyboard.Home => SetKey(DirectInputKeyCode.DikHome, out key),
            SCConstants.Input.Keyboard.End => SetKey(DirectInputKeyCode.DikEnd, out key),
            SCConstants.Input.Keyboard.PgUp => SetKey(DirectInputKeyCode.DikPageUp, out key),
            SCConstants.Input.Keyboard.PgDown => SetKey(DirectInputKeyCode.DikPageDown, out key),
            SCConstants.Input.Keyboard.Insert => SetKey(DirectInputKeyCode.DikInsert, out key),
            SCConstants.Input.Keyboard.Delete => SetKey(DirectInputKeyCode.DikDelete, out key),
            _ => false
        };

        if (specialKeyResult)
        {
            return true;
        }

        return SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(token, out key);

        static bool SetKey(DirectInputKeyCode value, out DirectInputKeyCode output)
        {
            output = value;
            return true;
        }
    }

    private static string[] SplitTokens(string binding) =>
        binding.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
