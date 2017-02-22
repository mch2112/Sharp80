using System;
using System.Collections.Generic;
using SharpDX.DirectInput;

namespace Sharp80
{
    internal sealed partial class Memory : IMemory
    {

        const int KEYEVENTF_EXTENDEDKEY = 0x1;
        const int KEYEVENTF_KEYUP = 0x2;
        
        private enum VirtualKey
        {
            NONE, D1, D2, D3, D4, D5, D6, D7, D8, D9, D0, COLON, MINUS, BREAK, UPARROW, Q, W, E, R, T, Y, U, I, O, P, AT, LEFTARROW, RIGHTARROW, 
            DOWNARROW, A, S, D, F, G, H, J, K, L, SEMICOLON, ENTER, CLEAR, LEFTSHIFT, Z, X, C, V, B, N, M, COMMA, PERIOD, SLASH, RIGHTSHIFT, SPACEBAR
        }

        private Dictionary<VirtualKey, Tuple<ushort, byte, byte>> keyAddresses;
        private Dictionary<Key, VirtualKey> basicMappings = new Dictionary<Key, VirtualKey>();
        private Dictionary<Tuple<Key, bool>, Tuple<VirtualKey, bool>> complexMappings = new Dictionary<Tuple<Key, bool>, Tuple<VirtualKey, bool>>();

        private bool isLeftShifted = false;
        private bool isRightShifted = false;

        private bool IsShiftedPhysical { get { return isLeftShifted || isRightShifted;  } }

        private Key fakedInputKey          = Key.Unknown;
        private VirtualKey fakedVirtualKey = VirtualKey.NONE;
        
        private void SetupDXKeyboardMatrix()
        {
            keyAddresses = new Dictionary<VirtualKey, Tuple<ushort, byte, byte>>();

            AddKey(VirtualKey.AT, 0x3801, 0x01, Key.Backslash);
            AddKey(VirtualKey.A, 0x3801, 0x02, Key.A);
            AddKey(VirtualKey.B, 0x3801, 0x04, Key.B);
            AddKey(VirtualKey.C, 0x3801, 0x08, Key.C);
            AddKey(VirtualKey.D, 0x3801, 0x10, Key.D);
            AddKey(VirtualKey.E, 0x3801, 0x20, Key.E);
            AddKey(VirtualKey.F, 0x3801, 0x40, Key.F);
            AddKey(VirtualKey.G, 0x3801, 0x80, Key.G);

            AddKey(VirtualKey.H, 0x3802, 0x01, Key.H);
            AddKey(VirtualKey.I, 0x3802, 0x02, Key.I);
            AddKey(VirtualKey.J, 0x3802, 0x04, Key.J);
            AddKey(VirtualKey.K, 0x3802, 0x08, Key.K);
            AddKey(VirtualKey.L, 0x3802, 0x10, Key.L);
            AddKey(VirtualKey.M, 0x3802, 0x20, Key.M);
            AddKey(VirtualKey.N, 0x3802, 0x40, Key.N);
            AddKey(VirtualKey.O, 0x3802, 0x80, Key.O);

            AddKey(VirtualKey.P, 0x3804, 0x01, Key.P);
            AddKey(VirtualKey.Q, 0x3804, 0x02, Key.Q);
            AddKey(VirtualKey.R, 0x3804, 0x04, Key.R);
            AddKey(VirtualKey.S, 0x3804, 0x08, Key.S);
            AddKey(VirtualKey.T, 0x3804, 0x10, Key.T);
            AddKey(VirtualKey.U, 0x3804, 0x20, Key.U);
            AddKey(VirtualKey.V, 0x3804, 0x40, Key.V);
            AddKey(VirtualKey.W, 0x3804, 0x80, Key.W);

            AddKey(VirtualKey.X, 0x3808, 0x01, Key.X);
            AddKey(VirtualKey.Y, 0x3808, 0x02, Key.Y);
            AddKey(VirtualKey.Z, 0x3808, 0x04, Key.Z);

            AddKey(VirtualKey.D0, 0x3810, 0x01, Key.NumberPad0);
            AddKey(VirtualKey.D1, 0x3810, 0x02, Key.D1);
            AddKey(VirtualKey.D2, 0x3810, 0x04, Key.NumberPad2);
            AddKey(VirtualKey.D3, 0x3810, 0x08, Key.NumberPad3, Key.D3);
            AddKey(VirtualKey.D4, 0x3810, 0x10, Key.NumberPad4, Key.D4);
            AddKey(VirtualKey.D5, 0x3810, 0x20, Key.NumberPad5, Key.D5);
            AddKey(VirtualKey.D6, 0x3810, 0x40, Key.NumberPad6);
            AddKey(VirtualKey.D7, 0x3810, 0x80, Key.NumberPad7);

            AddKey(VirtualKey.D8, 0x3820, 0x01, Key.NumberPad8);
            AddKey(VirtualKey.D9, 0x3820, 0x02, Key.NumberPad9);
            AddKey(VirtualKey.COLON, 0x3820, 0x04);
            AddKey(VirtualKey.SEMICOLON, 0x3820, 0x08);
            AddKey(VirtualKey.COMMA, 0x3820, 0x10, Key.Comma);
            AddKey(VirtualKey.MINUS, 0x3820, 0x20);
            AddKey(VirtualKey.PERIOD, 0x3820, 0x40, Key.Period);
            AddKey(VirtualKey.SLASH, 0x3820, 0x80, Key.Slash);

            AddKey(VirtualKey.ENTER, 0x3840, 0x01, Key.Return);
            AddKey(VirtualKey.CLEAR, 0x3840, 0x02, Key.Home);
            AddKey(VirtualKey.BREAK, 0x3840, 0x04, Key.Escape);
            AddKey(VirtualKey.UPARROW, 0x3840, 0x08, Key.Up);
            AddKey(VirtualKey.DOWNARROW, 0x3840, 0x10, Key.Down);
            AddKey(VirtualKey.LEFTARROW, 0x3840, 0x20, Key.Left, Key.Back, Key.LeftBracket, Key.Delete);
            AddKey(VirtualKey.RIGHTARROW, 0x3840, 0x40, Key.Right, Key.Tab, Key.RightBracket);
            AddKey(VirtualKey.SPACEBAR, 0x3840, 0x80, Key.Space);

            AddKey(VirtualKey.LEFTSHIFT, 0x3880, 0x01);
            AddKey(VirtualKey.RIGHTSHIFT, 0x3880, 0x02);

            AddComplexMapping(Key.D2, VirtualKey.D2, false, VirtualKey.AT, false);
            AddComplexMapping(Key.D6, VirtualKey.D6, false, VirtualKey.NONE, false);
            AddComplexMapping(Key.D7, VirtualKey.D7, false, VirtualKey.D6, true);
            AddComplexMapping(Key.D8, VirtualKey.D8, false, VirtualKey.COLON, true);
            AddComplexMapping(Key.D9, VirtualKey.D9, false, VirtualKey.D8, true);
            AddComplexMapping(Key.D0, VirtualKey.D0, false, VirtualKey.D9, true);
            AddComplexMapping(Key.Apostrophe, VirtualKey.D7, true, VirtualKey.D2, true);
            AddComplexMapping(Key.Semicolon, VirtualKey.SEMICOLON, false, VirtualKey.COLON, false);
            AddComplexMapping(Key.Minus, VirtualKey.MINUS, false, VirtualKey.NONE, false);
            AddComplexMapping(Key.Equals, VirtualKey.MINUS, true, VirtualKey.SEMICOLON, true);
            AddComplexMapping(Key.Capital, VirtualKey.D0, true, VirtualKey.D0, true);
        }

        private void AddComplexMapping(Key PhysicalKey, VirtualKey VirtualKeyUnshifted, bool VirtualShiftUnshifted, VirtualKey VirtualKeyShifted, bool VirtualShiftShifted)
        {
            complexMappings.Add(new Tuple<Key, bool>(PhysicalKey, false), new Tuple<VirtualKey, bool>(VirtualKeyUnshifted, VirtualShiftUnshifted));
            complexMappings.Add(new Tuple<Key, bool>(PhysicalKey, true), new Tuple<VirtualKey, bool>(VirtualKeyShifted, VirtualShiftShifted));
        }

        private void AddKey(VirtualKey VirtualKey, ushort Address, byte Mask, params Key[] PhysicalKeys)
        {
            keyAddresses.Add(VirtualKey, new Tuple<ushort, byte, byte>(Address, Mask, (byte) ~Mask));
            foreach (var pk in PhysicalKeys)
                basicMappings.Add(pk, VirtualKey);
        }

        public void NotifyKeyboardChange(Key Key, bool IsPressed)
        {
            if (Key == Key.LeftShift)
            {
                isLeftShifted = IsPressed;
            }
            if (Key == Key.RightShift)
            {
                isRightShifted = IsPressed;
            }

            DoKeyChange(isLeftShifted, VirtualKey.LEFTSHIFT);
            DoKeyChange(isRightShifted, VirtualKey.RIGHTSHIFT);

            if (Key == Key.LeftShift || Key == Key.RightShift)
                return;
            
            if (Key == Key.Capital && IsPressed == false)
                TurnCapsLockOff();

            VirtualKey k = VirtualKey.NONE;

            if (fakedInputKey != Key.Unknown)
            {
                bool ret = Key == fakedInputKey;

                DoKeyChange(false, fakedVirtualKey);
                fakedInputKey = Key.Unknown;
                fakedVirtualKey = VirtualKey.NONE;

                if (ret)
                    return;
            }

            if (!basicMappings.TryGetValue(Key, out k))
            {
                if (!IsPressed)
                    return;

                if (!complexMappings.TryGetValue(new Tuple<Key, bool>(Key, IsShiftedPhysical), out Tuple<VirtualKey, bool> cm))
                    return;

                DoKeyChange(cm.Item2, VirtualKey.RIGHTSHIFT);
                if (!cm.Item2)
                    DoKeyChange(false, VirtualKey.LEFTSHIFT);

                k = fakedVirtualKey = cm.Item1;
                fakedInputKey = Key;
            }
            DoKeyChange(IsPressed, k);
        }

        private void DoKeyChange(bool IsPressed, VirtualKey k)
        {
            if (k != VirtualKey.NONE)
            {
                if (k != VirtualKey.LEFTSHIFT && k != VirtualKey.RIGHTSHIFT)
                {
                    if (IsPressed)
                        System.Diagnostics.Debug.WriteLine("Key Pressed:  " + k.ToString());
                    else
                        System.Diagnostics.Debug.WriteLine("Key Released: " + k.ToString());
                }

                var m = keyAddresses[k];

                if (IsPressed)
                    mem[m.Item1] |= m.Item2;
                else
                    mem[m.Item1] &= m.Item3;
            }
        }

        public void ResetKeyboard()
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
