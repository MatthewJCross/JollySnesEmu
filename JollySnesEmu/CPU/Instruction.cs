namespace JollySnesEmu.CPU
{   
    public enum AddressingMode
    {
        Implied, Immediate, ImmediateM, ImmediateX, Immediate16,
        DirectPage, DirectPageX, DirectPageY, DirectPageIndirect,
        DirectPageIndexedIndirect, DirectPageIndirectIndexed, DirectPageIndirectIndexedRead,
        DirectPageIndirectLong, DirectPageIndirectLongIndexed, StackRelative,
        StackRelativeIndirectIndexed, Absolute, AbsoluteX, AbsoluteXRead,
        AbsoluteY, AbsoluteYRead, AbsoluteLong, AbsoluteLongX,
        AbsoluteIndirect, AbsoluteIndexedIndirect, AbsoluteIndirectLong,
        Relative, RelativeLong, BlockMove
    }

    public enum InstructionSizeMode
    {
        Fixed,
        Accumulator,
        Index
    }

    public sealed class Instruction
    {
        public int Opcode { get; init; }
        public string Mnemonic { get; init; } = string.Empty;
        public AddressingMode Mode { get; init; }
        // Size = 0 means immediate operand length depends on the M or X flag.
        public int Size { get; init; }
        public InstructionSizeMode SizeMode { get; init; } = InstructionSizeMode.Fixed;
        public int Cycles { get; init; }
        public Action<Cpu65816, int, int> Execute { get; init; } = null!;
    }
}
