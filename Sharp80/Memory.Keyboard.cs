/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;

namespace Sharp80
{
    internal sealed partial class Memory : IMemory
    {
        const int KEYEVENTF_EXTENDEDKEY = 0x01;
        const int KEYEVENTF_KEYUP = 0x02;

        private bool altKeyboardLayouot = false;
        public bool AltKeyboardLayout
        {
            get => altKeyboardLayouot;
            set
            {
                if (altKeyboardLayouot != value)
                {
                    altKeyboardLayouot = value;
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
        private Dictionary<(KeyCode KeyCode, bool Shifted), (VirtualKey VirtualKey, bool Shifted, bool ShiftInverted)> complexMappings = new Dictionary<(KeyCode KeyCode, bool Shifted), (VirtualKey VirtualKey, bool Shifted, bool ShiftInverted)>();

        private bool isLeftShifted = false;
        private bool isRightShifted = false;

        private bool IsShiftedPhysical { get { return isLeftShifted || isRightShifted; } }

        private KeyCode fakedInputKey = KeyCode.None;
        private VirtualKey fakedVirtualKey = VirtualKey.NONE;
        private bool fakedKeyIsShifted;

        private void SetupDXKeyboardMatrix()
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

            AddKey(VirtualKey.D0, 0x3810, 0x01, KeyCode.NumberPad0);
            AddKey(VirtualKey.D1, 0x3810, 0x02, KeyCode.NumberPad1, KeyCode.D1);
            AddKey(VirtualKey.D2, 0x3810, 0x04, KeyCode.NumberPad2);
            AddKey(VirtualKey.D3, 0x3810, 0x08, KeyCode.NumberPad3, KeyCode.D3);
            AddKey(VirtualKey.D4, 0x3810, 0x10, KeyCode.NumberPad4, KeyCode.D4);
            AddKey(VirtualKey.D5, 0x3810, 0x20, KeyCode.NumberPad5, KeyCode.D5);
            AddKey(VirtualKey.D6, 0x3810, 0x40, KeyCode.NumberPad6);
            AddKey(VirtualKey.D7, 0x3810, 0x80, KeyCode.NumberPad7);

            AddKey(VirtualKey.D8,        0x3820, 0x01, KeyCode.NumberPad8);
            AddKey(VirtualKey.D9,        0x3820, 0x02, KeyCode.NumberPad9);
            AddKey(VirtualKey.COLON,     0x3820, 0x04);
            AddKey(VirtualKey.SEMICOLON, 0x3820, 0x08);
            AddKey(VirtualKey.COMMA,     0x3820, 0x10, KeyCode.Comma);
            AddKey(VirtualKey.MINUS,     0x3820, 0x20);
            AddKey(VirtualKey.PERIOD,    0x3820, 0x40, KeyCode.Decimal, KeyCode.Period);
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
            AddComplexMapping(KeyCode.Minus,      VirtualKey.MINUS,     false, VirtualKey.NONE,      false);
            AddComplexMapping(KeyCode.Equals,     VirtualKey.MINUS,     true,  VirtualKey.SEMICOLON, true);
            AddComplexMapping(KeyCode.Capital,    VirtualKey.D0,        true,  VirtualKey.D0,        true);
        }

        private void AddComplexMapping(KeyCode PhysicalKey, VirtualKey VirtualKeyUnshifted, bool VirtualShiftUnshifted, VirtualKey VirtualKeyShifted, bool VirtualShiftShifted)
        {
            complexMappings.Add((PhysicalKey, false), (VirtualKeyUnshifted, VirtualShiftUnshifted, VirtualShiftUnshifted));
            complexMappings.Add((PhysicalKey, true),  (VirtualKeyShifted,   VirtualShiftShifted,   !VirtualShiftShifted));
        }

        private void AddKey(VirtualKey VirtualKey, ushort Address, byte Mask, params KeyCode[] PhysicalKeys)
        {
            keyAddresses.Add(VirtualKey, (Address, Mask, (byte)~Mask));
            foreach (var pk in PhysicalKeys)
                basicMappings.Add(pk, VirtualKey);
        }

        public bool NotifyKeyboardChange(KeyState Key)
        {
            Log.LogDebug("Key Event: " + Key.ToString());

            if (AltKeyboardLayout)
            {
                if (Key.Key == KeyCode.Tab)
                    Key = new KeyState(KeyCode.Up, false, false, false, Key.Pressed);
                else if (Key.Key == KeyCode.Capital)
                    Key = new KeyState(KeyCode.Down, false, false, false, Key.Pressed);
            }

            switch (Key.Key)
            {
                case KeyCode.LeftShift:
                    if (isLeftShifted != Key.Pressed)
                        isLeftShifted = Key.Pressed;
                    if (fakedVirtualKey != VirtualKey.NONE /* && fakedKeyIsShifted != IsShiftedPhysical */)
                        ClearFakedKey();
                    else
                        DoKeyChange(isLeftShifted, VirtualKey.LEFTSHIFT);
                    return true;
                case KeyCode.RightShift:
                    if (isRightShifted != Key.Pressed)
                        isRightShifted = Key.Pressed;
                    if (fakedVirtualKey != VirtualKey.NONE /* && fakedKeyIsShifted != IsShiftedPhysical */)
                        ClearFakedKey();
                    else
                        DoKeyChange(isRightShifted, VirtualKey.RIGHTSHIFT);
                    return true;
                case KeyCode.Capital:
                    if (Key.Released)
                        TurnCapsLockOff();
                    break;
            }

            var k = VirtualKey.NONE;

            if (!basicMappings.TryGetValue(Key.Key, out k))
            {
                if (!complexMappings.TryGetValue((Key.Key, IsShiftedPhysical), out (VirtualKey VirtualKey, bool Shifted, bool ShiftInverted) cm))
                    return false;

                // can we treat this like a normal key?
                if (!cm.ShiftInverted)
                {
                    ClearFakedKey();
                    DoKeyChange(Key.Pressed, cm.VirtualKey);
                }
                else
                {
                    // nope, there's a shift mismatch so we gotta fake it
                    if (Key.Pressed)
                    {
                        ClearFakedKey();
                        if (cm.Shifted && !IsShiftedPhysical)
                        {
                            // we need a shift key and don't have it, so fake it:
                            DoKeyChange(cm.Shifted, VirtualKey.RIGHTSHIFT);
                        }
                        else if (!cm.Shifted)
                        {
                            // we can't have a shift key but we have one, so fake releasing it:
                            DoKeyChange(false, VirtualKey.LEFTSHIFT);
                            DoKeyChange(false, VirtualKey.RIGHTSHIFT);
                        }
                        else
                        {
                            throw new Exception();
                        }
                        SetFakedKey(Key, cm);
                    }
                    else
                    {
                        ClearFakedKey();
                    }
                }
            }
            else
            {
                DoKeyChange(Key.Pressed, k);
            }
            return true;
        }

        private void SetFakedKey(KeyState Key, (VirtualKey VirtualKey, bool Shifted, bool ShiftInverted) Mapping)
        {
            System.Diagnostics.Debug.Assert(Key.Shift == Mapping.Shifted ^ Mapping.ShiftInverted);
            fakedInputKey = Key.Key;
            fakedVirtualKey = Mapping.VirtualKey;
            fakedKeyIsShifted = Mapping.Shifted;
            DoKeyChange(Key.Pressed, Mapping.VirtualKey);
        }

        private void ClearFakedKey()
        {
            if (fakedVirtualKey != VirtualKey.NONE)
            {
                DoKeyChange(false, fakedVirtualKey);
                fakedVirtualKey = VirtualKey.NONE;
            }
            fakedInputKey = KeyCode.None;
            DoKeyChange(isLeftShifted, VirtualKey.LEFTSHIFT);
            DoKeyChange(isRightShifted, VirtualKey.RIGHTSHIFT);
        }

        private void DoKeyChange(bool IsPressed, VirtualKey k)
        {
            if (k != VirtualKey.NONE)
            {
                var m = keyAddresses[k];

                if (IsPressed)
                    mem[m.Address] |= m.KeyMask;
                else
                    mem[m.Address] &= m.InverseMask;
            }
        }

        public void ResetKeyboard(bool LeftShift, bool RightShift)
        {
            mem[0x3801] =
            mem[0x3802] =
            mem[0x3804] =
            mem[0x3808] =
            mem[0x3810] =
            mem[0x3820] =
            mem[0x3840] =
            mem[0x3880] = 0;

            if (LeftShift)
                mem[0x3880] |= 0x01;
            if (RightShift)
                mem[0x3880] |= 0x02;
        }

        private void TurnCapsLockOff()
        {
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