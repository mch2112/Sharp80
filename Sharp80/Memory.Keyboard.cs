/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharp80
{
    internal sealed partial class Memory : IMemory
    {
        private bool altKeyboardLayout = false;
        public bool AltKeyboardLayout
        {
            get => altKeyboardLayout;
            set
            {
                if (altKeyboardLayout != value)
                {
                    altKeyboardLayout = value;
                    ResetKeyboard(false, false);
                }
            }
        }

        private enum VirtualKey
        {
            NONE, D1, D2, D3, D4, D5, D6, D7, D8, D9, D0, COLON, MINUS, BREAK, UPARROW, Q, W, E, R, T, Y, U, I, O, P, AT, LEFTARROW, RIGHTARROW,
            DOWNARROW, A, S, D, F, G, H, J, K, L, SEMICOLON, ENTER, CLEAR, LEFTSHIFT, Z, X, C, V, B, N, M, COMMA, PERIOD, SLASH, RIGHTSHIFT, SPACEBAR
        }
        private Dictionary<VirtualKey, (ushort Address, byte KeyMask, byte InverseMask)> keyAddresses;
        private Dictionary<KeyCode, VirtualKey> basicMappings = new Dictionary<KeyCode, VirtualKey>();
        private Dictionary<(KeyCode KeyCode, bool Shifted), (VirtualKey VirtualKey, bool Shifted)> complexMappings = new Dictionary<(KeyCode KeyCode, bool Shifted), (VirtualKey VirtualKey, bool Shifted)>();

        private List<KeyState> PressedKeys = new List<KeyState>();

        private void SetupKeyboardMatrix()
        {
            keyAddresses = new Dictionary<VirtualKey, (ushort Address, byte KeyMask, byte InverseMask)>();

            AddKey(VirtualKey.AT, 0x3801, 0x01, KeyCode.Backslash);
            AddKey(VirtualKey.A, 0x3801, 0x02, KeyCode.A);
            AddKey(VirtualKey.B, 0x3801, 0x04, KeyCode.B);
            AddKey(VirtualKey.C, 0x3801, 0x08, KeyCode.C);
            AddKey(VirtualKey.D, 0x3801, 0x10, KeyCode.D);
            AddKey(VirtualKey.E, 0x3801, 0x20, KeyCode.E);
            AddKey(VirtualKey.F, 0x3801, 0x40, KeyCode.F);
            AddKey(VirtualKey.G, 0x3801, 0x80, KeyCode.G);

            AddKey(VirtualKey.H, 0x3802, 0x01, KeyCode.H);
            AddKey(VirtualKey.I, 0x3802, 0x02, KeyCode.I);
            AddKey(VirtualKey.J, 0x3802, 0x04, KeyCode.J);
            AddKey(VirtualKey.K, 0x3802, 0x08, KeyCode.K);
            AddKey(VirtualKey.L, 0x3802, 0x10, KeyCode.L);
            AddKey(VirtualKey.M, 0x3802, 0x20, KeyCode.M);
            AddKey(VirtualKey.N, 0x3802, 0x40, KeyCode.N);
            AddKey(VirtualKey.O, 0x3802, 0x80, KeyCode.O);

            AddKey(VirtualKey.P, 0x3804, 0x01, KeyCode.P);
            AddKey(VirtualKey.Q, 0x3804, 0x02, KeyCode.Q);
            AddKey(VirtualKey.R, 0x3804, 0x04, KeyCode.R);
            AddKey(VirtualKey.S, 0x3804, 0x08, KeyCode.S);
            AddKey(VirtualKey.T, 0x3804, 0x10, KeyCode.T);
            AddKey(VirtualKey.U, 0x3804, 0x20, KeyCode.U);
            AddKey(VirtualKey.V, 0x3804, 0x40, KeyCode.V);
            AddKey(VirtualKey.W, 0x3804, 0x80, KeyCode.W);

            AddKey(VirtualKey.X, 0x3808, 0x01, KeyCode.X);
            AddKey(VirtualKey.Y, 0x3808, 0x02, KeyCode.Y);
            AddKey(VirtualKey.Z, 0x3808, 0x04, KeyCode.Z);

            AddKey(VirtualKey.D0, 0x3810, 0x01);
            AddKey(VirtualKey.D1, 0x3810, 0x02, KeyCode.D1);
            AddKey(VirtualKey.D2, 0x3810, 0x04);
            AddKey(VirtualKey.D3, 0x3810, 0x08, KeyCode.D3);
            AddKey(VirtualKey.D4, 0x3810, 0x10, KeyCode.D4);
            AddKey(VirtualKey.D5, 0x3810, 0x20, KeyCode.D5);
            AddKey(VirtualKey.D6, 0x3810, 0x40);
            AddKey(VirtualKey.D7, 0x3810, 0x80);

            AddKey(VirtualKey.D8,        0x3820, 0x01);
            AddKey(VirtualKey.D9,        0x3820, 0x02);
            AddKey(VirtualKey.COLON,     0x3820, 0x04);
            AddKey(VirtualKey.SEMICOLON, 0x3820, 0x08);
            AddKey(VirtualKey.COMMA,     0x3820, 0x10, KeyCode.Comma);
            AddKey(VirtualKey.MINUS,     0x3820, 0x20);
            AddKey(VirtualKey.PERIOD,    0x3820, 0x40, KeyCode.Period);
            AddKey(VirtualKey.SLASH,     0x3820, 0x80, KeyCode.Slash);

            AddKey(VirtualKey.ENTER,      0x3840, 0x01, KeyCode.Return, KeyCode.NumberPadEnter);
            AddKey(VirtualKey.CLEAR,      0x3840, 0x02, KeyCode.Home,   KeyCode.Grave);
            AddKey(VirtualKey.BREAK,      0x3840, 0x04, KeyCode.Escape);
            AddKey(VirtualKey.UPARROW,    0x3840, 0x08, KeyCode.Up);
            AddKey(VirtualKey.DOWNARROW,  0x3840, 0x10, KeyCode.Down);
            AddKey(VirtualKey.LEFTARROW,  0x3840, 0x20, KeyCode.Left,   KeyCode.Back, KeyCode.LeftBracket, KeyCode.Delete);
            AddKey(VirtualKey.RIGHTARROW, 0x3840, 0x40, KeyCode.Right,  KeyCode.Tab,  KeyCode.RightBracket);
            AddKey(VirtualKey.SPACEBAR,   0x3840, 0x80, KeyCode.Space);

            AddKey(VirtualKey.LEFTSHIFT,  0x3880, 0x01);
            AddKey(VirtualKey.RIGHTSHIFT, 0x3880, 0x02);

            AddComplexMapping(KeyCode.D2,         VirtualKey.D2,        false, VirtualKey.AT,        false);
            AddComplexMapping(KeyCode.D6,         VirtualKey.D6,        false, VirtualKey.NONE,      false);
            AddComplexMapping(KeyCode.D7,         VirtualKey.D7,        false, VirtualKey.D6,        true);
            AddComplexMapping(KeyCode.D8,         VirtualKey.D8,        false, VirtualKey.COLON,     true);
            AddComplexMapping(KeyCode.D9,         VirtualKey.D9,        false, VirtualKey.D8,        true);
            AddComplexMapping(KeyCode.D0,         VirtualKey.D0,        false, VirtualKey.D9,        true);
            AddComplexMapping(KeyCode.Apostrophe, VirtualKey.D7,        true,  VirtualKey.D2,        true);
            AddComplexMapping(KeyCode.Semicolon,  VirtualKey.SEMICOLON, false, VirtualKey.COLON,     false);
            AddComplexMapping(KeyCode.Minus,      VirtualKey.MINUS,     false, VirtualKey.MINUS,     false);
            AddComplexMapping(KeyCode.Equals,     VirtualKey.MINUS,     true,  VirtualKey.SEMICOLON, true);
            AddComplexMapping(KeyCode.Capital,    VirtualKey.D0,        true,  VirtualKey.D0,        true);

            AddComplexMapping(KeyCode.Decimal,    VirtualKey.PERIOD,    false, VirtualKey.PERIOD,    false);
            AddComplexMapping(KeyCode.Add,        VirtualKey.SEMICOLON, true,  VirtualKey.SEMICOLON, true);
            AddComplexMapping(KeyCode.Subtract,   VirtualKey.MINUS,     false, VirtualKey.MINUS,     false);
            AddComplexMapping(KeyCode.Multiply,   VirtualKey.COLON,     true,  VirtualKey.COLON,     true);
            AddComplexMapping(KeyCode.Divide,     VirtualKey.SLASH,     false, VirtualKey.SLASH,     false);

            AddComplexMapping(KeyCode.NumberPad0, VirtualKey.D0,        false, VirtualKey.D0,        false);
            AddComplexMapping(KeyCode.NumberPad1, VirtualKey.D1,        false, VirtualKey.D1,        false);
            AddComplexMapping(KeyCode.NumberPad2, VirtualKey.D2,        false, VirtualKey.D2,        false);
            AddComplexMapping(KeyCode.NumberPad3, VirtualKey.D3,        false, VirtualKey.D3,        false);
            AddComplexMapping(KeyCode.NumberPad4, VirtualKey.D4,        false, VirtualKey.D4,        false);
            AddComplexMapping(KeyCode.NumberPad5, VirtualKey.D5,        false, VirtualKey.D5,        false);
            AddComplexMapping(KeyCode.NumberPad6, VirtualKey.D6,        false, VirtualKey.D6,        false);
            AddComplexMapping(KeyCode.NumberPad7, VirtualKey.D7,        false, VirtualKey.D7,        false);
            AddComplexMapping(KeyCode.NumberPad8, VirtualKey.D8,        false, VirtualKey.D8,        false);
            AddComplexMapping(KeyCode.NumberPad9, VirtualKey.D9,        false, VirtualKey.D9,        false);
        }

        /// <summary>
        /// For a given PC Key (the "Physical Key") we can choose which virtual key it maps to when shifted or unshifted, and whether
        /// that virtual key itself is shifted or unshifted.
        /// </summary>
        private void AddComplexMapping(KeyCode PhysicalKey, VirtualKey VirtualKeyUnshifted, bool VirtualShiftUnshifted, VirtualKey VirtualKeyShifted, bool VirtualShiftShifted)
        {
            complexMappings.Add((PhysicalKey, false), (VirtualKeyUnshifted, VirtualShiftUnshifted));
            complexMappings.Add((PhysicalKey, true),  (VirtualKeyShifted,   VirtualShiftShifted));
        }

        private void AddKey(VirtualKey VirtualKey, ushort Address, byte Mask, params KeyCode[] PhysicalKeys)
        {
            keyAddresses.Add(VirtualKey, (Address, Mask, (byte)~Mask));
            foreach (var pk in PhysicalKeys)
                basicMappings.Add(pk, VirtualKey);
        }

        public bool NotifyKeyboardChange(KeyState Key)
        {
            // The basic strategy is that rather than incrementally updating keyboard memory with each
            // keystroke, we collect all the keys currently pressed and contruct the keyboard memory
            // status. This is needed because the remapping of keys leads to too many weird cases (example:
            // Shift-9 (left parenthesis) maps to virtual Shift-8. If the user releases the shift key before the 9 key, we have 
            // to decide what to do and things get complicated, especially if other keys are also pressed.
            // There is no analog to this in the actual TRS-80.)

            Log.LogDebug("Key Event: " + Key.ToString());

            if (Key.Key == KeyCode.Capital && Key.Released)
                TurnCapsLockOff();

            if (AltKeyboardLayout)
            {
                if (Key.Key == KeyCode.Tab)
                    Key = new KeyState(KeyCode.Up, false, false, false, Key.Pressed);
                else if (Key.Key == KeyCode.Capital)
                    Key = new KeyState(KeyCode.Down, false, false, false, Key.Pressed);
            }

            // if the key is already in the bag, overwrite it
            PressedKeys.RemoveAll(k => k.Key == Key.Key);

            if (Key.Pressed)
                PressedKeys.Add(Key);
            
            UpdateVirtualKeyboard();

            return true;
        }
        private void UpdateVirtualKeyboard()
        {
            ResetVirtualKeyboard();

            if (PressedKeys.Any(k => k.SyntheticShift))
            {
                // only one of these at a time
                var kk = PressedKeys.First(k => k.SyntheticShift);
                SetShiftState(kk.Shift, false, false);
                ProcessKey(kk, kk.Shift);
            }
            else
            {
                bool leftShift = PressedKeys.Any(k => k.Key == KeyCode.LeftShift);
                bool rightShift = PressedKeys.Any(k => k.Key == KeyCode.RightShift);

                SetShiftState(leftShift, rightShift, false);

                foreach (var k in PressedKeys)
                    if (k.Key != KeyCode.LeftShift && k.Key != KeyCode.RightShift)
                        ProcessKey(k, leftShift || rightShift);
            }
        }
        private void ProcessKey(KeyState k, bool KeyboardIsShifted)
        {
            System.Diagnostics.Debug.Assert(k.Pressed);
            if (basicMappings.TryGetValue(k.Key, out VirtualKey kk))
                DoKeyChange(true, kk);
            else
                DoComplexKeyChange(k, KeyboardIsShifted);
        }
        private bool DoComplexKeyChange(KeyState Key, bool KeyboardIsShifted)
        {
            if (!complexMappings.TryGetValue((Key.Key, Key.Shift), out (VirtualKey VirtualKey, bool Shifted) cm))
                return false;

            if (cm.Shifted && !KeyboardIsShifted)
            {
                // we need a shift key and don't have it, so fake it:
                SetShiftState(true, true, false);
            }
            else if (!cm.Shifted && KeyboardIsShifted)
            {
                // we can't have a shift key but we have one, so fake releasing it:
                SetShiftState(false, false, false);
            }
            DoKeyChange(true, cm.VirtualKey);
            return true;
        }
        private bool DoKeyChange(bool IsPressed, VirtualKey k)
        {
            //System.Diagnostics.Debug.WriteLine($"Virtual Key {k} Pressed: {IsPressed} KeyboardShiftState {mem[0x3880]}");
            if (k == VirtualKey.NONE)
            {
                return false;
            }
            else
            {
                var m = keyAddresses[k];

                if (IsPressed)
                    mem[m.Address] |= m.KeyMask;
                else
                    mem[m.Address] &= m.InverseMask;
                return true;
            }
        }

        public void ResetKeyboard(bool LeftShiftPressed, bool RightShiftPressed)
        {
            ResetVirtualKeyboard();
            PressedKeys.Clear();
            SetShiftState(LeftShiftPressed, RightShiftPressed, true);
        }
        private void SetShiftState(bool? LeftShiftPressed, bool? RightShiftPressed, bool UpdatePressedKeys)
        {
            if (LeftShiftPressed == true)
            {
                if (UpdatePressedKeys && !PressedKeys.Any(k => k.Key == KeyCode.LeftShift))
                    PressedKeys.Add(new KeyState(KeyCode.LeftShift, false, false, false, true));
                DoKeyChange(true, VirtualKey.LEFTSHIFT);
            }
            else if (LeftShiftPressed == false)
            {
                if (UpdatePressedKeys)
                    PressedKeys.RemoveAll(k => k.Key == KeyCode.LeftShift);
                DoKeyChange(false, VirtualKey.LEFTSHIFT);
            }
            if (RightShiftPressed == true)
            {
                if (UpdatePressedKeys)
                    PressedKeys.Add(new KeyState(KeyCode.RightShift, false, false, false, true));
                DoKeyChange(true, VirtualKey.RIGHTSHIFT);
            }
            else if (RightShiftPressed == false)
            {
                if (UpdatePressedKeys)
                    PressedKeys.RemoveAll(k => k.Key == KeyCode.RightShift);
                DoKeyChange(false, VirtualKey.RIGHTSHIFT);
            }
        }
        private void ResetVirtualKeyboard()
        {
            mem[0x3801] =
            mem[0x3802] =
            mem[0x3804] =
            mem[0x3808] =
            mem[0x3810] =
            mem[0x3820] =
            mem[0x3840] =
            mem[0x3880] = 0;
        }

        private void TurnCapsLockOff()
        {
            const int KEYEVENTF_EXTENDEDKEY = 0x01;
            const int KEYEVENTF_KEYUP = 0x02;

            // Annoying that it turns on when doing virtual shift-zero.
            if (System.Windows.Forms.Control.IsKeyLocked(System.Windows.Forms.Keys.CapsLock))
            {
                NativeMethods.keybd_event(0x14, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                NativeMethods.keybd_event(0x14, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP,
                    (UIntPtr)0);
            }
        }
    }
}