namespace JollySnesEmu.CPU
{
    public static class InstructionSet
    {
        static InstructionSet()
        {
            if (Instructions.Length != 259)
                throw new InvalidOperationException($"Expected 259 instruction entries, found {Instructions.Length}.");

            for (int opcode = 0; opcode < 259; opcode++)
            {
                Instruction instruction = Instructions[opcode];

                if (instruction.Opcode != opcode)
                    throw new InvalidOperationException($"Opcode mismatch at ${opcode:X3}.");

                if (string.IsNullOrWhiteSpace(instruction.Mnemonic))
                    throw new InvalidOperationException($"Missing mnemonic for opcode ${opcode:X3}.");

                if (instruction.Execute == null)
                    throw new InvalidOperationException($"Missing execution handler for opcode ${opcode:X3}.");

                if (opcode < 256 && instruction.SizeMode == InstructionSizeMode.Fixed && (instruction.Size < 1 || instruction.Size > 4))
                    throw new InvalidOperationException($"Invalid instruction size for opcode ${opcode:X2}.");
            }
        }

        public static readonly Instruction[] Instructions =
        [
            // 0x00 - 0x0F
            /* 0x000 */ new Instruction { Opcode = 0x000, Mnemonic = "BRK", Mode = AddressingMode.Implied, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Brk(address, value) },
            /* 0x001 */ new Instruction { Opcode = 0x001, Mnemonic = "ORA", Mode = AddressingMode.DirectPageIndexedIndirect, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x002 */ new Instruction { Opcode = 0x002, Mnemonic = "COP", Mode = AddressingMode.Immediate, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Cop(address, value) },
            /* 0x003 */ new Instruction { Opcode = 0x003, Mnemonic = "ORA", Mode = AddressingMode.StackRelative, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x004 */ new Instruction { Opcode = 0x004, Mnemonic = "TSB", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Tsb(address, value) },
            /* 0x005 */ new Instruction { Opcode = 0x005, Mnemonic = "ORA", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x006 */ new Instruction { Opcode = 0x006, Mnemonic = "ASL", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Asl(address, value) },
            /* 0x007 */ new Instruction { Opcode = 0x007, Mnemonic = "ORA", Mode = AddressingMode.DirectPageIndirectLong, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x008 */ new Instruction { Opcode = 0x008, Mnemonic = "PHP", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Php(address, value) },
            /* 0x009 */ new Instruction { Opcode = 0x009, Mnemonic = "ORA", Mode = AddressingMode.ImmediateM, Size = 0, SizeMode = InstructionSizeMode.Accumulator, Cycles = 2, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x00A */ new Instruction { Opcode = 0x00A, Mnemonic = "ASL", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Asla(address, value) },
            /* 0x00B */ new Instruction { Opcode = 0x00B, Mnemonic = "PHD", Mode = AddressingMode.Implied, Size = 1, Cycles = 4, Execute = static (cpu, address, value) => cpu.Phd(address, value) },
            /* 0x00C */ new Instruction { Opcode = 0x00C, Mnemonic = "TSB", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Tsb(address, value) },
            /* 0x00D */ new Instruction { Opcode = 0x00D, Mnemonic = "ORA", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x00E */ new Instruction { Opcode = 0x00E, Mnemonic = "ASL", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Asl(address, value) },
            /* 0x00F */ new Instruction { Opcode = 0x00F, Mnemonic = "ORA", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Ora(address, value) },

            // 0x10 - 0x1F
            /* 0x010 */ new Instruction { Opcode = 0x010, Mnemonic = "BPL", Mode = AddressingMode.Relative, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => cpu.Bpl(address, value) },
            /* 0x011 */ new Instruction { Opcode = 0x011, Mnemonic = "ORA", Mode = AddressingMode.DirectPageIndirectIndexedRead, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x012 */ new Instruction { Opcode = 0x012, Mnemonic = "ORA", Mode = AddressingMode.DirectPageIndirect, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x013 */ new Instruction { Opcode = 0x013, Mnemonic = "ORA", Mode = AddressingMode.StackRelativeIndirectIndexed, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x014 */ new Instruction { Opcode = 0x014, Mnemonic = "TRB", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Trb(address, value) },
            /* 0x015 */ new Instruction { Opcode = 0x015, Mnemonic = "ORA", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x016 */ new Instruction { Opcode = 0x016, Mnemonic = "ASL", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Asl(address, value) },
            /* 0x017 */ new Instruction { Opcode = 0x017, Mnemonic = "ORA", Mode = AddressingMode.DirectPageIndirectLongIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x018 */ new Instruction { Opcode = 0x018, Mnemonic = "CLC", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Clc(address, value) },
            /* 0x019 */ new Instruction { Opcode = 0x019, Mnemonic = "ORA", Mode = AddressingMode.AbsoluteYRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x01A */ new Instruction { Opcode = 0x01A, Mnemonic = "INC", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Inca(address, value) },
            /* 0x01B */ new Instruction { Opcode = 0x01B, Mnemonic = "TCS", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tcs(address, value) },
            /* 0x01C */ new Instruction { Opcode = 0x01C, Mnemonic = "TRB", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Trb(address, value) },
            /* 0x01D */ new Instruction { Opcode = 0x01D, Mnemonic = "ORA", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ora(address, value) },
            /* 0x01E */ new Instruction { Opcode = 0x01E, Mnemonic = "ASL", Mode = AddressingMode.AbsoluteX, Size = 3, Cycles = 7, Execute = static (cpu, address, value) => cpu.Asl(address, value) },
            /* 0x01F */ new Instruction { Opcode = 0x01F, Mnemonic = "ORA", Mode = AddressingMode.AbsoluteLongX, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Ora(address, value) },

            // 0x20 - 0x2F
            /* 0x020 */ new Instruction { Opcode = 0x020, Mnemonic = "JSR", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Jsr(address, value) },
            /* 0x021 */ new Instruction { Opcode = 0x021, Mnemonic = "AND", Mode = AddressingMode.DirectPageIndexedIndirect, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x022 */ new Instruction { Opcode = 0x022, Mnemonic = "JSL", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 8, Execute = static (cpu, address, value) => cpu.Jsl(address, value) },
            /* 0x023 */ new Instruction { Opcode = 0x023, Mnemonic = "AND", Mode = AddressingMode.StackRelative, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x024 */ new Instruction { Opcode = 0x024, Mnemonic = "BIT", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Bit(address, value) },
            /* 0x025 */ new Instruction { Opcode = 0x025, Mnemonic = "AND", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x026 */ new Instruction { Opcode = 0x026, Mnemonic = "ROL", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Rol(address, value) },
            /* 0x027 */ new Instruction { Opcode = 0x027, Mnemonic = "AND", Mode = AddressingMode.DirectPageIndirectLong, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x028 */ new Instruction { Opcode = 0x028, Mnemonic = "PLP", Mode = AddressingMode.Implied, Size = 1, Cycles = 4, Execute = static (cpu, address, value) => cpu.Plp(address, value) },
            /* 0x029 */ new Instruction { Opcode = 0x029, Mnemonic = "AND", Mode = AddressingMode.ImmediateM, Size = 0, SizeMode = InstructionSizeMode.Accumulator, Cycles = 2, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x02A */ new Instruction { Opcode = 0x02A, Mnemonic = "ROL", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Rola(address, value) },
            /* 0x02B */ new Instruction { Opcode = 0x02B, Mnemonic = "PLD", Mode = AddressingMode.Implied, Size = 1, Cycles = 5, Execute = static (cpu, address, value) => cpu.Pld(address, value) },
            /* 0x02C */ new Instruction { Opcode = 0x02C, Mnemonic = "BIT", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Bit(address, value) },
            /* 0x02D */ new Instruction { Opcode = 0x02D, Mnemonic = "AND", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x02E */ new Instruction { Opcode = 0x02E, Mnemonic = "ROL", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Rol(address, value) },
            /* 0x02F */ new Instruction { Opcode = 0x02F, Mnemonic = "AND", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.And(address, value) },

            // 0x30 - 0x3F
            /* 0x030 */ new Instruction { Opcode = 0x030, Mnemonic = "BMI", Mode = AddressingMode.Relative, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => cpu.Bmi(address, value) },
            /* 0x031 */ new Instruction { Opcode = 0x031, Mnemonic = "AND", Mode = AddressingMode.DirectPageIndirectIndexedRead, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x032 */ new Instruction { Opcode = 0x032, Mnemonic = "AND", Mode = AddressingMode.DirectPageIndirect, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x033 */ new Instruction { Opcode = 0x033, Mnemonic = "AND", Mode = AddressingMode.StackRelativeIndirectIndexed, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x034 */ new Instruction { Opcode = 0x034, Mnemonic = "BIT", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Bit(address, value) },
            /* 0x035 */ new Instruction { Opcode = 0x035, Mnemonic = "AND", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x036 */ new Instruction { Opcode = 0x036, Mnemonic = "ROL", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Rol(address, value) },
            /* 0x037 */ new Instruction { Opcode = 0x037, Mnemonic = "AND", Mode = AddressingMode.DirectPageIndirectLongIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x038 */ new Instruction { Opcode = 0x038, Mnemonic = "SEC", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Sec(address, value) },
            /* 0x039 */ new Instruction { Opcode = 0x039, Mnemonic = "AND", Mode = AddressingMode.AbsoluteYRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x03A */ new Instruction { Opcode = 0x03A, Mnemonic = "DEC", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Deca(address, value) },
            /* 0x03B */ new Instruction { Opcode = 0x03B, Mnemonic = "TSC", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tsc(address, value) },
            /* 0x03C */ new Instruction { Opcode = 0x03C, Mnemonic = "BIT", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Bit(address, value) },
            /* 0x03D */ new Instruction { Opcode = 0x03D, Mnemonic = "AND", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.And(address, value) },
            /* 0x03E */ new Instruction { Opcode = 0x03E, Mnemonic = "ROL", Mode = AddressingMode.AbsoluteX, Size = 3, Cycles = 7, Execute = static (cpu, address, value) => cpu.Rol(address, value) },
            /* 0x03F */ new Instruction { Opcode = 0x03F, Mnemonic = "AND", Mode = AddressingMode.AbsoluteLongX, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.And(address, value) },

            // 0x40 - 0x4F
            /* 0x040 */ new Instruction { Opcode = 0x040, Mnemonic = "RTI", Mode = AddressingMode.Implied, Size = 1, Cycles = 6, Execute = static (cpu, address, value) => cpu.Rti(address, value) },
            /* 0x041 */ new Instruction { Opcode = 0x041, Mnemonic = "EOR", Mode = AddressingMode.DirectPageIndexedIndirect, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x042 */ new Instruction { Opcode = 0x042, Mnemonic = "WDM", Mode = AddressingMode.Immediate, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => Cpu65816.Wdm(address, value) },
            /* 0x043 */ new Instruction { Opcode = 0x043, Mnemonic = "EOR", Mode = AddressingMode.StackRelative, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x044 */ new Instruction { Opcode = 0x044, Mnemonic = "MVP", Mode = AddressingMode.BlockMove, Size = 3, Cycles = 7, Execute = static (cpu, address, value) => cpu.Mvp(address, value) },
            /* 0x045 */ new Instruction { Opcode = 0x045, Mnemonic = "EOR", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x046 */ new Instruction { Opcode = 0x046, Mnemonic = "LSR", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Lsr(address, value) },
            /* 0x047 */ new Instruction { Opcode = 0x047, Mnemonic = "EOR", Mode = AddressingMode.DirectPageIndirectLong, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x048 */ new Instruction { Opcode = 0x048, Mnemonic = "PHA", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Pha(address, value) },
            /* 0x049 */ new Instruction { Opcode = 0x049, Mnemonic = "EOR", Mode = AddressingMode.ImmediateM, Size = 0, SizeMode = InstructionSizeMode.Accumulator, Cycles = 2, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x04A */ new Instruction { Opcode = 0x04A, Mnemonic = "LSR", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Lsra(address, value) },
            /* 0x04B */ new Instruction { Opcode = 0x04B, Mnemonic = "PHK", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Phk(address, value) },
            /* 0x04C */ new Instruction { Opcode = 0x04C, Mnemonic = "JMP", Mode = AddressingMode.Absolute, Size = 3, Cycles = 3, Execute = static (cpu, address, value) => cpu.Jmp(address, value) },
            /* 0x04D */ new Instruction { Opcode = 0x04D, Mnemonic = "EOR", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x04E */ new Instruction { Opcode = 0x04E, Mnemonic = "LSR", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Lsr(address, value) },
            /* 0x04F */ new Instruction { Opcode = 0x04F, Mnemonic = "EOR", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Eor(address, value) },

            // 0x50 - 0x5F
            /* 0x050 */ new Instruction { Opcode = 0x050, Mnemonic = "BVC", Mode = AddressingMode.Relative, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => cpu.Bvc(address, value) },
            /* 0x051 */ new Instruction { Opcode = 0x051, Mnemonic = "EOR", Mode = AddressingMode.DirectPageIndirectIndexedRead, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x052 */ new Instruction { Opcode = 0x052, Mnemonic = "EOR", Mode = AddressingMode.DirectPageIndirect, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x053 */ new Instruction { Opcode = 0x053, Mnemonic = "EOR", Mode = AddressingMode.StackRelativeIndirectIndexed, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x054 */ new Instruction { Opcode = 0x054, Mnemonic = "MVN", Mode = AddressingMode.BlockMove, Size = 3, Cycles = 7, Execute = static (cpu, address, value) => cpu.Mvn(address, value) },
            /* 0x055 */ new Instruction { Opcode = 0x055, Mnemonic = "EOR", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x056 */ new Instruction { Opcode = 0x056, Mnemonic = "LSR", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Lsr(address, value) },
            /* 0x057 */ new Instruction { Opcode = 0x057, Mnemonic = "EOR", Mode = AddressingMode.DirectPageIndirectLongIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x058 */ new Instruction { Opcode = 0x058, Mnemonic = "CLI", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Cli(address, value) },
            /* 0x059 */ new Instruction { Opcode = 0x059, Mnemonic = "EOR", Mode = AddressingMode.AbsoluteYRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x05A */ new Instruction { Opcode = 0x05A, Mnemonic = "PHY", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Phy(address, value) },
            /* 0x05B */ new Instruction { Opcode = 0x05B, Mnemonic = "TCD", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tcd(address, value) },
            /* 0x05C */ new Instruction { Opcode = 0x05C, Mnemonic = "JML", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 4, Execute = static (cpu, address, value) => cpu.Jml(address, value) },
            /* 0x05D */ new Instruction { Opcode = 0x05D, Mnemonic = "EOR", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Eor(address, value) },
            /* 0x05E */ new Instruction { Opcode = 0x05E, Mnemonic = "LSR", Mode = AddressingMode.AbsoluteX, Size = 3, Cycles = 7, Execute = static (cpu, address, value) => cpu.Lsr(address, value) },
            /* 0x05F */ new Instruction { Opcode = 0x05F, Mnemonic = "EOR", Mode = AddressingMode.AbsoluteLongX, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Eor(address, value) },

            // 0x60 - 0x6F
            /* 0x060 */ new Instruction { Opcode = 0x060, Mnemonic = "RTS", Mode = AddressingMode.Implied, Size = 1, Cycles = 6, Execute = static (cpu, address, value) => cpu.Rts(address, value) },
            /* 0x061 */ new Instruction { Opcode = 0x061, Mnemonic = "ADC", Mode = AddressingMode.DirectPageIndexedIndirect, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x062 */ new Instruction { Opcode = 0x062, Mnemonic = "PER", Mode = AddressingMode.RelativeLong, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Per(address, value) },
            /* 0x063 */ new Instruction { Opcode = 0x063, Mnemonic = "ADC", Mode = AddressingMode.StackRelative, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x064 */ new Instruction { Opcode = 0x064, Mnemonic = "STZ", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Stz(address, value) },
            /* 0x065 */ new Instruction { Opcode = 0x065, Mnemonic = "ADC", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x066 */ new Instruction { Opcode = 0x066, Mnemonic = "ROR", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Ror(address, value) },
            /* 0x067 */ new Instruction { Opcode = 0x067, Mnemonic = "ADC", Mode = AddressingMode.DirectPageIndirectLong, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x068 */ new Instruction { Opcode = 0x068, Mnemonic = "PLA", Mode = AddressingMode.Implied, Size = 1, Cycles = 4, Execute = static (cpu, address, value) => cpu.Pla(address, value) },
            /* 0x069 */ new Instruction { Opcode = 0x069, Mnemonic = "ADC", Mode = AddressingMode.ImmediateM, Size = 0, SizeMode = InstructionSizeMode.Accumulator, Cycles = 2, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x06A */ new Instruction { Opcode = 0x06A, Mnemonic = "ROR", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Rora(address, value) },
            /* 0x06B */ new Instruction { Opcode = 0x06B, Mnemonic = "RTL", Mode = AddressingMode.Implied, Size = 1, Cycles = 6, Execute = static (cpu, address, value) => cpu.Rtl(address, value) },
            /* 0x06C */ new Instruction { Opcode = 0x06C, Mnemonic = "JMP", Mode = AddressingMode.AbsoluteIndirect, Size = 3, Cycles = 5, Execute = static (cpu, address, value) => cpu.Jmp(address, value) },
            /* 0x06D */ new Instruction { Opcode = 0x06D, Mnemonic = "ADC", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x06E */ new Instruction { Opcode = 0x06E, Mnemonic = "ROR", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Ror(address, value) },
            /* 0x06F */ new Instruction { Opcode = 0x06F, Mnemonic = "ADC", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Adc(address, value) },

            // 0x70 - 0x7F
            /* 0x070 */ new Instruction { Opcode = 0x070, Mnemonic = "BVS", Mode = AddressingMode.Relative, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => cpu.Bvs(address, value) },
            /* 0x071 */ new Instruction { Opcode = 0x071, Mnemonic = "ADC", Mode = AddressingMode.DirectPageIndirectIndexedRead, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x072 */ new Instruction { Opcode = 0x072, Mnemonic = "ADC", Mode = AddressingMode.DirectPageIndirect, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x073 */ new Instruction { Opcode = 0x073, Mnemonic = "ADC", Mode = AddressingMode.StackRelativeIndirectIndexed, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x074 */ new Instruction { Opcode = 0x074, Mnemonic = "STZ", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Stz(address, value) },
            /* 0x075 */ new Instruction { Opcode = 0x075, Mnemonic = "ADC", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x076 */ new Instruction { Opcode = 0x076, Mnemonic = "ROR", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Ror(address, value) },
            /* 0x077 */ new Instruction { Opcode = 0x077, Mnemonic = "ADC", Mode = AddressingMode.DirectPageIndirectLongIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x078 */ new Instruction { Opcode = 0x078, Mnemonic = "SEI", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Sei(address, value) },
            /* 0x079 */ new Instruction { Opcode = 0x079, Mnemonic = "ADC", Mode = AddressingMode.AbsoluteYRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x07A */ new Instruction { Opcode = 0x07A, Mnemonic = "PLY", Mode = AddressingMode.Implied, Size = 1, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ply(address, value) },
            /* 0x07B */ new Instruction { Opcode = 0x07B, Mnemonic = "TDC", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tdc(address, value) },
            /* 0x07C */ new Instruction { Opcode = 0x07C, Mnemonic = "JMP", Mode = AddressingMode.AbsoluteIndexedIndirect, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Jmp(address, value) },
            /* 0x07D */ new Instruction { Opcode = 0x07D, Mnemonic = "ADC", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Adc(address, value) },
            /* 0x07E */ new Instruction { Opcode = 0x07E, Mnemonic = "ROR", Mode = AddressingMode.AbsoluteX, Size = 3, Cycles = 7, Execute = static (cpu, address, value) => cpu.Ror(address, value) },
            /* 0x07F */ new Instruction { Opcode = 0x07F, Mnemonic = "ADC", Mode = AddressingMode.AbsoluteLongX, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Adc(address, value) },

            // 0x80 - 0x8F
            /* 0x080 */ new Instruction { Opcode = 0x080, Mnemonic = "BRA", Mode = AddressingMode.Relative, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Bra(address, value) },
            /* 0x081 */ new Instruction { Opcode = 0x081, Mnemonic = "STA", Mode = AddressingMode.DirectPageIndexedIndirect, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x082 */ new Instruction { Opcode = 0x082, Mnemonic = "BRL", Mode = AddressingMode.RelativeLong, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Brl(address, value) },
            /* 0x083 */ new Instruction { Opcode = 0x083, Mnemonic = "STA", Mode = AddressingMode.StackRelative, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x084 */ new Instruction { Opcode = 0x084, Mnemonic = "STY", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Sty(address, value) },
            /* 0x085 */ new Instruction { Opcode = 0x085, Mnemonic = "STA", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x086 */ new Instruction { Opcode = 0x086, Mnemonic = "STX", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Stx(address, value) },
            /* 0x087 */ new Instruction { Opcode = 0x087, Mnemonic = "STA", Mode = AddressingMode.DirectPageIndirectLong, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x088 */ new Instruction { Opcode = 0x088, Mnemonic = "DEY", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Dey(address, value) },
            /* 0x089 */ new Instruction { Opcode = 0x089, Mnemonic = "BIT", Mode = AddressingMode.ImmediateM, Size = 0, SizeMode = InstructionSizeMode.Accumulator, Cycles = 2, Execute = static (cpu, address, value) => cpu.Biti(address, value) },
            /* 0x08A */ new Instruction { Opcode = 0x08A, Mnemonic = "TXA", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Txa(address, value) },
            /* 0x08B */ new Instruction { Opcode = 0x08B, Mnemonic = "PHB", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Phb(address, value) },
            /* 0x08C */ new Instruction { Opcode = 0x08C, Mnemonic = "STY", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sty(address, value) },
            /* 0x08D */ new Instruction { Opcode = 0x08D, Mnemonic = "STA", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x08E */ new Instruction { Opcode = 0x08E, Mnemonic = "STX", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Stx(address, value) },
            /* 0x08F */ new Instruction { Opcode = 0x08F, Mnemonic = "STA", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sta(address, value) },

            // 0x90 - 0x9F
            /* 0x090 */ new Instruction { Opcode = 0x090, Mnemonic = "BCC", Mode = AddressingMode.Relative, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => cpu.Bcc(address, value) },
            /* 0x091 */ new Instruction { Opcode = 0x091, Mnemonic = "STA", Mode = AddressingMode.DirectPageIndirectIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x092 */ new Instruction { Opcode = 0x092, Mnemonic = "STA", Mode = AddressingMode.DirectPageIndirect, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x093 */ new Instruction { Opcode = 0x093, Mnemonic = "STA", Mode = AddressingMode.StackRelativeIndirectIndexed, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x094 */ new Instruction { Opcode = 0x094, Mnemonic = "STY", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sty(address, value) },
            /* 0x095 */ new Instruction { Opcode = 0x095, Mnemonic = "STA", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x096 */ new Instruction { Opcode = 0x096, Mnemonic = "STX", Mode = AddressingMode.DirectPageY, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Stx(address, value) },
            /* 0x097 */ new Instruction { Opcode = 0x097, Mnemonic = "STA", Mode = AddressingMode.DirectPageIndirectLongIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x098 */ new Instruction { Opcode = 0x098, Mnemonic = "TYA", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tya(address, value) },
            /* 0x099 */ new Instruction { Opcode = 0x099, Mnemonic = "STA", Mode = AddressingMode.AbsoluteY, Size = 3, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x09A */ new Instruction { Opcode = 0x09A, Mnemonic = "TXS", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Txs(address, value) },
            /* 0x09B */ new Instruction { Opcode = 0x09B, Mnemonic = "TXY", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Txy(address, value) },
            /* 0x09C */ new Instruction { Opcode = 0x09C, Mnemonic = "STZ", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Stz(address, value) },
            /* 0x09D */ new Instruction { Opcode = 0x09D, Mnemonic = "STA", Mode = AddressingMode.AbsoluteX, Size = 3, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sta(address, value) },
            /* 0x09E */ new Instruction { Opcode = 0x09E, Mnemonic = "STZ", Mode = AddressingMode.AbsoluteX, Size = 3, Cycles = 5, Execute = static (cpu, address, value) => cpu.Stz(address, value) },
            /* 0x09F */ new Instruction { Opcode = 0x09F, Mnemonic = "STA", Mode = AddressingMode.AbsoluteLongX, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sta(address, value) },

            // 0xA0 - 0xAF
            /* 0x0A0 */ new Instruction { Opcode = 0x0A0, Mnemonic = "LDY", Mode = AddressingMode.ImmediateX, Size = 0, SizeMode = InstructionSizeMode.Index, Cycles = 2, Execute = static (cpu, address, value) => cpu.Ldy(address, value) },
            /* 0x0A1 */ new Instruction { Opcode = 0x0A1, Mnemonic = "LDA", Mode = AddressingMode.DirectPageIndexedIndirect, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0A2 */ new Instruction { Opcode = 0x0A2, Mnemonic = "LDX", Mode = AddressingMode.ImmediateX, Size = 0, SizeMode = InstructionSizeMode.Index, Cycles = 2, Execute = static (cpu, address, value) => cpu.Ldx(address, value) },
            /* 0x0A3 */ new Instruction { Opcode = 0x0A3, Mnemonic = "LDA", Mode = AddressingMode.StackRelative, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0A4 */ new Instruction { Opcode = 0x0A4, Mnemonic = "LDY", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Ldy(address, value) },
            /* 0x0A5 */ new Instruction { Opcode = 0x0A5, Mnemonic = "LDA", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0A6 */ new Instruction { Opcode = 0x0A6, Mnemonic = "LDX", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Ldx(address, value) },
            /* 0x0A7 */ new Instruction { Opcode = 0x0A7, Mnemonic = "LDA", Mode = AddressingMode.DirectPageIndirectLong, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0A8 */ new Instruction { Opcode = 0x0A8, Mnemonic = "TAY", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tay(address, value) },
            /* 0x0A9 */ new Instruction { Opcode = 0x0A9, Mnemonic = "LDA", Mode = AddressingMode.ImmediateM, Size = 0, SizeMode = InstructionSizeMode.Accumulator, Cycles = 2, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0AA */ new Instruction { Opcode = 0x0AA, Mnemonic = "TAX", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tax(address, value) },
            /* 0x0AB */ new Instruction { Opcode = 0x0AB, Mnemonic = "PLB", Mode = AddressingMode.Implied, Size = 1, Cycles = 4, Execute = static (cpu, address, value) => cpu.Plb(address, value) },
            /* 0x0AC */ new Instruction { Opcode = 0x0AC, Mnemonic = "LDY", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ldy(address, value) },
            /* 0x0AD */ new Instruction { Opcode = 0x0AD, Mnemonic = "LDA", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0AE */ new Instruction { Opcode = 0x0AE, Mnemonic = "LDX", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ldx(address, value) },
            /* 0x0AF */ new Instruction { Opcode = 0x0AF, Mnemonic = "LDA", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Lda(address, value) },

            // 0xB0 - 0xBF
            /* 0x0B0 */ new Instruction { Opcode = 0x0B0, Mnemonic = "BCS", Mode = AddressingMode.Relative, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => cpu.Bcs(address, value) },
            /* 0x0B1 */ new Instruction { Opcode = 0x0B1, Mnemonic = "LDA", Mode = AddressingMode.DirectPageIndirectIndexedRead, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0B2 */ new Instruction { Opcode = 0x0B2, Mnemonic = "LDA", Mode = AddressingMode.DirectPageIndirect, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0B3 */ new Instruction { Opcode = 0x0B3, Mnemonic = "LDA", Mode = AddressingMode.StackRelativeIndirectIndexed, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0B4 */ new Instruction { Opcode = 0x0B4, Mnemonic = "LDY", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ldy(address, value) },
            /* 0x0B5 */ new Instruction { Opcode = 0x0B5, Mnemonic = "LDA", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0B6 */ new Instruction { Opcode = 0x0B6, Mnemonic = "LDX", Mode = AddressingMode.DirectPageY, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ldx(address, value) },
            /* 0x0B7 */ new Instruction { Opcode = 0x0B7, Mnemonic = "LDA", Mode = AddressingMode.DirectPageIndirectLongIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0B8 */ new Instruction { Opcode = 0x0B8, Mnemonic = "CLV", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Clv(address, value) },
            /* 0x0B9 */ new Instruction { Opcode = 0x0B9, Mnemonic = "LDA", Mode = AddressingMode.AbsoluteYRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0BA */ new Instruction { Opcode = 0x0BA, Mnemonic = "TSX", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tsx(address, value) },
            /* 0x0BB */ new Instruction { Opcode = 0x0BB, Mnemonic = "TYX", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Tyx(address, value) },
            /* 0x0BC */ new Instruction { Opcode = 0x0BC, Mnemonic = "LDY", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ldy(address, value) },
            /* 0x0BD */ new Instruction { Opcode = 0x0BD, Mnemonic = "LDA", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Lda(address, value) },
            /* 0x0BE */ new Instruction { Opcode = 0x0BE, Mnemonic = "LDX", Mode = AddressingMode.AbsoluteYRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Ldx(address, value) },
            /* 0x0BF */ new Instruction { Opcode = 0x0BF, Mnemonic = "LDA", Mode = AddressingMode.AbsoluteLongX, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Lda(address, value) },

            // 0xC0 - 0xCF
            /* 0x0C0 */ new Instruction { Opcode = 0x0C0, Mnemonic = "CPY", Mode = AddressingMode.ImmediateX, Size = 0, SizeMode = InstructionSizeMode.Index, Cycles = 2, Execute = static (cpu, address, value) => cpu.Cpy(address, value) },
            /* 0x0C1 */ new Instruction { Opcode = 0x0C1, Mnemonic = "CMP", Mode = AddressingMode.DirectPageIndexedIndirect, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0C2 */ new Instruction { Opcode = 0x0C2, Mnemonic = "REP", Mode = AddressingMode.Immediate, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Rep(address, value) },
            /* 0x0C3 */ new Instruction { Opcode = 0x0C3, Mnemonic = "CMP", Mode = AddressingMode.StackRelative, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0C4 */ new Instruction { Opcode = 0x0C4, Mnemonic = "CPY", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Cpy(address, value) },
            /* 0x0C5 */ new Instruction { Opcode = 0x0C5, Mnemonic = "CMP", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0C6 */ new Instruction { Opcode = 0x0C6, Mnemonic = "DEC", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Dec(address, value) },
            /* 0x0C7 */ new Instruction { Opcode = 0x0C7, Mnemonic = "CMP", Mode = AddressingMode.DirectPageIndirectLong, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0C8 */ new Instruction { Opcode = 0x0C8, Mnemonic = "INY", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Iny(address, value) },
            /* 0x0C9 */ new Instruction { Opcode = 0x0C9, Mnemonic = "CMP", Mode = AddressingMode.ImmediateM, Size = 0, SizeMode = InstructionSizeMode.Accumulator, Cycles = 2, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0CA */ new Instruction { Opcode = 0x0CA, Mnemonic = "DEX", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Dex(address, value) },
            /* 0x0CB */ new Instruction { Opcode = 0x0CB, Mnemonic = "WAI", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Wai(address, value) },
            /* 0x0CC */ new Instruction { Opcode = 0x0CC, Mnemonic = "CPY", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Cpy(address, value) },
            /* 0x0CD */ new Instruction { Opcode = 0x0CD, Mnemonic = "CMP", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0CE */ new Instruction { Opcode = 0x0CE, Mnemonic = "DEC", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Dec(address, value) },
            /* 0x0CF */ new Instruction { Opcode = 0x0CF, Mnemonic = "CMP", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },

            // 0xD0 - 0xDF
            /* 0x0D0 */ new Instruction { Opcode = 0x0D0, Mnemonic = "BNE", Mode = AddressingMode.Relative, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => cpu.Bne(address, value) },
            /* 0x0D1 */ new Instruction { Opcode = 0x0D1, Mnemonic = "CMP", Mode = AddressingMode.DirectPageIndirectIndexedRead, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0D2 */ new Instruction { Opcode = 0x0D2, Mnemonic = "CMP", Mode = AddressingMode.DirectPageIndirect, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0D3 */ new Instruction { Opcode = 0x0D3, Mnemonic = "CMP", Mode = AddressingMode.StackRelativeIndirectIndexed, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0D4 */ new Instruction { Opcode = 0x0D4, Mnemonic = "PEI", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Pei(address, value) },
            /* 0x0D5 */ new Instruction { Opcode = 0x0D5, Mnemonic = "CMP", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0D6 */ new Instruction { Opcode = 0x0D6, Mnemonic = "DEC", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Dec(address, value) },
            /* 0x0D7 */ new Instruction { Opcode = 0x0D7, Mnemonic = "CMP", Mode = AddressingMode.DirectPageIndirectLongIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0D8 */ new Instruction { Opcode = 0x0D8, Mnemonic = "CLD", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Cld(address, value) },
            /* 0x0D9 */ new Instruction { Opcode = 0x0D9, Mnemonic = "CMP", Mode = AddressingMode.AbsoluteYRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0DA */ new Instruction { Opcode = 0x0DA, Mnemonic = "PHX", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Phx(address, value) },
            /* 0x0DB */ new Instruction { Opcode = 0x0DB, Mnemonic = "STP", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Stp(address, value) },
            /* 0x0DC */ new Instruction { Opcode = 0x0DC, Mnemonic = "JMP", Mode = AddressingMode.AbsoluteIndirectLong, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Jml(address, value) },
            /* 0x0DD */ new Instruction { Opcode = 0x0DD, Mnemonic = "CMP", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },
            /* 0x0DE */ new Instruction { Opcode = 0x0DE, Mnemonic = "DEC", Mode = AddressingMode.AbsoluteX, Size = 3, Cycles = 7, Execute = static (cpu, address, value) => cpu.Dec(address, value) },
            /* 0x0DF */ new Instruction { Opcode = 0x0DF, Mnemonic = "CMP", Mode = AddressingMode.AbsoluteLongX, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Cmp(address, value) },

            // 0xE0 - 0xEF
            /* 0x0E0 */ new Instruction { Opcode = 0x0E0, Mnemonic = "CPX", Mode = AddressingMode.ImmediateX, Size = 0, SizeMode = InstructionSizeMode.Index, Cycles = 2, Execute = static (cpu, address, value) => cpu.Cpx(address, value) },
            /* 0x0E1 */ new Instruction { Opcode = 0x0E1, Mnemonic = "SBC", Mode = AddressingMode.DirectPageIndexedIndirect, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0E2 */ new Instruction { Opcode = 0x0E2, Mnemonic = "SEP", Mode = AddressingMode.Immediate, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Sep(address, value) },
            /* 0x0E3 */ new Instruction { Opcode = 0x0E3, Mnemonic = "SBC", Mode = AddressingMode.StackRelative, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0E4 */ new Instruction { Opcode = 0x0E4, Mnemonic = "CPX", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Cpx(address, value) },
            /* 0x0E5 */ new Instruction { Opcode = 0x0E5, Mnemonic = "SBC", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 3, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0E6 */ new Instruction { Opcode = 0x0E6, Mnemonic = "INC", Mode = AddressingMode.DirectPage, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Inc(address, value) },
            /* 0x0E7 */ new Instruction { Opcode = 0x0E7, Mnemonic = "SBC", Mode = AddressingMode.DirectPageIndirectLong, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0E8 */ new Instruction { Opcode = 0x0E8, Mnemonic = "INX", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Inx(address, value) },
            /* 0x0E9 */ new Instruction { Opcode = 0x0E9, Mnemonic = "SBC", Mode = AddressingMode.ImmediateM, Size = 0, SizeMode = InstructionSizeMode.Accumulator, Cycles = 2, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0EA */ new Instruction { Opcode = 0x0EA, Mnemonic = "NOP", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => Cpu65816.Nop(address, value) },
            /* 0x0EB */ new Instruction { Opcode = 0x0EB, Mnemonic = "XBA", Mode = AddressingMode.Implied, Size = 1, Cycles = 3, Execute = static (cpu, address, value) => cpu.Xba(address, value) },
            /* 0x0EC */ new Instruction { Opcode = 0x0EC, Mnemonic = "CPX", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Cpx(address, value) },
            /* 0x0ED */ new Instruction { Opcode = 0x0ED, Mnemonic = "SBC", Mode = AddressingMode.Absolute, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0EE */ new Instruction { Opcode = 0x0EE, Mnemonic = "INC", Mode = AddressingMode.Absolute, Size = 3, Cycles = 6, Execute = static (cpu, address, value) => cpu.Inc(address, value) },
            /* 0x0EF */ new Instruction { Opcode = 0x0EF, Mnemonic = "SBC", Mode = AddressingMode.AbsoluteLong, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },

            // 0xF0 - 0xFF
            /* 0x0F0 */ new Instruction { Opcode = 0x0F0, Mnemonic = "BEQ", Mode = AddressingMode.Relative, Size = 2, Cycles = 2, Execute = static (cpu, address, value) => cpu.Beq(address, value) },
            /* 0x0F1 */ new Instruction { Opcode = 0x0F1, Mnemonic = "SBC", Mode = AddressingMode.DirectPageIndirectIndexedRead, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0F2 */ new Instruction { Opcode = 0x0F2, Mnemonic = "SBC", Mode = AddressingMode.DirectPageIndirect, Size = 2, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0F3 */ new Instruction { Opcode = 0x0F3, Mnemonic = "SBC", Mode = AddressingMode.StackRelativeIndirectIndexed, Size = 2, Cycles = 7, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0F4 */ new Instruction { Opcode = 0x0F4, Mnemonic = "PEA", Mode = AddressingMode.Immediate16, Size = 3, Cycles = 5, Execute = static (cpu, address, value) => cpu.Pea(address, value) },
            /* 0x0F5 */ new Instruction { Opcode = 0x0F5, Mnemonic = "SBC", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0F6 */ new Instruction { Opcode = 0x0F6, Mnemonic = "INC", Mode = AddressingMode.DirectPageX, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Inc(address, value) },
            /* 0x0F7 */ new Instruction { Opcode = 0x0F7, Mnemonic = "SBC", Mode = AddressingMode.DirectPageIndirectLongIndexed, Size = 2, Cycles = 6, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0F8 */ new Instruction { Opcode = 0x0F8, Mnemonic = "SED", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Sed(address, value) },
            /* 0x0F9 */ new Instruction { Opcode = 0x0F9, Mnemonic = "SBC", Mode = AddressingMode.AbsoluteYRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0FA */ new Instruction { Opcode = 0x0FA, Mnemonic = "PLX", Mode = AddressingMode.Implied, Size = 1, Cycles = 4, Execute = static (cpu, address, value) => cpu.Plx(address, value) },
            /* 0x0FB */ new Instruction { Opcode = 0x0FB, Mnemonic = "XCE", Mode = AddressingMode.Implied, Size = 1, Cycles = 2, Execute = static (cpu, address, value) => cpu.Xce(address, value) },
            /* 0x0FC */ new Instruction { Opcode = 0x0FC, Mnemonic = "JSR", Mode = AddressingMode.AbsoluteIndexedIndirect, Size = 3, Cycles = 8, Execute = static (cpu, address, value) => cpu.Jsr(address, value) },
            /* 0x0FD */ new Instruction { Opcode = 0x0FD, Mnemonic = "SBC", Mode = AddressingMode.AbsoluteXRead, Size = 3, Cycles = 4, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },
            /* 0x0FE */ new Instruction { Opcode = 0x0FE, Mnemonic = "INC", Mode = AddressingMode.AbsoluteX, Size = 3, Cycles = 7, Execute = static (cpu, address, value) => cpu.Inc(address, value) },
            /* 0x0FF */ new Instruction { Opcode = 0x0FF, Mnemonic = "SBC", Mode = AddressingMode.AbsoluteLongX, Size = 4, Cycles = 5, Execute = static (cpu, address, value) => cpu.Sbc(address, value) },

            // 0x100 - 0x102
            /* 0x100 */ new Instruction { Opcode = 0x100, Mnemonic = "ABORT", Mode = AddressingMode.Implied, Size = 1, Cycles = 7, Execute = static (cpu, address, value) => cpu.Abo(address, value) },
            /* 0x101 */ new Instruction { Opcode = 0x101, Mnemonic = "NMI", Mode = AddressingMode.Implied, Size = 1, Cycles = 7, Execute = static (cpu, address, value) => cpu.Nmi(address, value) },
            /* 0x102 */ new Instruction { Opcode = 0x102, Mnemonic = "IRQ", Mode = AddressingMode.Implied, Size = 1, Cycles = 7, Execute = static (cpu, address, value) => cpu.Irq(address, value) },
        ];
    }
}