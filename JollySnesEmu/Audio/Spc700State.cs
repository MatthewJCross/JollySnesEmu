namespace JollySnesEmu.Audio
{
    public sealed class Spc700State
    {
        public byte A { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte StackPointer { get; set; } = 0xEF;
        public ushort ProgramCounter { get; set; }

        public byte Status { get; set; } = 0x02;

        public bool Carry
        {
            get => (Status & 0x01) != 0;
            set => SetFlag(0x01, value);
        }

        public bool Zero
        {
            get => (Status & 0x02) != 0;
            set => SetFlag(0x02, value);
        }

        public bool InterruptEnable
        {
            get => (Status & 0x04) != 0;
            set => SetFlag(0x04, value);
        }

        public bool HalfCarry
        {
            get => (Status & 0x08) != 0;
            set => SetFlag(0x08, value);
        }

        public bool Break
        {
            get => (Status & 0x10) != 0;
            set => SetFlag(0x10, value);
        }

        public bool DirectPage
        {
            get => (Status & 0x20) != 0;
            set => SetFlag(0x20, value);
        }

        public bool Overflow
        {
            get => (Status & 0x40) != 0;
            set => SetFlag(0x40, value);
        }

        public bool Negative
        {
            get => (Status & 0x80) != 0;
            set => SetFlag(0x80, value);
        }

        private void SetFlag(byte mask, bool value)
        {
            Status = value ? (byte)(Status | mask) : (byte)(Status & ~mask);
        }
    }
}
