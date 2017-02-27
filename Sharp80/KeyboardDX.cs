using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SharpDX;
using SharpDX.DirectInput;

namespace Sharp80
{
    internal sealed class KeyboardDX : IDisposable
    {
        private Keyboard keyboard;

        public KeyboardDX()
        {
            // Initialize DirectInput
            var directInput = new DirectInput();

            keyboard = new Keyboard(directInput);

            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();
        }
        public void Acquire() { keyboard.Acquire(); }
        public void Unacquire() { keyboard.Unacquire(); }
        public KeyboardUpdate[] Poll()
        {
            return keyboard.GetBufferedData();
        }
        public bool IsPressed(Key Key)
        {
            return keyboard.GetCurrentState().IsPressed(Key);
        }
        public void UpdateModifierKeys(out bool LeftShift, out bool RightShift, out bool LeftAlt, out bool RightAlt, out bool LeftControl, out bool RightControl)
        {
            var cs = keyboard.GetCurrentState();
            LeftShift =    IsPressed(Key.LeftShift);
            RightShift =   IsPressed(Key.RightShift);
            LeftAlt =      IsPressed(Key.LeftAlt);
            RightAlt =     IsPressed(Key.RightAlt);
            LeftControl =  IsPressed(Key.LeftControl);
            RightControl = IsPressed(Key.RightControl);
        }
        public void Dispose()
        {
            if (!keyboard.IsDisposed)
            {
                keyboard.Dispose();
            }
        }
        public bool IsDisposed { get { return keyboard.IsDisposed; } }
    }
}