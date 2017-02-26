using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.DirectInput;

namespace Sharp80
{
    internal class ViewNormal : View
    {
        protected override bool ForceRedraw => false;
        protected override ViewMode Mode => ViewMode.NormalView;
        
        protected override byte[] GetViewBytes()
        {
            return null;
        }
    }
}
