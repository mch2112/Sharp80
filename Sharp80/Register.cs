/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3

using System;

namespace Sharp80.Processor
{
    internal abstract class Register
    {
        public string Name { get; private set; }
        protected Z80 z80 { get; private set; }

        public abstract void inc();
        public abstract void dec();

        public abstract bool Z { get; }
        public abstract bool NZ { get; }

        public Register(Z80 Processor, string Name)
        {
            z80 = Processor;
            this.Name = Name;
        }
    }

    internal class Register8 : Register
    {
        private byte myVal;

        public Register8(Z80 Processor, string Name) : base(Processor, Name)
        {
        }
        public virtual byte val
        {
            get { return myVal; }
            set { myVal = value; }
        }

        public override void inc() { myVal++; }
        public override void dec() { myVal--; }

        public override bool Z { get { return myVal == 0x00; } }
        public override bool NZ { get { return myVal != 0x00; } }

        public override string ToString()
        {
            return val.ToHexString();
        }
    }
    internal class Register8Indirect : Register8
    {
        public Register16Compound Proxy { get; set; }

        public Register8Indirect(Z80 Processor, Register16Compound Proxy, string Name) : base(Processor, Name)
        {
            this.Proxy = Proxy;
        }
        public override byte val
        {
            get { return z80.Memory[Proxy.val]; }
            set { z80.Memory[Proxy.val] = value; }
        }

        public ushort ProxyVal { get { return Proxy.val; } }

        public override void inc() { this.val++; }
        public override void dec() { this.val--; }
    }
    internal sealed class Register8Indexed : Register8Indirect
    {
        public Register8Indexed(Z80 Processor, Register16Compound Proxy, string Name) : base(Processor, Proxy, Name) { }

        public override byte val
        {
            get { return z80.Memory[OffsetAddress]; }
            set { z80.Memory[OffsetAddress] = value; }
        }
        public ushort OffsetAddress
        {
            get
            {
                return (ushort)(Proxy.val + z80.ByteAtPCPlusInitialOpCodeLength.TwosComp());
            }
        }
    }
    internal abstract class Register16 : Register
    {
        public abstract ushort val { get; set; }
        public Register16(Z80 Processor, string Name) : base(Processor, Name)
        {
        }
        public override string ToString()
        {
            return val.ToHexString();
        }
    }
    internal class Register16Normal : Register16
    {
        public override ushort val { get; set; }

        public Register16Normal(Z80 Processor, string Name) : base(Processor, Name)
        {
            
        }

        public override void inc()
        {
            val++;
        }

        public override void dec()
        {
            val--;
        }
        public override bool Z { get { return val == 0; } }
        public override bool NZ { get { return val != 0; } }

        public byte hVal
        {
            get { return (byte)((val & 0xFF00) >> 8); }
            set { val = (ushort)((val & 0x00FF) | (value << 8)); }
        }
        public byte lVal
        {
            get { return (byte)(val & 0xFF); }
            set { val = (ushort)((val & 0xFF00) | value); }
        }

        public void setVal(byte high, byte low)
        {
            val = (ushort) ((high << 8) | low);
        }
    }
    internal class Register16Compound : Register16
    {
        public Register8 L;
        public Register8 H;

        public Register16Compound(Register8 low, Register8 high, Z80 Processor, string Name) : base(Processor, Name)
        {
            L = low;
            H = high;           
        }
        public Register16Compound(Z80 Processor, string Name) : base(Processor, Name)
        {
            L = new Register8(Processor, Name + "l");
            H = new Register8(Processor, Name + "h");
        }
        public override ushort val
        {
            get { return Lib.CombineBytes(L.val, H.val); }
            set
            {
                L.val = (byte)(value & 0xFF);
                H.val = (byte)((value & 0xFF00) >> 8);
            }
        }
        public override void inc()
        {
            L.inc();
            if (L.val == 0x00)
                H.inc();
        }
        public override void dec()
        {
            L.dec();
            if (L.val == 0xFF)
                H.dec();
        }

        public override bool Z { get { return L.Z && H.Z; } }
        public override bool NZ { get { return L.NZ || H.NZ; } }        
    }

    // Used only for the stack pointer indirection
    internal sealed class Register16Indirect : Register16Compound
    {
        private Register16 proxy;
        
        public Register16Indirect(Z80 Processor, Register16 Proxy, string Name) : base(Processor, Name)
        {
            proxy = Proxy;
        }
        public override ushort val
        {
            get { return z80.Memory.GetWordAt(proxy.val); }
            set { z80.Memory.SetWordAt(proxy.val, value); }
        }
        public override void inc() { val++; }
        public override void dec() { val--; }
    }
}
