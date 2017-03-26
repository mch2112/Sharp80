using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Sharp80;

namespace Sharp80Tests
{
    public abstract class Test
    {
        protected Computer computer;

        protected void DelayMSec(ulong MSec)
        {
            DelayTStates(MSec * Clock.CLOCK_RATE / 1000);   
        }
        protected void DelayTStates(ulong Delay)
        {
            ulong startTStates = computer.GetElapsedTStates();
            ulong endTStates = startTStates + Delay;

            while (computer.GetElapsedTStates() < endTStates)
            {
                Thread.Sleep(100);
            }
        }
        protected void KeyDown(KeyCode Key, ulong DelayMSec = 300)
        {
            computer.NotifyKeyboardChange(new KeyState(Key, false, false, false, true));
            this.DelayMSec(DelayMSec);
        }
        protected void KeyUp(KeyCode Key, ulong DelayMSec = 300)
        {
            computer.NotifyKeyboardChange(new KeyState(Key, false, false, false, false));
            this.DelayMSec(DelayMSec);
        }
        protected void KeyPress(KeyCode Key, ulong DelayMSec = 300)
        {
            KeyDown(Key, DelayMSec);
            KeyUp(Key, DelayMSec);
        }
        protected bool ScreenContainsText(string Text)
        {
            return computer.VideoMemory.Contains(Text.ToByteArray());
        }
        protected void InitComputer()
        {
            computer = new Computer(new ScreenNull(), false, true);
        }
        protected void DisposeComputer()
        {
            computer.Stop(true);
            computer.Dispose();
        }
     }
}
