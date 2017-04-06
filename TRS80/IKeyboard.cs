/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp80.TRS80
{
    /// <summary>
    /// This interface is used to allow for future non-DirectInput based implementations
    /// </summary>
    public interface IKeyboard : IDisposable
    {
        Task Start(float PollRateInHz, Action<KeyState> Callback, CancellationToken StopToken);
        bool Enabled { get; set; }
        bool IsShifted { get; }
        bool LeftShiftPressed { get; }
        bool RightShiftPressed { get; }
        bool IsControlPressed { get; }
        bool IsAltPressed { get; }

        void Refresh();
    }
}