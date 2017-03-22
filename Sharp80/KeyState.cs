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
        public bool TryGetNum(out byte Value)
        {
            switch (Key)
            {
                case KeyCode.NumberPad0:
                case KeyCode.D0:
                    Value = 0;
                    break;
                case KeyCode.NumberPad1:
                case KeyCode.D1:
                    Value = 1;
                    break;
                case KeyCode.NumberPad2:
                case KeyCode.D2:
                    Value = 2;
                    break;
                case KeyCode.NumberPad3:
                case KeyCode.D3:
                    Value = 3;
                    break;
                case KeyCode.NumberPad4:
                case KeyCode.D4:
                    Value = 4;
                    break;
                case KeyCode.NumberPad5:
                case KeyCode.D5:
                    Value = 5;
                    break;
                case KeyCode.NumberPad6:
                case KeyCode.D6:
                    Value = 6;
                    break;
                case KeyCode.NumberPad7:
                case KeyCode.D7:
                    Value = 7;
                    break;
                case KeyCode.NumberPad8:
                case KeyCode.D8:
                    Value = 8;
                    break;
                case KeyCode.NumberPad9:
                case KeyCode.D9:
                    Value = 9;
                    break;
                default:
                    Value = 0;
                    return false;
            }
            return true;
        }
        public char ToHexChar()
        {
            switch (Key)
            {
                case KeyCode.NumberPad0:
                case KeyCode.D0:
                    return '0';
                case KeyCode.NumberPad1:
                case KeyCode.D1:
                    return '1';
                case KeyCode.NumberPad2:
                case KeyCode.D2:
                    return '2';
                case KeyCode.NumberPad3:
                case KeyCode.D3:
                    return '3';
                case KeyCode.NumberPad4:
                case KeyCode.D4:
                    return '4';
                case KeyCode.NumberPad5:
                case KeyCode.D5:
                    return '5';
                case KeyCode.NumberPad6:
                case KeyCode.D6:
                    return '6';
                case KeyCode.NumberPad7:
                case KeyCode.D7:
                    return '7';
                case KeyCode.NumberPad8:
                case KeyCode.D8:
                    return '8';
                case KeyCode.NumberPad9:
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
