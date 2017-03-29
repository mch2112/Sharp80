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
        
        protected async Task StartToBasic(bool Fast = true, bool Sound = false)
        {
            InitComputer(false, Sound);
            computer.NormalSpeed = !Fast;
            await computer.StartAndAwait();
            await computer.Delay(500);
            await KeyPress(KeyCode.Return, false, 500);
            await KeyPress(KeyCode.Return, false, 500);
            await computer.Delay(2000);
        }
        protected async Task StartToTrsdos(bool fast = true)
        {
            await StartWithFloppy(Storage.FILE_NAME_TRSDOS, fast);
        }
        protected async Task StartWithFloppy(string Path, bool Fast = true, bool Sound = false)
        {
            InitComputer(true, Sound);
            computer.NormalSpeed = !Fast;
            computer.LoadFloppy(0, Path);
            await computer.StartAndAwait();
            await computer.Delay(20000);
        }
        protected async Task KeyPress(KeyCode Key, bool Shift, uint DelayMSecDown = 40, uint DelayMSecUp = 40)
        {
            await computer.KeyStroke(Key, Shift, DelayMSecDown, DelayMSecUp);
        }

        protected bool ScreenContainsText(string Text) => computer.VideoMemory.Contains(Text.ToByteArray());

        protected void InitComputer(bool EnableFloppyController, bool Sound)
        {
            computer = new Computer(new ScreenNull(), EnableFloppyController, Sound);
            ExceptionHandler.PassThrough = true;
        }
        protected async Task DisposeComputer()
        {
            await computer.StopAndAwait();
            computer.Dispose();
        }
        protected void Log(string Message)
        {
            Microsoft.VisualStudio.TestTools.UnitTesting.Logging.Logger.LogMessage(Message);
        }
     }
}
