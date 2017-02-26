using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Sharp80
{
    internal sealed class UIController : IDisposable
    {
        public Computer Computer { get; set; }
        
#if CASSETTE
        public void LoadCassette(string FilePath)
        {
            computer.LoadCassette(FilePath);
        }
        public bool RewindCassette()
        {
            return computer.RewindCassette();
        }
#endif
        
        public void ResetKeyboard()
        {
            Computer.ResetKeyboard();
        }
        
        public bool IsDisposed
        {
            get { return Computer.IsDisposed; }
        }
        public void Dispose()
        {
            
        }
    }
}
