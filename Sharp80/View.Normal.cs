/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;

namespace Sharp80
{
    internal class ViewNormal : View
    {
        protected override bool ForceRedraw => false;
        protected override ViewMode Mode => ViewMode.Normal;
        
        protected override byte[] GetViewBytes()
        {
            return null;
        }
    }
}
