using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.Processor
{
    public interface IComputer
    {
        bool IsRunning {get;}
        void Stop(bool WaitForStop);
    }
}
