/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections;
using System.Collections.Generic;

using SharpDX.DirectInput;

namespace Sharp80
{
    internal sealed class KeyboardDX : IDisposable, IKeyboard
    {
        private Keyboard keyboard;

        public bool IsShifted => LeftShiftPressed || RightShiftPressed;
        public bool IsControlPressed => leftControlPressed || rightControlPressed;
        public bool IsAltPressed => leftAltPressed || rightAltPressed;
        public bool LeftShiftPressed { get; private set; }
        public bool RightShiftPressed { get; private set; }

        private bool enabled = false;
        private bool leftControlPressed = false;
        private bool rightControlPressed = false;
        private bool leftAltPressed = false;
        private bool rightAltPressed = false;

        public KeyboardDX()
        {
            // Initialize DirectInput
            var directInput = new DirectInput();

            keyboard = new Keyboard(directInput);

            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();
        }
        
        public bool Enabled { get { return enabled; }
        set
            {
                enabled = value;

                // Throw away strays that may have accumulated
                if (enabled)
                    keyboard.GetBufferedData();
            }
        }

        // required explicit interface implementation
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public IEnumerator<KeyState> GetEnumerator()
        {
            var data = keyboard.GetBufferedData();

            foreach (var d in data)
            {
                switch (d.Key)
                {
                    case Key.LeftShift: LeftShiftPressed = d.IsPressed; break;
                    case Key.RightShift: RightShiftPressed = d.IsPressed; break;
                    case Key.LeftControl: leftControlPressed = d.IsPressed; break;
                    case Key.RightControl: rightControlPressed = d.IsPressed; break;
                    case Key.LeftAlt: leftAltPressed = d.IsPressed; break;
                    case Key.RightAlt: rightAltPressed = d.IsPressed; break;
                }
                if (Enabled)
                {
                    yield return new KeyState((KeyCode)d.Key, IsShifted, IsControlPressed, IsAltPressed, d.IsPressed);
                }
            }
        }
        public bool IsPressed(KeyCode Key)
        {
            return keyboard.GetCurrentState().IsPressed((Key)Key);
        }
        public void Refresh()
        {
            var cs = keyboard.GetCurrentState();
            LeftShiftPressed =    cs.IsPressed(Key.LeftShift);
            RightShiftPressed =   cs.IsPressed(Key.RightShift);
            leftAltPressed =      cs.IsPressed(Key.LeftAlt);
            rightAltPressed =     cs.IsPressed(Key.RightAlt);
            leftControlPressed =  cs.IsPressed(Key.LeftControl);
            rightControlPressed = cs.IsPressed(Key.RightControl);
        }
        public void Dispose()
        {
            if (!keyboard.IsDisposed)
            {
                keyboard.Unacquire();
                keyboard.Dispose();
            }
        }
    }
}