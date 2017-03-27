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
        
        protected async Task StartToBasic(bool fast = true)
        {
            InitComputer(false);
            computer.NormalSpeed = !fast;
            computer.Start();
            await computer.Delay(500);
            await KeyPress(KeyCode.Return, false, 500);
            await KeyPress(KeyCode.Return, false, 500);
            await computer.Delay(2000);
        }
        protected async Task StartToTrsdos(bool fast = true)
        {
            await StartWithFloppy(Storage.FILE_NAME_TRSDOS, fast);
        }
        protected async Task StartWithFloppy(string Path, bool fast = true)
        {
            InitComputer(true);
            computer.NormalSpeed = !fast;
            computer.LoadFloppy(0, Path);
            computer.Start();
            await computer.Delay(20000);
        }
        protected async Task KeyPress(KeyCode Key, bool Shift, uint DelayMSecDown = 40, uint DelayMSecUp = 40)
        {
            await computer.KeyStroke(Key, Shift, DelayMSecDown, DelayMSecUp);
        }

        protected bool ScreenContainsText(string Text) => computer.VideoMemory.Contains(Text.ToByteArray());

        protected void InitComputer(bool EnableFloppyController)
        {
            computer = new Computer(new ScreenNull(), EnableFloppyController, false);
        }
        protected bool DisposeComputer(bool PassThrough = true)
        {
            computer.Stop(true);
            computer.Dispose();

            return PassThrough;
        }
     }
}
