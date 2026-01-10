using System.Collections.Concurrent;
using BarRaider.SdTools;
using Newtonsoft.Json;
using SCStreamDeck.SCCore.Common;
using SCStreamDeck.SCCore.Logging;
using SCStreamDeck.SCCore.Models;
using SCStreamDeck.SCCore.Services.Keybinding.ActivationHandlers;
using WindowsInput;
using WindowsInput.Native;


namespace SCStreamDeck.SCCore.Services.Keybinding;

/// <summary>
///     Core keybinding service that consolidates loading, caching, and execution.
///     Uses Strategy pattern for activation mode handling via ActivationModeHandlerRegistry.
/// </summary>
public sealed class KeybindingService : IKeybindingService, IDisposable
{
    private readonly Dictionary<string, KeybindingAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Timer> _activationTimers = new(StringComparer.OrdinalIgnoreCase);

    private readonly nint _currentKeyboardLayout;
    private readonly ActivationModeHandlerRegistry _handlerRegistry;
    private readonly ConcurrentDictionary<string, byte> _holdStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly KeybindingInputExecutor _inputExecutor;

    private readonly IInputSimulator _inputSimulator;
    private readonly object _lock = new();
    private Dictionary<string, ActivationModeMetadata> _activationModes = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public KeybindingService(IInputSimulator? inputSimulator = null)
    {
        _inputSimulator = inputSimulator ?? new InputSimulator();
        _currentKeyboardLayout = NativeMethods.GetKeyboardLayout(0);

        _handlerRegistry = new ActivationModeHandlerRegistry();
        _inputExecutor = new KeybindingInputExecutor(_inputSimulator, _holdStates, _activationTimers);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var kvp in _activationTimers)
            if (_activationTimers.TryRemove(kvp.Key, out var timer))
                timer.Dispose();

        _holdStates.Clear();
        _disposed = true;
    }

    public bool IsLoaded { get; private set; }

    public async Task<bool> LoadKeybindingsAsync(string jsonPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!SecurePathValidator.TryNormalizePath(jsonPath, out var validatedPath))
            {
                IsLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingService)}]: {ErrorMessages.InvalidPath}");
                return false;
            }

            if (!File.Exists(validatedPath))
            {
                IsLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingService)}]: {ErrorMessages.FileReadFailed}");
                return false;
            }

            var json = await File.ReadAllTextAsync(validatedPath, cancellationToken).ConfigureAwait(false);
            var dataFile = JsonConvert.DeserializeObject<KeybindingDataFile>(json);

            if (dataFile?.Actions == null)
            {
                IsLoaded = false;
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(KeybindingService)}]: {ErrorMessages.JsonParseFailed}");
                return false;
            }

            lock (_lock)
            {
                _actions.Clear();

                // Map DTO actions to domain model
                foreach (var action in dataFile.Actions)
                {
                    var keybindingAction = new KeybindingAction
                    {
                        ActionName = action.Name ?? string.Empty,
                        MapName = action.MapName ?? string.Empty,
                        UILabel = action.Label ?? string.Empty,
                        UIDescription = action.Description ?? string.Empty,
                        UICategory = action.Category ?? string.Empty,
                        KeyboardBinding = action.Bindings?.Keyboard ?? string.Empty,
                        MouseBinding = action.Bindings?.Mouse ?? string.Empty,
                        JoystickBinding = action.Bindings?.Joystick ?? string.Empty,
                        GamepadBinding = action.Bindings?.Gamepad ?? string.Empty,
                        ActivationMode = action.ActivationMode
                    };
                    _actions[keybindingAction.ActionName] = keybindingAction;
                }

                // Load activation modes from metadata
                if (dataFile.Metadata?.ActivationModes != null && dataFile.Metadata.ActivationModes.Count > 0)
                {
                    _activationModes = dataFile.Metadata.ActivationModes;
                }
                    

                IsLoaded = true;
            }

            return true;
        }

        catch (IOException ex)
        {
            IsLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"KeybindingService: {ErrorMessages.FileReadFailed} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            IsLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"KeybindingService: {ErrorMessages.FileAccessDenied} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
        catch (JsonException ex)
        {
            IsLoaded = false;
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"KeybindingService: {ErrorMessages.JsonParseFailed} '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ExecuteAsync(KeybindingExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsValid(out var errorMessage))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(KeybindingService)}]: Invalid execution context - {errorMessage}");
            return false;
        }

        try
        {
            return await Task.Run(() => ExecuteWithActivationMode(context, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }

        catch (InvalidOperationException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[KeybindingService]: {ErrorMessages.OperationFailedFor} '{context.ActionName}': {ex.Message}");
            return false;
        }
        catch (ArgumentException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[KeybindingService]: {ErrorMessages.OperationFailedFor} '{context.ActionName}': {ex.Message}");
            return false;
        }
    }

    public bool TryGetAction(string actionName, out KeybindingAction? action)
    {
        lock (_lock)
        {
            return _actions.TryGetValue(actionName, out action);
        }
    }

    public IReadOnlyList<KeybindingAction> GetAllActions()
    {
        lock (_lock)
        {
            return _actions.Values.ToList();
        }
    }

    public IntPtr GetKeyboardLayoutId()
    {
        return _currentKeyboardLayout;
    }


    /// <summary>
    ///     Executes an action using the Strategy pattern via ActivationModeHandlerRegistry.
    ///     Each activation mode (press, hold, tap, etc.) has its own handler.
    /// </summary>
    private bool ExecuteWithActivationMode(KeybindingExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parsedInput = ParseBinding(context.Binding);
        if (parsedInput == null)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(KeybindingService)}]: Failed to parse binding '{context.Binding}'");
            return false;
        }

        ActivationModeMetadata? metadata;
        lock (_lock)
        {
            var modeKey = context.ActivationMode.ToString();
            if (!_activationModes.TryGetValue(modeKey, out metadata))
                metadata = new ActivationModeMetadata { OnPress = true };
        }

        var executionContext = new ActivationExecutionContext
        {
            ActionName = context.ActionName,
            Input = new ParsedInput
            {
                Type = parsedInput.Type,
                Value = parsedInput.Value
            },
            IsKeyDown = context.IsKeyDown,
            Mode = context.ActivationMode
        };

        return _handlerRegistry.Execute(executionContext, metadata, _inputExecutor);
    }

    private static ParsedInputResult? ParseBinding(string binding)
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

    private sealed record ParsedInputResult(InputType Type, object Value);
}
