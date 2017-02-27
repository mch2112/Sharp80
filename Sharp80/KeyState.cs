/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

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
        public bool Released { get; }
        public bool Repeat { get; }
        public KeyState(KeyCode Key, bool Shift, bool Control, bool Alt, bool Pressed, bool Released, bool Repeat = false)
        {
            this.Key = Key;
            this.Shift = Shift;
            this.Control = Control;
            this.Alt = Alt;
            this.Pressed = Pressed;
            this.Released = Released;
            this.Repeat = Repeat;

            if (this.Pressed == this.Released)
                throw new Exception("WTF");
        }
        public bool IsUnmodified { get { return !Alt && !Control; } }
        public char ToChar()
        {
            switch (this.Key)
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
    }
}
