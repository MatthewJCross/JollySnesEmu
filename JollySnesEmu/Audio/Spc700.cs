using JollySnesEmu.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JollySnesEmu.Audio
{
    public sealed class Spc700
    {
        private readonly SnesApu _apu;
        private readonly ExecutionTracer _trace;
        private List<byte>? _instructionBytes;

        private bool _sleeping;
        private bool _stopped;

        public Spc700State State { get; } = new();

        public Spc700(SnesApu apu)
        {
            _apu = apu ?? throw new ArgumentNullException(nameof(apu));
            _trace = new ExecutionTracer("SPC700_Trace.log", 2_000_000) { Enabled = false };
            Reset();
        }

        // ------------------------------------------------------------
        // Reset and execution
        // ------------------------------------------------------------

        public void Reset()
        {
            State.A = 0;
            State.X = 0;
            State.Y = 0;
            State.StackPointer = 0xEF;
            State.ProgramCounter = 0xFFC0;
            State.Status = 0x02;

            _sleeping = false;
            _stopped = false;
        }

        public int Step()
        {
            // SLEEP and STOP halt instruction execution.
            // Return cycles so the surrounding APU can continue advancing.
            if (_sleeping || _stopped)
            {
                return 3;
            }

            ushort instructionAddress = State.ProgramCounter;

            byte a = State.A;
            byte x = State.X;
            byte y = State.Y;
            byte sp = State.StackPointer;
            byte psw = GetStatusByte();

            _instructionBytes = _trace.Enabled ? new List<byte>(3) : null;

            try
            {
                int cycles = ExecuteInstruction();

                if (_trace.Enabled)
                {
                    byte[] bytes = _instructionBytes!.ToArray();
                    string byteText = string.Join(" ", bytes.Select(b => b.ToString("X2")));
                    string assembly = GetInstructionName(bytes);

                    _trace.Write(
                        $"${instructionAddress:X4}  {byteText,-9}  {assembly,-22}  " +
                        $"A=${a:X2} X=${x:X2} Y=${y:X2} SP=${sp:X2} " +
                        $"PSW=${psw:X2} Cycles={cycles}");

                }

                return cycles;
            }
            finally
            {
                _instructionBytes = null;
            }
        }

        // ------------------------------------------------------------
        // Instruction decoder
        // ------------------------------------------------------------

        private int ExecuteInstruction()
        {
            _apu.DebugSpcProgramCounter = State.ProgramCounter;

            ushort instructionAddress = State.ProgramCounter;
            byte opcode = FetchByte();

            // Regular SET1 / CLR1 direct-page instruction family.
            if ((opcode & 0x0F) == 0x02)
            {
                return ExecuteSetClearBit(opcode);
            }

            // Regular BBS / BBC direct-page branch family.
            if ((opcode & 0x0F) == 0x03)
            {
                return ExecuteBitBranch(opcode);
            }

            // Regular OR, AND, EOR, CMP, ADC and SBC families.
            if (TryExecuteAlu(opcode, out int aluCycles))
            {
                return aluCycles;
            }

            switch (opcode)
            {
                // ----------------------------------------------------
                // Miscellaneous
                // ----------------------------------------------------

                case 0x00: // NOP
                    return 2;

                case 0xEF: // SLEEP
                    _sleeping = true;
                    return 3;

                case 0xFF: // STOP
                    _stopped = true;
                    return 3;

                case 0x0F: // BRK
                    State.Break = true;
                    State.InterruptEnable = false;

                    PushWord(State.ProgramCounter);
                    PushByte(GetStatusByte());

                    byte brkLow = _apu.ReadMemory(0xFFDE);
                    byte brkHigh = _apu.ReadMemory(0xFFDF);

                    State.ProgramCounter = (ushort)(brkLow | (brkHigh << 8));
                    return 8;

                // ----------------------------------------------------
                // Comparisons
                // ----------------------------------------------------

                case 0x1E: // CMP X,abs
                    Compare(State.X, ReadAbsolute(FetchWord()));
                    return 4;

                case 0x3E: // CMP X,dp
                    Compare(State.X, ReadDirectPage(FetchByte()));
                    return 3;

                case 0x5E: // CMP Y,abs
                    Compare(State.Y, ReadAbsolute(FetchWord()));
                    return 4;

                case 0x7E: // CMP Y,dp
                    Compare(State.Y, ReadDirectPage(FetchByte()));
                    return 3;

                case 0xAD: // CMP Y,#imm
                    Compare(State.Y, FetchByte());
                    return 2;

                case 0xC8: // CMP X,#imm
                    Compare(State.X, FetchByte());
                    return 2;

                // ----------------------------------------------------
                // Immediate loads
                // ----------------------------------------------------

                case 0xE8: // MOV A,#imm
                    State.A = FetchByte();
                    SetNegativeZero(State.A);
                    return 2;

                case 0xCD: // MOV X,#imm
                    State.X = FetchByte();
                    SetNegativeZero(State.X);
                    return 2;

                case 0x8D: // MOV Y,#imm
                    State.Y = FetchByte();
                    SetNegativeZero(State.Y);
                    return 2;

                // ----------------------------------------------------
                // Direct-page loads
                // ----------------------------------------------------

                case 0xE4: // MOV A,dp
                    State.A = ReadDirectPage(FetchByte());
                    SetNegativeZero(State.A);
                    return 3;

                case 0xF4: // MOV A,dp+X
                    State.A = ReadDirectPage(unchecked((byte)(FetchByte() + State.X)));
                    SetNegativeZero(State.A);
                    return 4;

                case 0xE6: // MOV A,(X)
                    State.A = ReadDirectPage(State.X);
                    SetNegativeZero(State.A);
                    return 3;

                case 0xBF: // MOV A,(X)+
                    State.A = ReadDirectPage(State.X++);
                    SetNegativeZero(State.A);
                    return 4;

                case 0xE7: // MOV A,[dp+X]
                    {
                        byte dp = unchecked((byte)(FetchByte() + State.X));
                        ushort address = ReadDirectPageWord(dp);
                        State.A = _apu.ReadMemory(address);
                        SetNegativeZero(State.A);
                        return 6;
                    }

                case 0xF7: // MOV A,[dp]+Y
                    {
                        byte dp = FetchByte();
                        ushort address = (ushort)(ReadDirectPageWord(dp) + State.Y);
                        State.A = _apu.ReadMemory(address);
                        SetNegativeZero(State.A);
                        return 6;
                    }

                case 0xEB: // MOV Y,dp
                    State.Y = ReadDirectPage(FetchByte());
                    SetNegativeZero(State.Y);
                    return 3;

                case 0xFB: // MOV Y,dp+X
                    State.Y = ReadDirectPage(unchecked((byte)(FetchByte() + State.X)));
                    SetNegativeZero(State.Y);
                    return 4;

                case 0xF8: // MOV X,dp
                    State.X = ReadDirectPage(FetchByte());
                    SetNegativeZero(State.X);
                    return 3;

                case 0xF9: // MOV X,dp+Y
                    State.X = ReadDirectPage(unchecked((byte)(FetchByte() + State.Y)));
                    SetNegativeZero(State.X);
                    return 4;

                // ----------------------------------------------------
                // Absolute loads
                // ----------------------------------------------------

                case 0xE5: // MOV A,abs
                    State.A = ReadAbsolute(FetchWord());
                    SetNegativeZero(State.A);
                    return 4;

                case 0xF5: // MOV A,abs+X
                    State.A = ReadAbsolute((ushort)(FetchWord() + State.X));
                    SetNegativeZero(State.A);
                    return 5;

                case 0xF6: // MOV A,abs+Y
                    State.A = ReadAbsolute((ushort)(FetchWord() + State.Y));
                    SetNegativeZero(State.A);
                    return 5;

                case 0xE9: // MOV X,abs
                    State.X = ReadAbsolute(FetchWord());
                    SetNegativeZero(State.X);
                    return 4;

                case 0xEC: // MOV Y,abs
                    State.Y = ReadAbsolute(FetchWord());
                    SetNegativeZero(State.Y);
                    return 4;

                // ----------------------------------------------------
                // Direct-page stores
                // ----------------------------------------------------

                case 0xC4: // MOV dp,A
                    WriteDirectPage(FetchByte(), State.A);
                    return 4;

                case 0xD4: // MOV dp+X,A
                    WriteDirectPage(unchecked((byte)(FetchByte() + State.X)), State.A);
                    return 5;

                case 0xC6: // MOV (X),A
                    WriteDirectPage(State.X, State.A);
                    return 4;

                case 0xAF: // MOV (X)+,A
                    WriteDirectPage(State.X++, State.A);
                    return 4;

                case 0xCB: // MOV dp,Y
                    WriteDirectPage(FetchByte(), State.Y);
                    return 4;

                case 0xDB: // MOV dp+X,Y
                    WriteDirectPage(unchecked((byte)(FetchByte() + State.X)), State.Y);
                    return 5;

                case 0xD8: // MOV dp,X
                    WriteDirectPage(FetchByte(), State.X);
                    return 4;

                case 0xD9: // MOV dp+Y,X
                    WriteDirectPage(unchecked((byte)(FetchByte() + State.Y)), State.X);
                    return 5;

                case 0x8F: // MOV dp,#imm
                    {
                        byte value = FetchByte();
                        byte address = FetchByte();
                        WriteDirectPage(address, value);
                        return 5;
                    }

                case 0xFA: // MOV dp,dp
                    {
                        byte source = FetchByte();
                        byte destination = FetchByte();
                        WriteDirectPage(destination, ReadDirectPage(source));
                        return 5;
                    }

                case 0xD7: // MOV [dp]+Y,A
                    {
                        byte dp = FetchByte();
                        ushort address = (ushort)(ReadDirectPageWord(dp) + State.Y);
                        _apu.WriteMemory(address, State.A);
                        return 7;
                    }

                case 0xC7: // MOV [dp+X],A
                    {
                        byte dp = unchecked((byte)(FetchByte() + State.X));
                        ushort address = ReadDirectPageWord(dp);
                        _apu.WriteMemory(address, State.A);
                        return 7;
                    }

                // ----------------------------------------------------
                // Absolute stores
                // ----------------------------------------------------

                case 0xC5: // MOV abs,A
                    _apu.WriteMemory(FetchWord(), State.A);
                    return 5;

                case 0xD5: // MOV abs+X,A
                    _apu.WriteMemory((ushort)(FetchWord() + State.X), State.A);
                    return 5;

                case 0xD6: // MOV abs+Y,A
                    _apu.WriteMemory((ushort)(FetchWord() + State.Y), State.A);
                    return 6;

                case 0xC9: // MOV abs,X
                    _apu.WriteMemory(FetchWord(), State.X);
                    return 5;

                case 0xCC: // MOV abs,Y
                    _apu.WriteMemory(FetchWord(), State.Y);
                    return 5;

                // ----------------------------------------------------
                // Register transfers
                // ----------------------------------------------------

                case 0x7D: // MOV A,X
                    State.A = State.X;
                    SetNegativeZero(State.A);
                    return 2;

                case 0x5D: // MOV X,A
                    State.X = State.A;
                    SetNegativeZero(State.X);
                    return 2;

                case 0xFD: // MOV Y,A
                    State.Y = State.A;
                    SetNegativeZero(State.Y);
                    return 2;

                case 0xDD: // MOV A,Y
                    State.A = State.Y;
                    SetNegativeZero(State.A);
                    return 2;

                case 0x9D: // MOV X,SP
                    State.X = State.StackPointer;
                    SetNegativeZero(State.X);
                    return 2;

                case 0xBD: // MOV SP,X
                    State.StackPointer = State.X;
                    return 2;

                // ----------------------------------------------------
                // Increment and decrement
                // ----------------------------------------------------

                case 0xBC: // INC A
                    State.A++;
                    SetNegativeZero(State.A);
                    return 2;

                case 0x9C: // DEC A
                    State.A--;
                    SetNegativeZero(State.A);
                    return 2;

                case 0x3D: // INC X
                    State.X++;
                    SetNegativeZero(State.X);
                    return 2;

                case 0x1D: // DEC X
                    State.X--;
                    SetNegativeZero(State.X);
                    return 2;

                case 0xFC: // INC Y
                    State.Y++;
                    SetNegativeZero(State.Y);
                    return 2;

                case 0xDC: // DEC Y
                    State.Y--;
                    SetNegativeZero(State.Y);
                    return 2;

                case 0xAB: // INC dp
                    ModifyDirectPage(FetchByte(), 1);
                    return 4;

                case 0x8B: // DEC dp
                    ModifyDirectPage(FetchByte(), -1);
                    return 4;

                case 0xBB: // INC dp+X
                    ModifyDirectPage(unchecked((byte)(FetchByte() + State.X)), 1);
                    return 5;

                case 0x9B: // DEC dp+X
                    ModifyDirectPage(unchecked((byte)(FetchByte() + State.X)), -1);
                    return 5;

                case 0xAC: // INC abs
                    ModifyAbsolute(FetchWord(), 1);
                    return 5;

                case 0x8C: // DEC abs
                    ModifyAbsolute(FetchWord(), -1);
                    return 5;

                case 0x3A: // INCW dp
                    ModifyDirectPageWord(FetchByte(), 1);
                    return 6;

                case 0x1A: // DECW dp
                    ModifyDirectPageWord(FetchByte(), -1);
                    return 6;

                // ----------------------------------------------------
                // Conditional branches
                // ----------------------------------------------------

                case 0x10: // BPL
                    return Branch(!State.Negative);

                case 0x30: // BMI
                    return Branch(State.Negative);

                case 0x50: // BVC
                    return Branch(!State.Overflow);

                case 0x70: // BVS
                    return Branch(State.Overflow);

                case 0x90: // BCC
                    return Branch(!State.Carry);

                case 0xB0: // BCS
                    return Branch(State.Carry);

                case 0xD0: // BNE
                    return Branch(!State.Zero);

                case 0xF0: // BEQ
                    return Branch(State.Zero);

                case 0x2F: // BRA
                    return Branch(true);

                case 0xFE: // DBNZ Y,rel
                    {
                        State.Y--;
                        sbyte offset = (sbyte)FetchByte();

                        if (State.Y != 0)
                        {
                            State.ProgramCounter = (ushort)(State.ProgramCounter + offset);
                            return 6;
                        }

                        return 4;
                    }

                case 0x6E: // DBNZ dp,rel
                    {
                        byte dp = FetchByte();
                        byte value = (byte)(ReadDirectPage(dp) - 1);
                        WriteDirectPage(dp, value);

                        sbyte offset = (sbyte)FetchByte();

                        if (value != 0)
                        {
                            State.ProgramCounter = (ushort)(State.ProgramCounter + offset);
                            return 7;
                        }

                        return 5;
                    }

                case 0x2E: // CBNE dp,rel
                    return CompareBranch(ReadDirectPage(FetchByte()), 5, 7);

                case 0xDE: // CBNE dp+X,rel
                    {
                        byte dp = unchecked((byte)(FetchByte() + State.X));
                        return CompareBranch(ReadDirectPage(dp), 6, 8);
                    }

                // ----------------------------------------------------
                // Jumps, calls and returns
                // ----------------------------------------------------

                case 0x5F: // JMP abs
                    State.ProgramCounter = FetchWord();
                    return 3;

                case 0x1F: // JMP [abs+X]
                    {
                        ushort address = (ushort)(FetchWord() + State.X);
                        byte low = _apu.ReadMemory(address);
                        byte high = _apu.ReadMemory(unchecked((ushort)(address + 1)));
                        State.ProgramCounter = (ushort)(low | (high << 8));
                        return 6;
                    }

                case 0x3F: // CALL abs
                    {
                        ushort address = FetchWord();
                        PushWord(State.ProgramCounter);
                        State.ProgramCounter = address;
                        return 8;
                    }

                case 0x6F: // RET
                    State.ProgramCounter = PopWord();
                    return 5;

                case 0x7F: // RETI
                    State.Status = PopByte();
                    State.ProgramCounter = PopWord();
                    return 6;

                case 0x4F: // PCALL upage
                    {
                        byte address = FetchByte();
                        PushWord(State.ProgramCounter);
                        State.ProgramCounter = (ushort)(0xFF00 | address);
                        return 6;
                    }

                // ----------------------------------------------------
                // Stack operations
                // ----------------------------------------------------

                case 0x0D: // PUSH PSW
                    PushByte(GetStatusByte());
                    return 4;

                case 0x2D: // PUSH A
                    PushByte(State.A);
                    return 4;

                case 0x4D: // PUSH X
                    PushByte(State.X);
                    return 4;

                case 0x6D: // PUSH Y
                    PushByte(State.Y);
                    return 4;

                case 0x8E: // POP PSW
                    State.Status = PopByte();
                    return 4;

                case 0xAE: // POP A
                    State.A = PopByte();
                    return 4;

                case 0xCE: // POP X
                    State.X = PopByte();
                    return 4;

                case 0xEE: // POP Y
                    State.Y = PopByte();
                    return 4;

                // ----------------------------------------------------
                // Flags
                // ----------------------------------------------------

                case 0x80: // SETC
                    State.Carry = true;
                    return 2;

                case 0x60: // CLRC
                    State.Carry = false;
                    return 2;

                case 0xED: // NOTC
                    State.Carry = !State.Carry;
                    return 3;

                case 0xA0: // EI
                    State.InterruptEnable = true;
                    return 3;

                case 0xC0: // DI
                    State.InterruptEnable = false;
                    return 3;

                case 0xE0: // CLRV
                    State.Overflow = false;
                    State.HalfCarry = false;
                    return 2;

                case 0x20: // CLRP
                    State.DirectPage = false;
                    return 2;

                case 0x40: // SETP
                    State.DirectPage = true;
                    return 2;

                // ----------------------------------------------------
                // Word operations
                // ----------------------------------------------------

                case 0xBA: // MOVW YA,dp
                    {
                        ushort value = ReadDirectPageWord(FetchByte());
                        SetYA(value);
                        SetNegativeZero16(value);
                        return 5;
                    }

                case 0xDA: // MOVW dp,YA
                    WriteDirectPageWord(FetchByte(), GetYA());
                    return 5;

                case 0x7A: // ADDW YA,dp
                    AddWord(ReadDirectPageWord(FetchByte()));
                    return 5;

                case 0x9A: // SUBW YA,dp
                    SubtractWord(ReadDirectPageWord(FetchByte()));
                    return 5;

                case 0x5A: // CMPW YA,dp
                    CompareWord(ReadDirectPageWord(FetchByte()));
                    return 4;

                // ----------------------------------------------------
                // Multiply, divide and decimal adjustment
                // ----------------------------------------------------

                case 0xCF: // MUL YA
                    {
                        ushort result = (ushort)(State.A * State.Y);
                        SetYA(result);
                        SetNegativeZero(State.Y);
                        return 9;
                    }

                case 0x9E: // DIV YA,X
                    Divide();
                    return 12;

                case 0xDF: // DAA
                    DecimalAdjustAddition();
                    return 3;

                case 0xBE: // DAS
                    DecimalAdjustSubtraction();
                    return 3;

                case 0x9F: // XCN A
                    State.A = (byte)((State.A << 4) | (State.A >> 4));
                    SetNegativeZero(State.A);
                    return 5;

                // ----------------------------------------------------
                // Shift and rotate instructions
                // ----------------------------------------------------

                case 0x1C: // ASL A
                    State.A = ShiftLeft(State.A);
                    return 2;

                case 0x5C: // LSR A
                    State.A = ShiftRight(State.A);
                    return 2;

                case 0x3C: // ROL A
                    State.A = RotateLeft(State.A);
                    return 2;

                case 0x7C: // ROR A
                    State.A = RotateRight(State.A);
                    return 2;

                case 0x0B: // ASL dp
                    ModifyShift(FetchByte(), 0);
                    return 4;

                case 0x4B: // LSR dp
                    ModifyShift(FetchByte(), 1);
                    return 4;

                case 0x2B: // ROL dp
                    ModifyShift(FetchByte(), 2);
                    return 4;

                case 0x6B: // ROR dp
                    ModifyShift(FetchByte(), 3);
                    return 4;

                case 0x0C: // ASL abs
                    ModifyAbsoluteShift(FetchWord(), 0);
                    return 5;

                case 0x4C: // LSR abs
                    ModifyAbsoluteShift(FetchWord(), 1);
                    return 5;

                case 0x2C: // ROL abs
                    ModifyAbsoluteShift(FetchWord(), 2);
                    return 5;

                case 0x6C: // ROR abs
                    ModifyAbsoluteShift(FetchWord(), 3);
                    return 5;

                case 0x1B: // ASL dp+X
                    ModifyShift(unchecked((byte)(FetchByte() + State.X)), 0);
                    return 5;

                case 0x5B: // LSR dp+X
                    ModifyShift(unchecked((byte)(FetchByte() + State.X)), 1);
                    return 5;

                case 0x3B: // ROL dp+X
                    ModifyShift(unchecked((byte)(FetchByte() + State.X)), 2);
                    return 5;

                case 0x7B: // ROR dp+X
                    ModifyShift(unchecked((byte)(FetchByte() + State.X)), 3);
                    return 5;

                // ----------------------------------------------------
                // Bit-addressed instructions
                // ----------------------------------------------------

                case 0x0A: // OR1 C,mem.bit
                    ExecuteCarryBit(0);
                    return 5;

                case 0x2A: // OR1 C,/mem.bit
                    ExecuteCarryBit(1);
                    return 5;

                case 0x4A: // AND1 C,mem.bit
                    ExecuteCarryBit(2);
                    return 4;

                case 0x6A: // AND1 C,/mem.bit
                    ExecuteCarryBit(3);
                    return 4;

                case 0x8A: // EOR1 C,mem.bit
                    ExecuteCarryBit(4);
                    return 5;

                case 0xAA: // MOV1 C,mem.bit
                    State.Carry = ReadBitAddress(FetchWord());
                    return 4;

                case 0xCA: // MOV1 mem.bit,C
                    WriteBitAddress(FetchWord(), State.Carry);
                    return 6;

                case 0xEA: // NOT1 mem.bit
                    ToggleBitAddress(FetchWord());
                    return 5;

                case 0x0E: // TSET1 abs
                    TestSetClearAbsolute(true);
                    return 6;

                case 0x4E: // TCLR1 abs
                    TestSetClearAbsolute(false);
                    return 6;

                // ----------------------------------------------------
                // TCALL instructions
                // ----------------------------------------------------

                case 0x01: return TCall(0);
                case 0x11: return TCall(1);
                case 0x21: return TCall(2);
                case 0x31: return TCall(3);
                case 0x41: return TCall(4);
                case 0x51: return TCall(5);
                case 0x61: return TCall(6);
                case 0x71: return TCall(7);
                case 0x81: return TCall(8);
                case 0x91: return TCall(9);
                case 0xA1: return TCall(10);
                case 0xB1: return TCall(11);
                case 0xC1: return TCall(12);
                case 0xD1: return TCall(13);
                case 0xE1: return TCall(14);
                case 0xF1: return TCall(15);

                default:
                    throw new NotImplementedException(
                        $"SPC700 opcode ${opcode:X2} at ${instructionAddress:X4} is not implemented.");
            }
        }

        // ------------------------------------------------------------
        // ALU opcode families
        // ------------------------------------------------------------

        private bool TryExecuteAlu(byte opcode, out int cycles)
        {
            cycles = 0;

            int group = opcode & 0xE0;
            int mode = opcode & 0x1F;

            if (group > 0xA0)
            {
                return false;
            }

            AluOperation operation = group switch
            {
                0x00 => AluOperation.Or,
                0x20 => AluOperation.And,
                0x40 => AluOperation.Eor,
                0x60 => AluOperation.Compare,
                0x80 => AluOperation.Add,
                0xA0 => AluOperation.Subtract,
                _ => AluOperation.None
            };

            if (operation == AluOperation.None)
            {
                return false;
            }

            byte value;

            switch (mode)
            {
                case 0x04: // A,dp
                    value = ReadDirectPage(FetchByte());
                    cycles = 3;
                    break;

                case 0x05: // A,abs
                    value = ReadAbsolute(FetchWord());
                    cycles = 4;
                    break;

                case 0x06: // A,(X)
                    value = ReadDirectPage(State.X);
                    cycles = 3;
                    break;

                case 0x07: // A,[dp+X]
                    {
                        byte dp = unchecked((byte)(FetchByte() + State.X));
                        value = _apu.ReadMemory(ReadDirectPageWord(dp));
                        cycles = 6;
                        break;
                    }

                case 0x08: // A,#imm
                    value = FetchByte();
                    cycles = 2;
                    break;

                case 0x09: // dp,dp
                    {
                        byte source = FetchByte();
                        byte destination = FetchByte();

                        byte destinationValue = ReadDirectPage(destination);
                        byte sourceValue = ReadDirectPage(source);

                        ApplyAlu(operation, destinationValue, sourceValue, out byte result);

                        if (operation != AluOperation.Compare)
                        {
                            WriteDirectPage(destination, result);
                        }

                        cycles = 6;
                        return true;
                    }

                case 0x14: // A,dp+X
                    value = ReadDirectPage(unchecked((byte)(FetchByte() + State.X)));
                    cycles = 4;
                    break;

                case 0x15: // A,abs+X
                    value = ReadAbsolute((ushort)(FetchWord() + State.X));
                    cycles = 5;
                    break;

                case 0x16: // A,abs+Y
                    value = ReadAbsolute((ushort)(FetchWord() + State.Y));
                    cycles = 5;
                    break;

                case 0x17: // A,[dp]+Y
                    {
                        byte dp = FetchByte();
                        ushort address = (ushort)(ReadDirectPageWord(dp) + State.Y);
                        value = _apu.ReadMemory(address);
                        cycles = 6;
                        break;
                    }

                case 0x18: // dp,#imm
                    {
                        byte immediate = FetchByte();
                        byte dp = FetchByte();

                        byte destinationValue = ReadDirectPage(dp);
                        ApplyAlu(operation, destinationValue, immediate, out byte result);

                        if (operation != AluOperation.Compare)
                        {
                            WriteDirectPage(dp, result);
                        }

                        cycles = 5;
                        return true;
                    }

                case 0x19: // (X),(Y)
                    {
                        byte destinationValue = ReadDirectPage(State.X);
                        byte sourceValue = ReadDirectPage(State.Y);

                        ApplyAlu(operation, destinationValue, sourceValue, out byte result);

                        if (operation != AluOperation.Compare)
                        {
                            WriteDirectPage(State.X, result);
                        }

                        cycles = 5;
                        return true;
                    }

                default:
                    return false;
            }

            ApplyAlu(operation, State.A, value, out byte accumulatorResult);

            if (operation != AluOperation.Compare)
            {
                State.A = accumulatorResult;
            }

            return true;
        }

        private void ApplyAlu(AluOperation operation, byte left, byte right, out byte result)
        {
            switch (operation)
            {
                case AluOperation.Or:
                    result = (byte)(left | right);
                    SetNegativeZero(result);
                    break;

                case AluOperation.And:
                    result = (byte)(left & right);
                    SetNegativeZero(result);
                    break;

                case AluOperation.Eor:
                    result = (byte)(left ^ right);
                    SetNegativeZero(result);
                    break;

                case AluOperation.Compare:
                    Compare(left, right);
                    result = left;
                    break;

                case AluOperation.Add:
                    result = Add(left, right);
                    break;

                case AluOperation.Subtract:
                    result = Subtract(left, right);
                    break;

                default:
                    result = left;
                    break;
            }
        }

        private enum AluOperation
        {
            None,
            Or,
            And,
            Eor,
            Compare,
            Add,
            Subtract
        }

        // ------------------------------------------------------------
        // SET1 / CLR1 and BBS / BBC
        // ------------------------------------------------------------

        private int ExecuteSetClearBit(byte opcode)
        {
            int bit = (opcode >> 5) & 0x07;
            bool set = (opcode & 0x10) == 0;

            byte address = FetchByte();
            byte value = ReadDirectPage(address);
            byte mask = (byte)(1 << bit);

            value = set
                ? (byte)(value | mask)
                : (byte)(value & ~mask);

            WriteDirectPage(address, value);
            return 4;
        }

        private int ExecuteBitBranch(byte opcode)
        {
            int bit = (opcode >> 5) & 0x07;
            bool branchWhenSet = (opcode & 0x10) == 0;

            byte address = FetchByte();
            sbyte offset = (sbyte)FetchByte();

            bool isSet = (ReadDirectPage(address) & (1 << bit)) != 0;
            bool takeBranch = isSet == branchWhenSet;

            if (takeBranch)
            {
                State.ProgramCounter = (ushort)(State.ProgramCounter + offset);
                return 7;
            }

            return 5;
        }

        // ------------------------------------------------------------
        // Bit-addressed memory
        // ------------------------------------------------------------

        private bool ReadBitAddress(ushort bitAddress)
        {
            int bit = (bitAddress >> 13) & 0x07;
            ushort address = (ushort)(bitAddress & 0x1FFF);

            return (_apu.ReadMemory(address) & (1 << bit)) != 0;
        }

        private void WriteBitAddress(ushort bitAddress, bool value)
        {
            int bit = (bitAddress >> 13) & 0x07;
            ushort address = (ushort)(bitAddress & 0x1FFF);

            byte current = _apu.ReadMemory(address);
            byte mask = (byte)(1 << bit);

            current = value
                ? (byte)(current | mask)
                : (byte)(current & ~mask);

            _apu.WriteMemory(address, current);
        }

        private void ToggleBitAddress(ushort bitAddress)
        {
            WriteBitAddress(bitAddress, !ReadBitAddress(bitAddress));
        }

        private void ExecuteCarryBit(int operation)
        {
            bool bit = ReadBitAddress(FetchWord());

            switch (operation)
            {
                case 0: // OR1
                    State.Carry |= bit;
                    break;

                case 1: // OR1 /bit
                    State.Carry |= !bit;
                    break;

                case 2: // AND1
                    State.Carry &= bit;
                    break;

                case 3: // AND1 /bit
                    State.Carry &= !bit;
                    break;

                case 4: // EOR1
                    State.Carry ^= bit;
                    break;
            }
        }

        private void TestSetClearAbsolute(bool set)
        {
            ushort address = FetchWord();
            byte value = _apu.ReadMemory(address);

            int result = State.A - value;
            SetNegativeZero((byte)result);

            value = set
                ? (byte)(value | State.A)
                : (byte)(value & ~State.A);

            _apu.WriteMemory(address, value);
        }

        // ------------------------------------------------------------
        // Shift and rotate helpers
        // ------------------------------------------------------------

        private byte ShiftLeft(byte value)
        {
            State.Carry = (value & 0x80) != 0;
            byte result = (byte)(value << 1);
            SetNegativeZero(result);
            return result;
        }

        private byte ShiftRight(byte value)
        {
            State.Carry = (value & 0x01) != 0;
            byte result = (byte)(value >> 1);
            SetNegativeZero(result);
            return result;
        }

        private byte RotateLeft(byte value)
        {
            bool oldCarry = State.Carry;
            State.Carry = (value & 0x80) != 0;

            byte result = (byte)((value << 1) | (oldCarry ? 1 : 0));
            SetNegativeZero(result);
            return result;
        }

        private byte RotateRight(byte value)
        {
            bool oldCarry = State.Carry;
            State.Carry = (value & 0x01) != 0;

            byte result = (byte)((value >> 1) | (oldCarry ? 0x80 : 0));
            SetNegativeZero(result);
            return result;
        }

        private void ModifyShift(byte address, int operation)
        {
            byte value = ReadDirectPage(address);
            value = ApplyShift(value, operation);
            WriteDirectPage(address, value);
        }

        private void ModifyAbsoluteShift(ushort address, int operation)
        {
            byte value = _apu.ReadMemory(address);
            value = ApplyShift(value, operation);
            _apu.WriteMemory(address, value);
        }

        private byte ApplyShift(byte value, int operation)
        {
            return operation switch
            {
                0 => ShiftLeft(value),
                1 => ShiftRight(value),
                2 => RotateLeft(value),
                3 => RotateRight(value),
                _ => value
            };
        }

        // ------------------------------------------------------------
        // Increment and decrement helpers
        // ------------------------------------------------------------

        private void ModifyDirectPage(byte address, int amount)
        {
            byte value = unchecked((byte)(ReadDirectPage(address) + amount));
            WriteDirectPage(address, value);
            SetNegativeZero(value);
        }

        private void ModifyAbsolute(ushort address, int amount)
        {
            byte value = unchecked((byte)(_apu.ReadMemory(address) + amount));
            _apu.WriteMemory(address, value);
            SetNegativeZero(value);
        }

        private void ModifyDirectPageWord(byte address, int amount)
        {
            ushort value = unchecked((ushort)(ReadDirectPageWord(address) + amount));
            WriteDirectPageWord(address, value);
            SetNegativeZero16(value);
        }

        // ------------------------------------------------------------
        // Branch helpers
        // ------------------------------------------------------------

        private int Branch(bool condition)
        {
            sbyte offset = (sbyte)FetchByte();

            if (condition)
            {
                State.ProgramCounter = (ushort)(State.ProgramCounter + offset);
                return 4;
            }

            return 2;
        }

        private int CompareBranch(byte value, int notTakenCycles, int takenCycles)
        {
            sbyte offset = (sbyte)FetchByte();

            if (State.A != value)
            {
                State.ProgramCounter = (ushort)(State.ProgramCounter + offset);
                return takenCycles;
            }

            return notTakenCycles;
        }

        // ------------------------------------------------------------
        // TCALL
        // ------------------------------------------------------------

        private int TCall(int number)
        {
            PushWord(State.ProgramCounter);

            ushort vectorAddress = (ushort)(0xFFDE - (number * 2));
            byte low = _apu.ReadMemory(vectorAddress);
            byte high = _apu.ReadMemory((ushort)(vectorAddress + 1));

            State.ProgramCounter = (ushort)(low | (high << 8));
            return 8;
        }

        // ------------------------------------------------------------
        // Status and flags
        // ------------------------------------------------------------

        private byte GetStatusByte()
        {
            byte status = 0;

            if (State.Negative) status |= 0x80;
            if (State.Overflow) status |= 0x40;
            if (State.DirectPage) status |= 0x20;
            if (State.Break) status |= 0x10;
            if (State.HalfCarry) status |= 0x08;
            if (State.InterruptEnable) status |= 0x04;
            if (State.Zero) status |= 0x02;
            if (State.Carry) status |= 0x01;

            return status;
        }

        private void SetNegativeZero(byte value)
        {
            State.Zero = value == 0;
            State.Negative = (value & 0x80) != 0;
        }

        private void SetNegativeZero16(ushort value)
        {
            State.Zero = value == 0;
            State.Negative = (value & 0x8000) != 0;
        }

        // ------------------------------------------------------------
        // Memory access
        // ------------------------------------------------------------

        private byte FetchByte()
        {
            byte value = _apu.ReadMemory(State.ProgramCounter++);
            _instructionBytes?.Add(value);
            return value;
        }

        private ushort FetchWord()
        {
            byte low = FetchByte();
            byte high = FetchByte();
            return (ushort)(low | (high << 8));
        }

        private byte ReadDirectPage(byte address)
        {
            ushort baseAddress = State.DirectPage ? (ushort)0x0100 : (ushort)0x0000;
            return _apu.ReadMemory((ushort)(baseAddress + address));
        }

        private void WriteDirectPage(byte address, byte value)
        {
            ushort baseAddress = State.DirectPage ? (ushort)0x0100 : (ushort)0x0000;
            _apu.WriteMemory((ushort)(baseAddress + address), value);
        }

        private ushort ReadDirectPageWord(byte address)
        {
            byte low = ReadDirectPage(address);
            byte high = ReadDirectPage(unchecked((byte)(address + 1)));
            return (ushort)(low | (high << 8));
        }

        private void WriteDirectPageWord(byte address, ushort value)
        {
            WriteDirectPage(address, (byte)value);
            WriteDirectPage(unchecked((byte)(address + 1)), (byte)(value >> 8));
        }

        private byte ReadAbsolute(ushort address)
        {
            return _apu.ReadMemory(address);
        }

        // ------------------------------------------------------------
        // Stack
        // ------------------------------------------------------------

        private void PushByte(byte value)
        {
            _apu.WriteMemory((ushort)(0x0100 | State.StackPointer), value);
            State.StackPointer--;
        }

        private byte PopByte()
        {
            State.StackPointer++;
            return _apu.ReadMemory((ushort)(0x0100 | State.StackPointer));
        }

        private void PushWord(ushort value)
        {
            PushByte((byte)(value >> 8));
            PushByte((byte)value);
        }

        private ushort PopWord()
        {
            byte low = PopByte();
            byte high = PopByte();
            return (ushort)(low | (high << 8));
        }

        // ------------------------------------------------------------
        // Comparisons
        // ------------------------------------------------------------

        private void Compare(byte left, byte right)
        {
            int result = left - right;
            State.Carry = result >= 0;
            SetNegativeZero((byte)result);
        }

        private void CompareWord(ushort value)
        {
            int result = GetYA() - value;
            State.Carry = result >= 0;
            SetNegativeZero16((ushort)result);
        }

        // ------------------------------------------------------------
        // Arithmetic
        // ------------------------------------------------------------

        private byte Add(byte register, byte value)
        {
            int carry = State.Carry ? 1 : 0;
            int result = register + value + carry;

            State.Carry = result > 0xFF;
            State.HalfCarry = ((register & 0x0F) + (value & 0x0F) + carry) > 0x0F;
            State.Overflow = (~(register ^ value) & (register ^ result) & 0x80) != 0;

            byte resultByte = (byte)result;
            SetNegativeZero(resultByte);

            return resultByte;
        }

        private byte Subtract(byte register, byte value)
        {
            int borrow = State.Carry ? 0 : 1;
            int result = register - value - borrow;

            State.Carry = result >= 0;
            State.HalfCarry = (register & 0x0F) >= ((value & 0x0F) + borrow);
            State.Overflow = ((register ^ value) & (register ^ result) & 0x80) != 0;

            byte resultByte = (byte)result;
            SetNegativeZero(resultByte);

            return resultByte;
        }

        private void AddWord(ushort value)
        {
            ushort accumulator = GetYA();
            int result = accumulator + value;

            State.Carry = result > 0xFFFF;
            State.HalfCarry = ((accumulator & 0x0FFF) + (value & 0x0FFF)) > 0x0FFF;
            State.Overflow = (~(accumulator ^ value) & (accumulator ^ result) & 0x8000) != 0;

            SetYA((ushort)result);
            SetNegativeZero16((ushort)result);
        }

        private void SubtractWord(ushort value)
        {
            ushort accumulator = GetYA();
            int result = accumulator - value;

            State.Carry = result >= 0;
            State.HalfCarry = (accumulator & 0x0FFF) >= (value & 0x0FFF);
            State.Overflow = ((accumulator ^ value) & (accumulator ^ result) & 0x8000) != 0;

            SetYA((ushort)result);
            SetNegativeZero16((ushort)result);
        }

        private ushort GetYA()
        {
            return (ushort)((State.Y << 8) | State.A);
        }

        private void SetYA(ushort value)
        {
            State.A = (byte)value;
            State.Y = (byte)(value >> 8);
        }

        // ------------------------------------------------------------
        // Division
        // ------------------------------------------------------------

        private void Divide()
        {
            int oldY = State.Y;
            int x = State.X;
            int ya = (oldY << 8) | State.A;

            if (x == 0)
            {
                State.A = 0xFF;
                State.Y = (byte)ya;
                State.HalfCarry = false;
                State.Overflow = true;
                SetNegativeZero(State.A);
                return;
            }

            int quotient;
            int remainder;

            if (oldY < (x << 1))
            {
                quotient = ya / x;
                remainder = ya % x;
            }
            else
            {
                int numerator = ya - (x << 9);
                int denominator = 0x100 - x;

                quotient = 0xFF - (numerator / denominator);
                remainder = x + (numerator % denominator);
            }

            State.HalfCarry = (x & 0x0F) <= (oldY & 0x0F);
            State.Overflow = quotient > 0xFF;

            State.A = (byte)quotient;
            State.Y = (byte)remainder;

            SetNegativeZero(State.A);
        }

        // ------------------------------------------------------------
        // Decimal adjustment
        // ------------------------------------------------------------

        private void DecimalAdjustAddition()
        {
            int value = State.A;

            if (State.Carry || value > 0x99)
            {
                value += 0x60;
                State.Carry = true;
            }

            if (State.HalfCarry || (value & 0x0F) > 9)
            {
                value += 0x06;
            }

            State.A = (byte)value;
            SetNegativeZero(State.A);
        }

        private void DecimalAdjustSubtraction()
        {
            int value = State.A;

            if (!State.Carry)
            {
                value -= 0x60;
            }

            if (!State.HalfCarry)
            {
                value -= 0x06;
            }

            State.A = (byte)value;
            SetNegativeZero(State.A);
        }

        // ------------------------------------------------------------
        // Disassembler
        // ------------------------------------------------------------

        private string GetInstructionName(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "UNKNOWN";
            }

            byte opcode = bytes[0];

            if ((opcode & 0x0F) == 0x02)
            {
                int bit = (opcode >> 5) & 7;
                return $"{((opcode & 0x10) == 0 ? "SET1" : "CLR1")} dp.{bit}";
            }

            if ((opcode & 0x0F) == 0x03)
            {
                int bit = (opcode >> 5) & 7;
                return $"{((opcode & 0x10) == 0 ? "BBS" : "BBC")} dp.{bit},rel";
            }

            string mnemonic = opcode switch
            {
                0x00 => "NOP",
                0x0F => "BRK",
                0xEF => "SLEEP",
                0xFF => "STOP",

                0x1E => "CMP X,abs",
                0x3E => "CMP X,dp",
                0x5E => "CMP Y,abs",
                0x7E => "CMP Y,dp",
                0xAD => "CMP Y,#imm",
                0xC8 => "CMP X,#imm",

                0xE8 => "MOV A,#imm",
                0xCD => "MOV X,#imm",
                0x8D => "MOV Y,#imm",
                0xE4 => "MOV A,dp",
                0xF4 => "MOV A,dp+X",
                0xE5 => "MOV A,abs",
                0xF5 => "MOV A,abs+X",
                0xF6 => "MOV A,abs+Y",
                0xE6 => "MOV A,(X)",
                0xBF => "MOV A,(X)+",
                0xE7 => "MOV A,[dp+X]",
                0xF7 => "MOV A,[dp]+Y",
                0xC4 => "MOV dp,A",
                0xD4 => "MOV dp+X,A",
                0xC5 => "MOV abs,A",
                0xD5 => "MOV abs+X,A",
                0xD6 => "MOV abs+Y,A",
                0xAF => "MOV (X)+,A",
                0xEB => "MOV Y,dp",
                0xFB => "MOV Y,dp+X",
                0xF8 => "MOV X,dp",
                0xF9 => "MOV X,dp+Y",
                0xEC => "MOV Y,abs",
                0xE9 => "MOV X,abs",
                0x7D => "MOV A,X",
                0x5D => "MOV X,A",
                0xFD => "MOV Y,A",
                0xDD => "MOV A,Y",
                0x9D => "MOV X,SP",
                0xBD => "MOV SP,X",
                0xBC => "INC A",
                0x9C => "DEC A",
                0x3D => "INC X",
                0x1D => "DEC X",
                0xFC => "INC Y",
                0xDC => "DEC Y",
                0xAB => "INC dp",
                0x8B => "DEC dp",
                0xBB => "INC dp+X",
                0x9B => "DEC dp+X",
                0xAC => "INC abs",
                0x8C => "DEC abs",
                0x3A => "INCW dp",
                0x1A => "DECW dp",
                0x10 => "BPL",
                0x30 => "BMI",
                0x50 => "BVC",
                0x70 => "BVS",
                0x90 => "BCC",
                0xB0 => "BCS",
                0xD0 => "BNE",
                0xF0 => "BEQ",
                0x2F => "BRA",
                0x6E => "DBNZ dp,rel",
                0xFE => "DBNZ Y,rel",
                0x2E => "CBNE dp,rel",
                0xDE => "CBNE dp+X,rel",
                0x5F => "JMP abs",
                0x1F => "JMP [abs+X]",
                0x3F => "CALL abs",
                0x6F => "RET",
                0x7F => "RETI",
                0x4F => "PCALL",
                0x0D => "PUSH PSW",
                0x2D => "PUSH A",
                0x4D => "PUSH X",
                0x6D => "PUSH Y",
                0x8E => "POP PSW",
                0xAE => "POP A",
                0xCE => "POP X",
                0xEE => "POP Y",
                0x80 => "SETC",
                0x60 => "CLRC",
                0xED => "NOTC",
                0xA0 => "EI",
                0xC0 => "DI",
                0xE0 => "CLRV",
                0x20 => "CLRP",
                0x40 => "SETP",
                0xBA => "MOVW YA,dp",
                0xDA => "MOVW dp,YA",
                0x7A => "ADDW YA,dp",
                0x9A => "SUBW YA,dp",
                0x5A => "CMPW YA,dp",
                0xCF => "MUL YA",
                0x9E => "DIV YA,X",
                0xDF => "DAA",
                0xBE => "DAS",
                0x9F => "XCN A",
                0x1C => "ASL A",
                0x5C => "LSR A",
                0x3C => "ROL A",
                0x7C => "ROR A",
                0x0B => "ASL dp",
                0x4B => "LSR dp",
                0x2B => "ROL dp",
                0x6B => "ROR dp",
                0x0C => "ASL abs",
                0x4C => "LSR abs",
                0x2C => "ROL abs",
                0x6C => "ROR abs",
                0x0A => "OR1 C,mem.bit",
                0x2A => "OR1 C,/mem.bit",
                0x4A => "AND1 C,mem.bit",
                0x6A => "AND1 C,/mem.bit",
                0x8A => "EOR1 C,mem.bit",
                0xAA => "MOV1 C,mem.bit",
                0xCA => "MOV1 mem.bit,C",
                0xEA => "NOT1 mem.bit",
                0x0E => "TSET1 abs",
                0x4E => "TCLR1 abs",
                0x01 => "TCALL 0",
                0x11 => "TCALL 1",
                0x21 => "TCALL 2",
                0x31 => "TCALL 3",
                0x41 => "TCALL 4",
                0x51 => "TCALL 5",
                0x61 => "TCALL 6",
                0x71 => "TCALL 7",
                0x81 => "TCALL 8",
                0x91 => "TCALL 9",
                0xA1 => "TCALL 10",
                0xB1 => "TCALL 11",
                0xC1 => "TCALL 12",
                0xD1 => "TCALL 13",
                0xE1 => "TCALL 14",
                0xF1 => "TCALL 15",
                _ => GetAluInstructionName(opcode)
            };

            if (bytes.Length == 1)
            {
                return mnemonic;
            }

            string operands = string.Join(" ", bytes.Skip(1).Select(b => $"${b:X2}"));
            return $"{mnemonic} [{operands}]";
        }

        private string GetAluInstructionName(byte opcode)
        {
            int group = opcode & 0xE0;
            int mode = opcode & 0x1F;

            string operation = group switch
            {
                0x00 => "OR",
                0x20 => "AND",
                0x40 => "EOR",
                0x60 => "CMP",
                0x80 => "ADC",
                0xA0 => "SBC",
                _ => string.Empty
            };

            if (operation.Length == 0)
            {
                return $"UNKNOWN ${opcode:X2}";
            }

            string addressing = mode switch
            {
                0x04 => "A,dp",
                0x05 => "A,abs",
                0x06 => "A,(X)",
                0x07 => "A,[dp+X]",
                0x08 => "A,#imm",
                0x09 => "dp,dp",
                0x14 => "A,dp+X",
                0x15 => "A,abs+X",
                0x16 => "A,abs+Y",
                0x17 => "A,[dp]+Y",
                0x18 => "dp,#imm",
                0x19 => "(X),(Y)",
                _ => string.Empty
            };

            return addressing.Length == 0
                ? $"UNKNOWN ${opcode:X2}"
                : $"{operation} {addressing}";
        }
    }
}