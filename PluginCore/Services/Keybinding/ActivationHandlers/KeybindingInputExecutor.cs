using System.Collections.Concurrent;
using SCStreamDeck.Models;
using WindowsInput;
using WindowsInput.Native;

// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

namespace SCStreamDeck.Services.Keybinding.ActivationHandlers;

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
        }

        return false;
    }

    public bool ExecutePressNoRepeat(ParsedInput input)
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
                        ExecuteModifiedKeyPress(key, modifiers);
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
                ExecuteMouseWheelPress(input.Value);
                return true;

            default:
                return false;
        }
    }

    public bool ExecuteDown(ParsedInput input, string actionKey)
    {
        if (!_holdStates.TryAdd(actionKey, 1))
        {
            return true;
        }

        switch (input.Type)
        {
            case InputType.Keyboard:
                return ExecuteKeyboardDown(input.Value, actionKey);

            case InputType.MouseButton:
                VirtualKeyCode button = (VirtualKeyCode)input.Value;
                ExecuteMouseButtonDown(button);
                return true;

            case InputType.MouseWheel:
                ExecuteMouseWheelDown(input.Value, actionKey);
                return true;

            default:
                return true;
        }
    }

    public bool ExecuteUp(ParsedInput input, string actionKey)
    {
        if (!_holdStates.TryRemove(actionKey, out _))
        {
            return true;
        }

        if (_activationTimers.TryRemove(actionKey, out Timer? timer))
        {
            timer.Dispose();
        }

        switch (input.Type)
        {
            case InputType.Keyboard:
                return ExecuteKeyboardUp(input.Value);

            case InputType.MouseButton:
                VirtualKeyCode button = (VirtualKeyCode)input.Value;
                ExecuteMouseButtonUp(button);
                return true;

            case InputType.MouseWheel:
                ReleaseMouseWheelModifiers(input.Value);
                return true;

            default:
                return true;
        }
    }

    public bool ScheduleDelayedPress(ParsedInput input, string actionKey, float delaySeconds)
    {
        CancelExistingTimer(actionKey);

        int delayMs = (int)(delaySeconds * 1000);
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
        CancelExistingTimer(actionKey);

        int delayMs = (int)(delaySeconds * 1000);
        Timer timer = new(_ =>
        {
            ExecuteDown(input, actionKey);
            _activationTimers.TryRemove(actionKey, out Timer? _);
        }, null, delayMs, Timeout.Infinite);
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

    private void ExecuteModifiedKeyPress(DirectInputKeyCode key, DirectInputKeyCode[] modifiers)
    {
        PressModifiers(modifiers);
        Thread.Sleep(20);
        _inputSimulator.Keyboard.KeyDown(key);
        Thread.Sleep(30);
        _inputSimulator.Keyboard.KeyUp(key);
        ReleaseModifiers(modifiers);
    }

    private void PressModifiers(DirectInputKeyCode[] modifiers)
    {
        foreach (DirectInputKeyCode modifier in modifiers)
        {
            _inputSimulator.Keyboard.KeyDown(modifier);
        }
    }

    private void ReleaseModifiers(DirectInputKeyCode[] modifiers)
    {
        foreach (DirectInputKeyCode modifier in modifiers.Reverse())
        {
            _inputSimulator.Keyboard.KeyUp(modifier);
        }
    }

    private void ExecuteMouseWheelPress(object wheelValue)
    {
        if (wheelValue is ValueTuple<DirectInputKeyCode[], int> wheelWithModifiers)
        {
            (DirectInputKeyCode[] wheelModifiers, int wheelDirection) = wheelWithModifiers;

            PressModifiers(wheelModifiers);
            _inputSimulator.Mouse.VerticalScroll(wheelDirection);
            Thread.Sleep(50);
            ReleaseModifiers(wheelModifiers);
        }
        else
        {
            int wheelDir = (int)wheelValue;
            _inputSimulator.Mouse.VerticalScroll(wheelDir);
        }
    }

    private bool ExecuteKeyboardDown(object keyboardValue, string actionKey)
    {
        (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) =
            ((DirectInputKeyCode[], DirectInputKeyCode[]))keyboardValue;

        if (modifiers.Length > 0)
        {
            PressModifiers(modifiers);
            Timer repeatTimer = new(_ =>
            {
                foreach (DirectInputKeyCode key in keys)
                {
                    _inputSimulator.Keyboard.DelayedKeyPress(key, 50);
                }
            }, null, 0, 200);
            _activationTimers[actionKey] = repeatTimer;
        }
        else
        {
            foreach (DirectInputKeyCode key in keys)
            {
                _inputSimulator.Keyboard.KeyDown(key);
            }
        }

        return true;
    }

    private void ExecuteMouseWheelDown(object wheelValue, string actionKey)
    {
        if (wheelValue is ValueTuple<DirectInputKeyCode[], int> wheelWithModifiers)
        {
            (DirectInputKeyCode[] wheelModifiers, int wheelDirection) = wheelWithModifiers;

            PressModifiers(wheelModifiers);
            Timer scrollTimer = new(_ => _inputSimulator.Mouse.VerticalScroll(wheelDirection), null, 0, 50);
            _activationTimers[actionKey] = scrollTimer;
        }
        else
        {
            int wheelDir = (int)wheelValue;
            Timer scrollTimer = new(_ => _inputSimulator.Mouse.VerticalScroll(wheelDir), null, 0, 50);
            _activationTimers[actionKey] = scrollTimer;
        }
    }

    private bool ExecuteKeyboardUp(object keyboardValue)
    {
        (DirectInputKeyCode[] modifiers, DirectInputKeyCode[] keys) =
            ((DirectInputKeyCode[], DirectInputKeyCode[]))keyboardValue;

        if (modifiers.Length == 0)
        {
            foreach (DirectInputKeyCode key in keys)
            {
                _inputSimulator.Keyboard.KeyUp(key);
            }
        }
        else
        {
            ReleaseModifiers(modifiers);
        }

        return true;
    }

    private void ReleaseMouseWheelModifiers(object wheelValue)
    {
        if (wheelValue is not ValueTuple<DirectInputKeyCode[], int> wheelWithModifiers)
        {
            return;
        }

        (DirectInputKeyCode[] wheelModifiers, _) = wheelWithModifiers;
        ReleaseModifiers(wheelModifiers);
    }

    private void CancelExistingTimer(string actionKey)
    {
        if (_activationTimers.TryRemove(actionKey, out Timer? existingTimer))
        {
            existingTimer.Dispose();
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
