using System.Collections.Concurrent;
using SCStreamDeck.SCCore.Models;
using WindowsInput;
using WindowsInput.Native;

namespace SCStreamDeck.SCCore.Services.Keybinding.ActivationHandlers;

/// <summary>
///     Adapts the KeybindingService's input simulation capabilities to the IInputExecutor interface.
///     Used by activation mode handlers to execute input actions.
///     Uses DirectInputKeyCode and InputSimulatorPlus methods for EAC-compatible input.
/// </summary>
internal sealed class KeybindingInputExecutor(
    IInputSimulator inputSimulator,
    ConcurrentDictionary<string, byte> holdStates,
    ConcurrentDictionary<string, Timer> activationTimers)
    : IInputExecutor
{
    private readonly ConcurrentDictionary<string, Timer> _activationTimers =
        activationTimers ?? throw new ArgumentNullException(nameof(activationTimers));

    private readonly ConcurrentDictionary<string, byte> _holdStates =
        holdStates ?? throw new ArgumentNullException(nameof(holdStates));

    private readonly IInputSimulator _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));

    public bool ExecutePress(ParsedInput input)
    {
        switch (input.Type)
        {
            case InputType.Keyboard:
                (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) =
                    ((DirectInputKeyCode[], DirectInputKeyCode[]))input.Value;

                foreach (DirectInputKeyCode key in keys)
                {
                    if (modifiers.Length > 0)
                    {
                        _inputSimulator.Keyboard.DelayedModifiedKeyStroke(modifiers, key, 50);
                    }
                    else
                    {
                        _inputSimulator.Keyboard.DelayedKeyPress(key, 50);
                    }
                }

                return true;

            case InputType.MouseButton:
                VirtualKeyCode button = (VirtualKeyCode)input.Value;
                ExecuteMouseButtonClick(button);
                return true;

            case InputType.MouseWheel:
                // Check if it's a tuple (modifiers[] + direction) or just direction
                if (input.Value is ValueTuple<DirectInputKeyCode[], int> wheelWithModifiers)
                {
                    (DirectInputKeyCode[] wheelModifiers, int wheelDirection) = wheelWithModifiers;

                    foreach (DirectInputKeyCode modifier in wheelModifiers)
                    {
                        _inputSimulator.Keyboard.KeyDown(modifier);
                    }

                    _inputSimulator.Mouse.VerticalScroll(wheelDirection);
                    Thread.Sleep(50);

                    // Release modifiers up (reverse order) - separate SendInput() calls
                    foreach (DirectInputKeyCode modifier in wheelModifiers.Reverse())
                    {
                        _inputSimulator.Keyboard.KeyUp(modifier);
                    }
                }
                else
                {
                    // Standalone mouse wheel (no modifiers)
                    int wheelDir = (int)input.Value;
                    _inputSimulator.Mouse.VerticalScroll(wheelDir);
                }

                return true;

            default:
                return false;
        }
    }

    public bool ExecuteDown(ParsedInput input, string actionKey)
    {
        // TryAdd is atomic - no lock needed
        if (!_holdStates.TryAdd(actionKey, 1))
        {
            return true; // Already holding
        }

        switch (input.Type)
        {
            case InputType.Keyboard:
                (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) =
                    ((DirectInputKeyCode[], DirectInputKeyCode[]))input.Value;
                // Use ModifiedKeyStrokeDown - sends KeyDown for modifiers and keys, holds them
                foreach (DirectInputKeyCode key in keys)
                {
                    _inputSimulator.Keyboard.ModifiedKeyStrokeDown(modifiers, key);
                }

                return true;

            case InputType.MouseButton:
                VirtualKeyCode button = (VirtualKeyCode)input.Value;
                ExecuteMouseButtonDown(button);
                return true;

            default:
                return true;
        }
    }

    public bool ExecuteUp(ParsedInput input, string actionKey)
    {
        // TryRemove is atomic - no lock needed
        if (!_holdStates.TryRemove(actionKey, out _))
        {
            return true; // Was not holding
        }

        switch (input.Type)
        {
            case InputType.Keyboard:
                (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) =
                    ((DirectInputKeyCode[], DirectInputKeyCode[]))input.Value;
                // Use ModifiedKeyStrokeUp - sends KeyUp for keys and modifiers (reverse order)
                // Process in reverse order of ExecuteDown
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    _inputSimulator.Keyboard.ModifiedKeyStrokeUp(modifiers, keys[i]);
                }

                return true;

            case InputType.MouseButton:
                VirtualKeyCode button = (VirtualKeyCode)input.Value;
                ExecuteMouseButtonUp(button);
                return true;

            default:
                return true;
        }
    }

    public bool ScheduleDelayedPress(ParsedInput input, string actionKey, float delaySeconds)
    {
        int delayMs = (int)(delaySeconds * 1000);

        // Cancel any existing timer for this action atomically
        if (_activationTimers.TryRemove(actionKey, out Timer? existingTimer))
        {
            existingTimer.Dispose();
        }

        Timer timer = new(_ => ExecutePress(input), null, delayMs, Timeout.Infinite);
        _activationTimers[actionKey] = timer;

        return true;
    }

    public void CancelDelayedPress(string actionKey)
    {
        if (_activationTimers.TryRemove(actionKey, out Timer? timer))
        {
            timer.Dispose();
        }
    }

    public bool ScheduleDelayedHold(ParsedInput input, string actionKey, float delaySeconds)
    {
        int delayMs = (int)(delaySeconds * 1000);

        // Cancel any existing timer for this action atomically
        if (_activationTimers.TryRemove(actionKey, out Timer? existingTimer))
        {
            existingTimer.Dispose();
        }

        Timer timer = new(_ => ExecuteDown(input, actionKey), null, delayMs, Timeout.Infinite);
        _activationTimers[actionKey] = timer;

        return true;
    }

    public void CancelDelayedHold(string actionKey)
    {
        if (_activationTimers.TryRemove(actionKey, out Timer? timer))
        {
            timer.Dispose();
        }
    }

    #region Mouse Button Helpers

    private void ExecuteMouseButtonClick(VirtualKeyCode button)
    {
        switch (button)
        {
            case VirtualKeyCode.LBUTTON:
                _inputSimulator.Mouse.LeftButtonClick();
                break;
            case VirtualKeyCode.RBUTTON:
                _inputSimulator.Mouse.RightButtonClick();
                break;
            case VirtualKeyCode.MBUTTON:
                _inputSimulator.Mouse.MiddleButtonClick();
                break;
            case VirtualKeyCode.XBUTTON1:
                _inputSimulator.Mouse.XButtonClick(1);
                break;
            case VirtualKeyCode.XBUTTON2:
                _inputSimulator.Mouse.XButtonClick(2);
                break;
        }
    }

    private void ExecuteMouseButtonDown(VirtualKeyCode button)
    {
        switch (button)
        {
            case VirtualKeyCode.LBUTTON:
                _inputSimulator.Mouse.LeftButtonDown();
                break;
            case VirtualKeyCode.RBUTTON:
                _inputSimulator.Mouse.RightButtonDown();
                break;
            case VirtualKeyCode.MBUTTON:
                _inputSimulator.Mouse.MiddleButtonDown();
                break;
            case VirtualKeyCode.XBUTTON1:
                _inputSimulator.Mouse.XButtonDown(1);
                break;
            case VirtualKeyCode.XBUTTON2:
                _inputSimulator.Mouse.XButtonDown(2);
                break;
        }
    }

    private void ExecuteMouseButtonUp(VirtualKeyCode button)
    {
        switch (button)
        {
            case VirtualKeyCode.LBUTTON:
                _inputSimulator.Mouse.LeftButtonUp();
                break;
            case VirtualKeyCode.RBUTTON:
                _inputSimulator.Mouse.RightButtonUp();
                break;
            case VirtualKeyCode.MBUTTON:
                _inputSimulator.Mouse.MiddleButtonUp();
                break;
            case VirtualKeyCode.XBUTTON1:
                _inputSimulator.Mouse.XButtonUp(1);
                break;
            case VirtualKeyCode.XBUTTON2:
                _inputSimulator.Mouse.XButtonUp(2);
                break;
        }
    }

    #endregion
}
