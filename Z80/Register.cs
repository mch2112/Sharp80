/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.Z80
{
    internal class Register8 : IRegister<byte>
    {
        public byte val { get; set; }
        public string Name { get; }

        public Register8(string Name) => this.Name = Name;

        public void inc() => val++;
        public void dec() => val--;

        public bool NZ => val != 0x00;

        public override string ToString() => val.ToHexString();
    }
    internal class Register8Indirect : IRegister<byte>
    {
        public IRegister<ushort> Proxy { get; }
        public string Name { get; }
        private Z80 z80;
        public Register8Indirect(Z80 Processor, IRegister<ushort> Proxy, string Name)
        {
            z80 = Processor;
            this.Proxy = Proxy;
            this.Name = Name;
        }
        public byte val
        {
            get => z80.Memory[Proxy.val];
            set => z80.Memory[Proxy.val] = value;
        }

        public void inc() => val++;
        public void dec() => val--;
        public bool NZ => val != 0;
        public override string ToString() => val.ToHexString();
    }
    internal class RegisterIndexed : IRegisterIndexed
    {
        public IRegister<ushort> Proxy { get; }
        public string Name { get; }
        private Z80 z80;

        public RegisterIndexed(Z80 Processor, IRegister<ushort> Proxy, string Name)
        {
            z80 = Processor;
            this.Proxy = Proxy;
            this.Name = Name;
        }
        public byte val
        {
            get => z80.Memory[OffsetAddress];
            set => z80.Memory[OffsetAddress] = value;
        }
        public void inc() => val++;
        public void dec() => val--;
        public bool NZ => val != 0;
        public ushort OffsetAddress => Proxy.val.Offset(z80.ByteAtPCPlusCoreOpCodeSize.TwosComp());
        public override string ToString() => val.ToHexString();
    }
    internal class Register16 : IRegister<ushort>
    {
        public string Name { get; }
        public ushort val { get; set; }

        public Register16(string Name) => this.Name = Name;

        public void inc() => val++;
        public void dec() => val--;
        public bool NZ => val != 0;
        public override string ToString() => val.ToHexString();
    }
    internal class RegisterCompound : IRegisterCompound
    {
        public string Name { get; }
        public IRegister<byte> L { get; }
        public IRegister<byte> H { get; }

        public RegisterCompound(IRegister<byte> Low, IRegister<byte> High, string Name)
        {
            L = Low;
            H = High;
            this.Name = Name;
        }
        public RegisterCompound(string Name) : this(new Register8(Name + "l"), new Register8(Name + "h"), Name)
        {
        }
        public ushort val
        {
            get => Lib.CombineBytes(L.val, H.val);
            set
            {
                L.val = (byte)(value & 0xFF);
                H.val = (byte)(value >> 8);
            }
        }
        public void inc()
        {
            L.inc();
            if (L.val == 0x00)
                H.inc();
        }
        public void dec()
        {
            L.dec();
            if (L.val == 0xFF)
                H.dec();
        }

        public bool NZ => L.NZ || H.NZ;
        public override string ToString() => val.ToHexString();
    }

    // Used only for the stack pointer indirection
    internal class Register16Indirect : IRegister<ushort>
    {
        public string Name { get; }

        private Z80 z80;
        private IRegister<ushort> proxy;

        public Register16Indirect(Z80 Processor, IRegister<ushort> Proxy, string Name)
        {
            z80 = Processor;
            proxy = Proxy;
            this.Name = Name;
        }
        public ushort val
        {
            get => z80.Memory.GetWordAt(proxy.val);
            set => z80.Memory.SetWordAt(proxy.val, value);
        }
        public void inc() => val++;
        public void dec() => val--;
        public bool NZ => val != 0;
        public override string ToString() => val.ToHexString();
    }
}
