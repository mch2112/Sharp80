/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80.Z80
{
    internal class Register8 : IRegister<byte>
    {
        public string Name { get; }
        public byte Value { get; set; }
        
        public Register8(string Name) => this.Name = Name;

        public void Inc() => Value++;
        public void Dec() => Value--;

        public bool NZ => Value != 0x00;

        public override string ToString() => Value.ToHexString();
    }
    internal class Register8Indirect : IRegister<byte>
    {
        public IRegister<ushort> Proxy { get; }
        public string Name { get; }
        private readonly Z80 z80;
        public Register8Indirect(Z80 Processor, IRegister<ushort> Proxy, string Name)
        {
            z80 = Processor;
            this.Proxy = Proxy;
            this.Name = Name;
        }
        public byte Value
        {
            get => z80.Memory[Proxy.Value];
            set => z80.Memory[Proxy.Value] = value;
        }

        public void Inc() => Value++;
        public void Dec() => Value--;
        public bool NZ => Value != 0;
        public override string ToString() => Value.ToHexString();
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
        public byte Value
        {
            get => z80.Memory[OffsetAddress];
            set => z80.Memory[OffsetAddress] = value;
        }
        public void Inc() => Value++;
        public void Dec() => Value--;
        public bool NZ => Value != 0;
        public ushort OffsetAddress => Proxy.Value.Offset(z80.ByteAtPCPlusCoreOpCodeSize.TwosComp());
        public override string ToString() => Value.ToHexString();
    }
    internal class Register16 : IRegister<ushort>
    {
        public string Name { get; }
        public ushort Value { get; set; }

        public Register16(string Name) => this.Name = Name;

        public void Inc() => Value++;
        public void Dec() => Value--;
        public bool NZ => Value != 0;
        public override string ToString() => Value.ToHexString();
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
        public ushort Value
        {
            get => Lib.CombineBytes(L.Value, H.Value);
            set
            {
                L.Value = (byte)(value & 0xFF);
                H.Value = (byte)(value >> 8);
            }
        }
        public void Inc()
        {
            L.Inc();
            if (L.Value == 0x00)
                H.Inc();
        }
        public void Dec()
        {
            L.Dec();
            if (L.Value == 0xFF)
                H.Dec();
        }

        public bool NZ => L.NZ || H.NZ;
        public override string ToString() => Value.ToHexString();
    }

    // Used only for the stack pointer indirection
    internal class Register16Indirect : IRegister<ushort>
    {
        public string Name { get; }

        private Z80 z80;
        private readonly IRegister<ushort> proxy;

        public Register16Indirect(Z80 Processor, IRegister<ushort> Proxy, string Name)
        {
            z80 = Processor;
            proxy = Proxy;
            this.Name = Name;
        }
        public ushort Value
        {
            get => z80.Memory.GetWordAt(proxy.Value);
            set => z80.Memory.SetWordAt(proxy.Value, value);
        }
        public void Inc() => Value++;
        public void Dec() => Value--;
        public bool NZ => Value != 0;
        public override string ToString() => Value.ToHexString();
    }
}
