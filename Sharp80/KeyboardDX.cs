/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.DirectInput;

namespace Sharp80
{
    internal delegate void KeyPressedDelegate(KeyState KeyState);

    internal sealed class KeyboardDX : IKeyboard, IDisposable
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
        
        private KeyCode repeatKey = KeyCode.None;
        private uint repeatKeyCount = 0;

        public KeyboardDX()
        {
            // Initialize DirectInput
            var directInput = new DirectInput();

            keyboard = new Keyboard(directInput);

            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();
        }

        public async Task Start(float RefreshRateHz, KeyPressedDelegate Callback, CancellationToken StopToken)
        {
            var delay = TimeSpan.FromTicks((int)(10_000_000f / RefreshRateHz));
            await Poll(delay, Callback, (int)(RefreshRateHz / 2), StopToken);
        }
        private async Task Poll(TimeSpan Delay, KeyPressedDelegate Callback, int RepeatThreshold, CancellationToken StopToken)
        {
            while (!StopToken.IsCancellationRequested)
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
                        var keyCode = (KeyCode)d.Key;
                        if (d.IsPressed)
                        {
                            switch (keyCode)
                            {
                                case KeyCode.Up:
                                case KeyCode.Down:
                                case KeyCode.Left:
                                case KeyCode.Right:
                                case KeyCode.PageUp:
                                case KeyCode.PageDown:
                                case KeyCode.F8:
                                case KeyCode.F9:
                                case KeyCode.F10:
                                    if (repeatKey != keyCode)
                                    {
                                        repeatKey = keyCode;
                                        repeatKeyCount = 0;
                                    }
                                    break;
                            }
                        }
                        else if (keyCode == repeatKey)
                        {
                            // repeat key is released
                            repeatKey = KeyCode.None;
                        }

                        Callback(new KeyState(keyCode, IsShifted, IsControlPressed, IsAltPressed, d.IsPressed));
                    }
                }
                if (repeatKey != KeyCode.None && ++repeatKeyCount > RepeatThreshold)
                    Callback(new KeyState(repeatKey, IsShifted, IsControlPressed, IsAltPressed, true, true));

                await Task.Delay(Delay, StopToken);
            }
        }
        public bool Enabled { get { return enabled; }
        set
            {
                // Throw away strays that may have accumulated
                keyboard.GetBufferedData();
                enabled = value;
            }
        }

        public bool IsPressed(KeyCode Key) => keyboard.GetCurrentState().IsPressed((Key)Key);
        
        public void Refresh()
        {
            repeatKey = KeyCode.None;

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