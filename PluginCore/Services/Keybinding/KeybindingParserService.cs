using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Models;
using WindowsInput.Native;

namespace SCStreamDeck.SCCore.Services.Keybinding;

/// <summary>
///     Service for parsing keybinding strings into executable inputs.
/// </summary>
public sealed class KeybindingParserService : IKeybindingParserService
{
    public ParsedInputResult? ParseBinding(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding)) return null;
        var normalized = binding.ToUpperInvariant().Trim();

        // Check mouse buttons first (no modifiers)
        if (TryParseMouseButton(normalized, out var mouseButton))
            return new ParsedInputResult(InputType.MouseButton, mouseButton);

        // Check for mouse wheel with modifiers (e.g., "lalt+mwheel_up")
        if (TryParseMouseWheelWithModifiers(normalized, out var modifiers, out var wheelDirection))
            return new ParsedInputResult(InputType.MouseWheel, (modifiers, wheelDirection));

        // Check keyboard bindings
        if (TryParseKeyboard(normalized, out var kbModifiers, out var keys))
            return new ParsedInputResult(InputType.Keyboard, (kbModifiers, keys));

        // Check standalone mouse wheel (no modifiers)
        if (normalized.Contains(InputConstants.Mouse.WheelUp, StringComparison.Ordinal))
        {
            // Windows VerticalScroll: negative = content scrolls UP
            return new ParsedInputResult(InputType.MouseWheel, -1);
        }

        if (normalized.Contains(InputConstants.Mouse.WheelDown, StringComparison.Ordinal))
        {
            // Windows VerticalScroll: positive = content scrolls DOWN
            return new ParsedInputResult(InputType.MouseWheel, 1);
        }

        return null;
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
            return false;

        if (!binding.Contains(InputConstants.Mouse.WheelPrefix, StringComparison.Ordinal))
            return false;

        var tokens = binding.Split('+');
        var modifierList = new List<DirectInputKeyCode>();

        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (TryParseModifier(trimmed, out var modifier))
                modifierList.Add(modifier);
            else if (trimmed == InputConstants.Mouse.WheelUp)
                wheelDirection = -1; // Negative = scroll UP
            else if (trimmed == InputConstants.Mouse.WheelDown)
                wheelDirection = 1; // Positive = scroll DOWN
        }

        if (modifierList.Count == 0 || wheelDirection == 0)
            return false;

        modifiers = modifierList.ToArray();
        return true;
    }

    private static bool TryParseMouseButton(string normalized, out VirtualKeyCode button)
    {
        button = VirtualKeyCode.LBUTTON;

        if (normalized.Contains(InputConstants.Mouse.Button1) || normalized == InputConstants.Mouse.LeftButton)
        {
            button = VirtualKeyCode.LBUTTON;
            return true;
        }

        if (normalized.Contains(InputConstants.Mouse.Button2) || normalized == InputConstants.Mouse.RightButton)
        {
            button = VirtualKeyCode.RBUTTON;
            return true;
        }

        if (normalized.Contains(InputConstants.Mouse.Button3) || normalized == InputConstants.Mouse.MiddleButton)
        {
            button = VirtualKeyCode.MBUTTON;
            return true;
        }

        if (normalized.Contains(InputConstants.Mouse.Button4))
        {
            button = VirtualKeyCode.XBUTTON1;
            return true;
        }

        if (normalized.Contains(InputConstants.Mouse.Button5))
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

        var tokens = scBinding.Split('+');
        var modifierList = new List<DirectInputKeyCode>();
        var keyList = new List<DirectInputKeyCode>();

        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (TryParseModifier(trimmed, out var modifier))
                modifierList.Add(modifier);
            else if (TryParseKey(trimmed, out var key))
                keyList.Add(key);
        }

        if (keyList.Count == 0)
            return false;

        modifiers = modifierList.ToArray();
        keys = keyList.ToArray();
        return true;
    }

    private static bool TryParseModifier(string token, out DirectInputKeyCode modifier)
    {
        modifier = default;

        return token switch
        {
            InputConstants.Keyboard.LAlt => SetModifier(DirectInputKeyCode.DikLalt, out modifier),
            InputConstants.Keyboard.RAlt => SetModifier(DirectInputKeyCode.DikRalt, out modifier),
            InputConstants.Keyboard.LShift => SetModifier(DirectInputKeyCode.DikLshift, out modifier),
            InputConstants.Keyboard.RShift => SetModifier(DirectInputKeyCode.DikRshift, out modifier),
            InputConstants.Keyboard.LCtrl => SetModifier(DirectInputKeyCode.DikLcontrol, out modifier),
            InputConstants.Keyboard.RCtrl => SetModifier(DirectInputKeyCode.DikRcontrol, out modifier),
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

        // Special keys mapping
        var specialKeyResult = token switch
        {
            InputConstants.Keyboard.F1 => SetKey(DirectInputKeyCode.DikF1, out key),
            InputConstants.Keyboard.F2 => SetKey(DirectInputKeyCode.DikF2, out key),
            InputConstants.Keyboard.F3 => SetKey(DirectInputKeyCode.DikF3, out key),
            InputConstants.Keyboard.F4 => SetKey(DirectInputKeyCode.DikF4, out key),
            InputConstants.Keyboard.F5 => SetKey(DirectInputKeyCode.DikF5, out key),
            InputConstants.Keyboard.F6 => SetKey(DirectInputKeyCode.DikF6, out key),
            InputConstants.Keyboard.F7 => SetKey(DirectInputKeyCode.DikF7, out key),
            InputConstants.Keyboard.F8 => SetKey(DirectInputKeyCode.DikF8, out key),
            InputConstants.Keyboard.F9 => SetKey(DirectInputKeyCode.DikF9, out key),
            InputConstants.Keyboard.F10 => SetKey(DirectInputKeyCode.DikF10, out key),
            InputConstants.Keyboard.F11 => SetKey(DirectInputKeyCode.DikF11, out key),
            InputConstants.Keyboard.F12 => SetKey(DirectInputKeyCode.DikF12, out key),
            InputConstants.Keyboard.Space => SetKey(DirectInputKeyCode.DikSpace, out key),
            InputConstants.Keyboard.Enter => SetKey(DirectInputKeyCode.DikReturn, out key),
            InputConstants.Keyboard.Tab => SetKey(DirectInputKeyCode.DikTab, out key),
            InputConstants.Keyboard.Escape => SetKey(DirectInputKeyCode.DikEscape, out key),
            InputConstants.Keyboard.Backspace => SetKey(DirectInputKeyCode.DikBackspace, out key),
            InputConstants.Keyboard.CapsLock => SetKey(DirectInputKeyCode.DikCapital, out key),
            InputConstants.Keyboard.NumLock => SetKey(DirectInputKeyCode.DikNumlock, out key),
            InputConstants.Keyboard.ScrollLock => SetKey(DirectInputKeyCode.DikScroll, out key),
            InputConstants.Keyboard.Up => SetKey(DirectInputKeyCode.DikUp, out key),
            InputConstants.Keyboard.Down => SetKey(DirectInputKeyCode.DikDown, out key),
            InputConstants.Keyboard.Left => SetKey(DirectInputKeyCode.DikLeft, out key),
            InputConstants.Keyboard.Right => SetKey(DirectInputKeyCode.DikRight, out key),
            InputConstants.Keyboard.Home => SetKey(DirectInputKeyCode.DikHome, out key),
            InputConstants.Keyboard.End => SetKey(DirectInputKeyCode.DikEnd, out key),
            InputConstants.Keyboard.PgUp => SetKey(DirectInputKeyCode.DikPageUp, out key),
            InputConstants.Keyboard.PgDown => SetKey(DirectInputKeyCode.DikPageDown, out key),
            InputConstants.Keyboard.Insert => SetKey(DirectInputKeyCode.DikInsert, out key),
            InputConstants.Keyboard.Delete => SetKey(DirectInputKeyCode.DikDelete, out key),
            _ => false
        };

        if (specialKeyResult)
            return true;

        // Use SCKeyToDirectInputMapper for all other keys (letters, numbers, punctuation)
        return SCKeyToDirectInputMapper.TryGetDirectInputKeyCode(token, out key);

        static bool SetKey(DirectInputKeyCode value, out DirectInputKeyCode output)
        {
            output = value;
            return true;
        }
    }
}
