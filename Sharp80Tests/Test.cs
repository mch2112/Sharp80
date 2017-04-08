using System;
using System.Threading.Tasks;

using Sharp80;
using Sharp80.TRS80;

namespace Sharp80Tests
{
    public abstract class Test
    {
        protected Computer computer;
        
        protected async Task StartToBasic(ClockSpeed ClockSpeed)
        {
            InitComputer(false, ClockSpeed.Unlimited);
            await computer.StartAndAwait();
            await computer.Delay(500);
            await KeyPress(KeyCode.Return, false, 500);
            await KeyPress(KeyCode.Return, false, 500);
            await computer.Delay(2000);
        }
        protected async Task StartToTrsdos(ClockSpeed ClockSpeed)
        {
            await StartWithFloppy(Storage.FILE_NAME_TRSDOS, ClockSpeed);
        }
        protected async Task StartWithFloppy(string Path, ClockSpeed ClockSpeed)
        {
            InitComputer(true, ClockSpeed);
            computer.LoadFloppy(0, Path);
            await computer.StartAndAwait();
            await computer.Delay(20000);
        }
        protected async Task KeyPress(KeyCode Key, bool Shift, uint DelayMSecDown = 40, uint DelayMSecUp = 40)
        {
            await computer.KeyStroke(Key, Shift, DelayMSecDown, DelayMSecUp);
        }
        protected async Task PasteLine() => await PasteLine(Environment.NewLine);
        protected async Task PasteLine(string Text)
        {
            await computer.Paste(Text + Environment.NewLine, new System.Threading.CancellationToken());
        }
        protected bool ScreenContainsText(string Text) => computer.VideoMemory.Contains(Text.ToByteArray());

        protected void InitComputer(bool EnableFloppyController, ClockSpeed ClockSpeed = ClockSpeed.Unlimited)
        {
            var settings = new Settings()
            {
                ClockSpeed = ClockSpeed,
                DiskEnabled = EnableFloppyController
            };
            InitComputer(settings);
        }
        protected void InitComputer(ISettings Settings)
        {
            computer = new Computer(new ScreenNull(), new SoundNull(), new Timer(), Settings, new NullDialogs());
        }
        protected async Task DisposeComputer()
        {
            await computer.Shutdown();
        }
        protected void Log(string Message)
        {
            Microsoft.VisualStudio.TestTools.UnitTesting.Logging.Logger.LogMessage(Message);
        }
     }
}
