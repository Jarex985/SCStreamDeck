using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WindowsInput.Native;

namespace WindowsInput
{
    /// <summary>
    ///     Implements the <see cref="IKeyboardSimulator" /> interface by calling the an <see cref="IInputMessageDispatcher" /> to
    ///     simulate Keyboard gestures.
    /// </summary>
    public class KeyboardSimulator : IKeyboardSimulator
    {
        private readonly IInputSimulator _inputSimulator;

        /// <summary>
        ///     The instance of the <see cref="IInputMessageDispatcher" /> to use for dispatching <see cref="INPUT" /> messages.
        /// </summary>
        private readonly IInputMessageDispatcher _messageDispatcher;

        /// <summary>
        ///     Initializes a new instance of the <see cref="KeyboardSimulator" /> class using an instance of a
        ///     <see cref="WindowsInputMessageDispatcher" /> for dispatching <see cref="INPUT" /> messages.
        /// </summary>
        /// <param name="inputSimulator">The <see cref="IInputSimulator" /> that owns this instance.</param>
        public KeyboardSimulator(IInputSimulator inputSimulator)
        {
            if (inputSimulator == null)
            {
                throw new ArgumentNullException("inputSimulator");
            }

            _inputSimulator = inputSimulator;
            _messageDispatcher = new WindowsInputMessageDispatcher();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="KeyboardSimulator" /> class using the specified
        ///     <see cref="IInputMessageDispatcher" /> for dispatching <see cref="INPUT" /> messages.
        /// </summary>
        /// <param name="inputSimulator">The <see cref="IInputSimulator" /> that owns this instance.</param>
        /// <param name="messageDispatcher">The <see cref="IInputMessageDispatcher" /> to use for dispatching <see cref="INPUT" /> messages.</param>
        /// <exception cref="InvalidOperationException">If null is passed as the <paramref name="messageDispatcher" />.</exception>
        internal KeyboardSimulator(IInputSimulator inputSimulator, IInputMessageDispatcher messageDispatcher)
        {
            if (inputSimulator == null)
            {
                throw new ArgumentNullException("inputSimulator");
            }

            if (messageDispatcher == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "The {0} cannot operate with a null {1}. Please provide a valid {1} instance to use for dispatching {2} messages.",
                        typeof(KeyboardSimulator).Name, typeof(IInputMessageDispatcher).Name, typeof(INPUT).Name));
            }

            _inputSimulator = inputSimulator;
            _messageDispatcher = messageDispatcher;
        }

        /// <summary>
        ///     Gets the <see cref="IMouseSimulator" /> instance for simulating Mouse input.
        /// </summary>
        /// <value>The <see cref="IMouseSimulator" /> instance.</value>
        public IMouseSimulator Mouse => _inputSimulator.Mouse;

        /// <summary>
        ///     Calls the Win32 SendInput method to simulate a KeyDown.
        /// </summary>
        /// <param name="keyCode">The <see cref="VirtualKeyCode" /> to press</param>
        public IKeyboardSimulator KeyDown(VirtualKeyCode keyCode)
        {
            INPUT[] inputList = new InputBuilder().AddKeyDown(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        /// <summary>
        ///     Calls the Win32 SendInput method to simulate a KeyUp.
        /// </summary>
        /// <param name="keyCode">The <see cref="VirtualKeyCode" /> to lift up</param>
        public IKeyboardSimulator KeyUp(VirtualKeyCode keyCode)
        {
            INPUT[] inputList = new InputBuilder().AddKeyUp(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        /// <summary>
        ///     Calls the Win32 SendInput method with a KeyDown and KeyUp message in the same input sequence in order to simulate a Key
        ///     PRESS.
        /// </summary>
        /// <param name="keyCode">The <see cref="VirtualKeyCode" /> to press</param>
        public IKeyboardSimulator KeyPress(VirtualKeyCode keyCode)
        {
            INPUT[] inputList = new InputBuilder().AddKeyPress(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        /// <summary>
        ///     Simulates a key press for each of the specified key codes in the order they are specified.
        /// </summary>
        /// <param name="keyCodes"></param>
        public IKeyboardSimulator KeyPress(params VirtualKeyCode[] keyCodes)
        {
            InputBuilder builder = new InputBuilder();
            KeysPress(builder, keyCodes);
            SendSimulatedInput(builder.ToArray());
            return this;
        }

        /// <summary>
        ///     Simulates a simple modified keystroke like CTRL-C where CTRL is the modifierKey and C is the key.
        ///     The flow is Modifier KeyDown, Key Press, Modifier KeyUp.
        /// </summary>
        /// <param name="modifierKeyCode">The modifier key</param>
        /// <param name="keyCode">The key to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(VirtualKeyCode modifierKeyCode, VirtualKeyCode keyCode)
        {
            ModifiedKeyStroke(new[] { modifierKeyCode }, new[] { keyCode });
            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there are multiple modifiers and one key like CTRL-ALT-C where CTRL and ALT are the
        ///     modifierKeys and C is the key.
        ///     The flow is Modifiers KeyDown in order, Key Press, Modifiers KeyUp in reverse order.
        /// </summary>
        /// <param name="modifierKeyCodes">The list of modifier keys</param>
        /// <param name="keyCode">The key to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<VirtualKeyCode> modifierKeyCodes, VirtualKeyCode keyCode)
        {
            ModifiedKeyStroke(modifierKeyCodes, new[] { keyCode });
            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there is one modifier and multiple keys like CTRL-K-C where CTRL is the modifierKey and
        ///     K and C are the keys.
        ///     The flow is Modifier KeyDown, Keys Press in order, Modifier KeyUp.
        /// </summary>
        /// <param name="modifierKey">The modifier key</param>
        /// <param name="keyCodes">The list of keys to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(VirtualKeyCode modifierKey, IEnumerable<VirtualKeyCode> keyCodes)
        {
            ModifiedKeyStroke(new[] { modifierKey }, keyCodes);
            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there are multiple modifiers and multiple keys like CTRL-ALT-K-C where CTRL and ALT are
        ///     the modifierKeys and K and C are the keys.
        ///     The flow is Modifiers KeyDown in order, Keys Press in order, Modifiers KeyUp in reverse order.
        /// </summary>
        /// <param name="modifierKeyCodes">The list of modifier keys</param>
        /// <param name="keyCodes">The list of keys to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<VirtualKeyCode> modifierKeyCodes,
            IEnumerable<VirtualKeyCode> keyCodes)
        {
            InputBuilder builder = new InputBuilder();
            ModifiersDown(builder, modifierKeyCodes);
            KeysPress(builder, keyCodes);
            ModifiersUp(builder, modifierKeyCodes);

            SendSimulatedInput(builder.ToArray());
            return this;
        }

        /// <summary>
        ///     Manually creates a keystroke and delays the keyup by a specified number of ms.
        ///     The flow is Modifiers KeyDown in order, Keys Press in order, Modifiers KeyUp in reverse order.
        /// </summary>
        /// <param name="modifierKeyCodes">The list of modifier keys</param>
        /// <param name="keyCodes">The list of keys to simulate</param>
        /// <param name="delay">Delay in ms between keydown and keyup of final keyCode. 50ms should be minimum</param>
        public IKeyboardSimulator DelayedModifiedKeyStroke(IEnumerable<VirtualKeyCode> modifierKeyCodes,
            IEnumerable<VirtualKeyCode> keyCodes, int delay)
        {
            foreach (VirtualKeyCode keyCode in modifierKeyCodes)
            {
                INPUT[] inputList = new InputBuilder().AddKeyDown(keyCode).ToArray();
                SendSimulatedInput(inputList);
            }

            foreach (VirtualKeyCode keyCode in keyCodes)
            {
                INPUT[] inputList = new InputBuilder().AddKeyDown(keyCode).ToArray();
                SendSimulatedInput(inputList);
            }

            Thread.Sleep(delay);


            foreach (VirtualKeyCode keyCode in keyCodes.Reverse())
            {
                INPUT[] inputList = new InputBuilder().AddKeyUp(keyCode).ToArray();
                SendSimulatedInput(inputList);
            }

            foreach (VirtualKeyCode keyCode in modifierKeyCodes.Reverse())
            {
                INPUT[] inputList = new InputBuilder().AddKeyUp(keyCode).ToArray();
                SendSimulatedInput(inputList);
            }

            return this;
        }

        /// <summary>
        ///     Calls the Win32 SendInput method with a stream of KeyDown and KeyUp messages in order to simulate uninterrupted text entry
        ///     via the keyboard.
        /// </summary>
        /// <param name="text">The text to be simulated.</param>
        public IKeyboardSimulator TextEntry(string text)
        {
            if (text.Length > uint.MaxValue / 2)
            {
                throw new ArgumentException(
                    string.Format("The text parameter is too long. It must be less than {0} characters.", uint.MaxValue / 2),
                    "text");
            }

            INPUT[] inputList = new InputBuilder().AddCharacters(text).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        /// <summary>
        ///     Simulates a single character text entry via the keyboard.
        /// </summary>
        /// <param name="character">The unicode character to be simulated.</param>
        public IKeyboardSimulator TextEntry(char character)
        {
            INPUT[] inputList = new InputBuilder().AddCharacter(character).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        /// <summary>
        ///     Sleeps the executing thread to create a pause between simulated inputs.
        /// </summary>
        /// <param name="millsecondsTimeout">The number of milliseconds to wait.</param>
        public IKeyboardSimulator Sleep(int millsecondsTimeout)
        {
            Thread.Sleep(millsecondsTimeout);
            return this;
        }

        /// <summary>
        ///     Sleeps the executing thread to create a pause between simulated inputs.
        /// </summary>
        /// <param name="timeout">The time to wait.</param>
        public IKeyboardSimulator Sleep(TimeSpan timeout)
        {
            Thread.Sleep(timeout);
            return this;
        }

        private void ModifiersDown(InputBuilder builder, IEnumerable<VirtualKeyCode> modifierKeyCodes)
        {
            if (modifierKeyCodes == null)
            {
                return;
            }

            foreach (VirtualKeyCode key in modifierKeyCodes)
            {
                builder.AddKeyDown(key);
            }
        }

        private void ModifiersUp(InputBuilder builder, IEnumerable<VirtualKeyCode> modifierKeyCodes)
        {
            if (modifierKeyCodes == null)
            {
                return;
            }

            // Key up in reverse (I miss LINQ)
            Stack<VirtualKeyCode> stack = new Stack<VirtualKeyCode>(modifierKeyCodes);
            while (stack.Count > 0)
            {
                builder.AddKeyUp(stack.Pop());
            }
        }

        private void KeysPress(InputBuilder builder, IEnumerable<VirtualKeyCode> keyCodes)
        {
            if (keyCodes == null)
            {
                return;
            }

            foreach (VirtualKeyCode key in keyCodes)
            {
                builder.AddKeyPress(key);
            }
        }

        /// <summary>
        ///     Sends the list of <see cref="INPUT" /> messages using the <see cref="IInputMessageDispatcher" /> instance.
        /// </summary>
        /// <param name="inputList">The <see cref="System.Array" /> of <see cref="INPUT" /> messages to send.</param>
        private void SendSimulatedInput(INPUT[] inputList) => _messageDispatcher.DispatchInput(inputList);


        #region DIK scancodes

        /// <summary>
        ///     Calls the Win32 SendInput method to simulate a KeyDown.
        /// </summary>
        /// <param name="dikCode">The <see cref="DirectInputKeyCode" /> to press</param>
        public IKeyboardSimulator KeyDown(DirectInputKeyCode dikCode)
        {
            INPUT[] inputList = new InputBuilder().AddKeyDown(dikCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        /// <summary>
        ///     Calls the Win32 SendInput method to simulate a KeyUp.
        /// </summary>
        /// <param name="dikCode">The <see cref="DirectInputKeyCode" /> to lift up</param>
        public IKeyboardSimulator KeyUp(DirectInputKeyCode dikCode)
        {
            INPUT[] inputList = new InputBuilder().AddKeyUp(dikCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        /// <summary>
        ///     Calls the Win32 SendInput method with a KeyDown and KeyUp message in the same input sequence in order to simulate a Key
        ///     PRESS.
        /// </summary>
        /// <param name="dikCode">The <see cref="DirectInputKeyCode" /> to press</param>
        public IKeyboardSimulator KeyPress(DirectInputKeyCode dikCode)
        {
            INPUT[] inputList =
                new InputBuilder()
                    .AddKeyDown(dikCode)
                    .AddKeyUp(dikCode)
                    .ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        /// <summary>
        ///     Calls the Win32 SendInput method with a KeyDown and KeyUp message in the same input sequence in order to simulate a Key
        ///     PRESS.
        /// </summary>
        /// <param name="dikCode">The <see cref="DirectInputKeyCode" /> to press</param>
        /// <param name="delay">Delay in ms between keydown and keyup of final keyCode. 50ms should be minimum</param>
        public IKeyboardSimulator DelayedKeyPress(DirectInputKeyCode dikCode, int delay)
        {
            DelayedKeyPressDown(dikCode);

            Thread.Sleep(delay);

            DelayedKeyPressUp(dikCode);

            return this;
        }

        /// <summary>
        ///     Calls the Win32 SendInput method with a KeyDown message
        /// </summary>
        /// <param name="dikCode">The <see cref="DirectInputKeyCode" /> to press</param>
        public IKeyboardSimulator DelayedKeyPressDown(DirectInputKeyCode dikCode)
        {
            INPUT[] inputList1 = new InputBuilder().AddKeyDown(dikCode).ToArray();
            SendSimulatedInput(inputList1);

            return this;
        }

        /// <summary>
        ///     Calls the Win32 SendInput method with a KeyUp message
        /// </summary>
        /// <param name="dikCode">The <see cref="DirectInputKeyCode" /> to press</param>
        public IKeyboardSimulator DelayedKeyPressUp(DirectInputKeyCode dikCode)
        {
            INPUT[] inputList2 = new InputBuilder().AddKeyUp(dikCode).ToArray();
            SendSimulatedInput(inputList2);

            return this;
        }

        /// <summary>
        ///     Simulates a simple modified keystroke like CTRL-C where CTRL is the modifierKey and C is the key.
        ///     The flow is Modifier KeyDown, Key Press, Modifier KeyUp.
        /// </summary>
        /// <param name="modifierDikCode">The modifier key</param>
        /// <param name="dikCode">The key to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(DirectInputKeyCode modifierDikCode, DirectInputKeyCode dikCode)
        {
            ModifiedKeyStroke(new[] { modifierDikCode }, new[] { dikCode });
            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there are multiple modifiers and one key like CTRL-ALT-C where CTRL and ALT are the
        ///     modifierKeys and C is the key.
        ///     The flow is Modifiers KeyDown in order, Key Press, Modifiers KeyUp in reverse order.
        /// </summary>
        /// <param name="modifierDikCodes">The list of modifier keys</param>
        /// <param name="dikCode">The key to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<DirectInputKeyCode> modifierDikCodes, DirectInputKeyCode dikCode)
        {
            ModifiedKeyStroke(modifierDikCodes, new[] { dikCode });
            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there is one modifier and multiple keys like CTRL-K-C where CTRL is the modifierKey and
        ///     K and C are the keys.
        ///     The flow is Modifier KeyDown, Keys Press in order, Modifier KeyUp.
        /// </summary>
        /// <param name="modifierDikCode">The modifier key</param>
        /// <param name="dikCodes">The list of keys to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(DirectInputKeyCode modifierDikCode, IEnumerable<DirectInputKeyCode> dikCodes)
        {
            ModifiedKeyStroke(new[] { modifierDikCode }, dikCodes);
            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there are multiple modifiers and multiple keys like CTRL-ALT-K-C where CTRL and ALT are
        ///     the modifierKeys and K and C are the keys.
        ///     The flow is Modifiers KeyDown in order, Keys Press in order, Modifiers KeyUp in reverse order.
        /// </summary>
        /// <param name="modifierDikCodes">The list of modifier keys</param>
        /// <param name="dikCodes">The list of keys to simulate</param>
        public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<DirectInputKeyCode> modifierDikCodes,
            IEnumerable<DirectInputKeyCode> dikCodes)
        {
            InputBuilder builder = new InputBuilder();
            ModifiersDown(builder, modifierDikCodes);
            KeysPress(builder, dikCodes);
            ModifiersUp(builder, modifierDikCodes);
            SendSimulatedInput(builder.ToArray());
            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there are multiple modifiers and multiple keys like CTRL-ALT-K-C where CTRL and ALT are
        ///     the modifierKeys and K and C are the keys.
        ///     The flow is Modifiers KeyDown in order, Keys Press in order, Modifiers KeyUp in reverse order.
        /// </summary>
        /// <param name="modifierDikCodes">The list of modifier keys</param>
        /// <param name="dikCode">The list of keys to simulate</param>
        /// <param name="delay">Delay in ms between keydown and keyup of final keyCode. 50ms should be minimum</param>
        public IKeyboardSimulator DelayedModifiedKeyStroke(IEnumerable<DirectInputKeyCode> modifierDikCodes,
            DirectInputKeyCode dikCode, int delay)
        {
            ModifiedKeyStrokeDown(modifierDikCodes, dikCode);

            Thread.Sleep(delay);

            ModifiedKeyStrokeUp(modifierDikCodes, dikCode);

            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there are multiple modifiers and multiple keys like CTRL-ALT-K-C where CTRL and ALT are
        ///     the modifierKeys and K and C are the keys.
        ///     The flow is Modifiers KeyDown in order, Keys Press in order
        /// </summary>
        /// <param name="modifierDikCodes">The list of modifier keys</param>
        /// <param name="dikCode">The list of keys to simulate</param>
        public IKeyboardSimulator ModifiedKeyStrokeDown(IEnumerable<DirectInputKeyCode> modifierDikCodes,
            DirectInputKeyCode dikCode)
        {
            foreach (DirectInputKeyCode keyCode in modifierDikCodes)
            {
                INPUT[] inputList = new InputBuilder().AddKeyDown(keyCode).ToArray();
                SendSimulatedInput(inputList);
            }

            KeyDown(dikCode);

            return this;
        }

        /// <summary>
        ///     Simulates a modified keystroke where there are multiple modifiers and multiple keys like CTRL-ALT-K-C where CTRL and ALT are
        ///     the modifierKeys and K and C are the keys.
        ///     The flow is Keys Press in order, Modifiers KeyUp in reverse order.
        /// </summary>
        /// <param name="modifierDikCodes">The list of modifier keys</param>
        /// <param name="dikCode">The list of keys to simulate</param>
        public IKeyboardSimulator ModifiedKeyStrokeUp(IEnumerable<DirectInputKeyCode> modifierDikCodes,
            DirectInputKeyCode dikCode)
        {
            KeyUp(dikCode);

            foreach (DirectInputKeyCode keyCode in modifierDikCodes.Reverse())
            {
                INPUT[] inputList = new InputBuilder().AddKeyUp(keyCode).ToArray();
                SendSimulatedInput(inputList);
            }

            return this;
        }

        private void ModifiersDown(InputBuilder builder, IEnumerable<DirectInputKeyCode> modifierDikCodes)
        {
            if (modifierDikCodes == null)
            {
                return;
            }

            foreach (DirectInputKeyCode dik in modifierDikCodes)
            {
                builder.AddKeyDown(dik);
            }
        }

        private void ModifiersUp(InputBuilder builder, IEnumerable<DirectInputKeyCode> modifierDikCodes)
        {
            if (modifierDikCodes == null)
            {
                return;
            }

            // Key up in reverse (I miss LINQ)
            Stack<DirectInputKeyCode> stack = new Stack<DirectInputKeyCode>(modifierDikCodes);
            while (stack.Count > 0)
            {
                builder.AddKeyUp(stack.Pop());
            }
        }

        private void KeysPress(InputBuilder builder, IEnumerable<DirectInputKeyCode> dikCodes)
        {
            if (dikCodes == null)
            {
                return;
            }

            foreach (DirectInputKeyCode dik in dikCodes)
            {
                builder.AddKeyPress(dik);
            }
        }

        #endregion
    }
}
