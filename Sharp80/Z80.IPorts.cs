using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80.Processor
{
    internal interface IPorts
    {
        byte this[byte PortNumber] { get; set; }
    }
}
