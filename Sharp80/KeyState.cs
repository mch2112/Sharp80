/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal struct KeyState
    {
        public KeyCode Key { get; }
        public bool Shift { get; }
        public bool Control { get; }
        public bool Alt { get; }
        public bool Pressed { get; }
        public bool Released => !Pressed;
        public bool Repeat { get; }
        public bool IsUnmodified => !Alt && !Control; 

        public KeyState(KeyCode Key, bool Shift, bool Control, bool Alt, bool Pressed, bool Repeat = false)
        {
            this.Key = Key;
            this.Shift = Shift;
            this.Control = Control;
            this.Alt = Alt;
            this.Pressed = Pressed;
            this.Repeat = Repeat;

            System.Diagnostics.Debug.Assert(Pressed == !Released);
        }
        
        public char ToHexChar()
        {
            switch (Key)
            {
                case KeyCode.D0:
                    return '0';
                case KeyCode.D1:
                    return '1';
                case KeyCode.D2:
                    return '2';
                case KeyCode.D3:
                    return '3';
                case KeyCode.D4:
                    return '4';
                case KeyCode.D5:
                    return '5';
                case KeyCode.D6:
                    return '6';
                case KeyCode.D7:
                    return '7';
                case KeyCode.D8:
                    return '8';
                case KeyCode.D9:
                    return '9';
                case KeyCode.A:
                    return 'A';
                case KeyCode.B:
                    return 'B';
                case KeyCode.C:
                    return 'C';
                case KeyCode.D:
                    return 'D';
                case KeyCode.E:
                    return 'E';
                case KeyCode.F:
                    return 'F';
                default:
                    return '\0';
            }
        }
        public override string ToString()
        {
            return Key.ToString() +
                   (Pressed ? " Pressed" : " Released") +
                   (Shift ? " Shft" : "") +
                   (Alt ? " Alt" : "") +
                   (Control ? " Ctrl" : "");
        }
    }
}
