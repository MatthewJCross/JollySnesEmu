using JollySnesEmu.Memory;

namespace JollySnesEmu.CPU
{
    public sealed class Cpu65816
    {
        private const int DBR = 0;
        private const int K = 1;
        private const int A = 0;
        private const int X = 1;
        private const int Y = 2;
        private const int SP = 3;
        private const int PC = 4;
        private const int DPR = 5;
        private const int IMP = 0;
        private const int IMM = 1;
        private const int IMMm = 2;
        private const int IMMx = 3;
        private const int IMMl = 4;
        private const int DP = 5;
        private const int DPX = 6;
        private const int DPY = 7;
        private const int IDP = 8;
        private const int IDX = 9;
        private const int IDY = 10;
        private const int IDYr = 11;
        private const int IDL = 12;
        private const int ILY = 13;
        private const int SR = 14;
        private const int ISY = 15;
        private const int ABS = 16;
        private const int ABX = 17;
        private const int ABXr = 18;
        private const int ABY = 19;
        private const int ABYr = 20;
        private const int ABL = 21;
        private const int ALX = 22;
        private const int IND = 23;
        private const int IAX = 24;
        private const int IAL = 25;
        private const int REL = 26;
        private const int RLL = 27;
        private const int BM = 28;

        private byte[] _r = [];
        private ushort[] _br = [];
        private bool _n;
        private bool _v;
        private bool _m;
        private bool _x;
        private bool _d;
        private bool _i;
        private bool _z;
        private bool _c;
        private bool _e;

        public bool IrqWanted { get; set; }
        public bool NmiWanted { get; set; }
        private bool _aboWanted;

        private bool _stopped;
        private bool _waiting;

        public int CyclesLeft { get; set; }
        public Instruction? CurrentInstruction { get; private set; }
        private readonly MemoryBus _memory;

        public Cpu65816(MemoryBus memory)
        {
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        }


        private int Read(int address)
        {
            return _memory.ReadByte((uint)(address & 0xFFFFFF));
        }

        private void Write(int address, int value)
        {
            _memory.WriteByte((uint)(address & 0xFFFFFF), (byte)value);
        }

        public byte ProgramBank => _r.Length > 1 ? _r[K] : (byte)0;
        public ushort ProgramCounter => _br.Length > PC ? _br[PC] : (ushort)0;

        public bool AccumulatorIs8Bit => _e || _m;
        public bool IndexIs8Bit => _e || _x;

        public int GetInstructionSize(Instruction instruction)
        {
            ArgumentNullException.ThrowIfNull(instruction);

            return instruction.SizeMode switch
            {
                InstructionSizeMode.Accumulator => AccumulatorIs8Bit ? 2 : 3,
                InstructionSizeMode.Index => IndexIs8Bit ? 2 : 3,
                _ => instruction.Size
            };
        }

        // Executes a complete instruction, while retaining the original CPU's
        // cycle-by-cycle internal accounting.
        public int Step()
        {
            if (_memory.NmiPending)
            {
                NmiWanted = true;
                _memory.ClearNmiPending();
            }

            int cycles = 0;
            do
            {
                Cycle();
                cycles++;
            }
            while (CyclesLeft > 0);

            return cycles;
        }

        public void Reset()
        {
            _r = new byte[2];
            _br = new ushort[6];
            _br[PC] = (ushort)(Read(0xfffc) | (Read(0xfffd) << 8));
            _n = false;
            _v = false;
            _m = true;
            _x = true;
            _d = false;
            _i = false;
            _z = false;
            _c = false;
            _e = true;
            IrqWanted = false;
            NmiWanted = false;
            _aboWanted = false;
            _stopped = false;
            _waiting = false;
            CyclesLeft = 0;
            CurrentInstruction = null;
        }

        public void Cycle()
        {
            if (CyclesLeft == 0)
            {
                if (_stopped)
                {
                    CyclesLeft = 1;
                }
                else if (!_waiting)
                {
                    int instr = Read((_r[K] << 16) | _br[PC]++);
                    Instruction instruction = InstructionSet.Instructions[instr];
                    CyclesLeft = instruction.Cycles;
                    int mode = (int)instruction.Mode;
                    if (IrqWanted && !_i || NmiWanted || _aboWanted)
                    {
                        _br[PC]--;
                        if (_aboWanted)
                        {
                            _aboWanted = false;
                            instr = 0x100;
                        }
                        else if (NmiWanted)
                        {
                            NmiWanted = false;
                            instr = 0x101;
                        }
                        else
                        {
                            instr = 0x102;
                        }
                        instruction = InstructionSet.Instructions[instr];
                        CyclesLeft = instruction.Cycles;
                        mode = (int)instruction.Mode;
                    }
                    var (item1, item2) = GetAdr(mode);
                    CurrentInstruction = instruction;
                    instruction.Execute(this, item1, item2);
                }
                else
                {
                    if (_aboWanted || IrqWanted || NmiWanted)
                    {
                        _waiting = false;
                    }
                    CyclesLeft = 1;
                }
            }
            CyclesLeft--;
        }

        private byte GetP()
        {
            byte val = 0;
            val |= (byte)(_n ? 0x80 : 0);
            val |= (byte)(_v ? 0x40 : 0);
            val |= (byte)(_m ? 0x20 : 0);
            val |= (byte)(_x ? 0x10 : 0);
            val |= (byte)(_d ? 0x08 : 0);
            val |= (byte)(_i ? 0x04 : 0);
            val |= (byte)(_z ? 0x02 : 0);
            val |= (byte)(_c ? 0x01 : 0);
            return val;
        }

        private void SetP(byte value)
        {
            _n = (value & 0x80) > 0;
            _v = (value & 0x40) > 0;
            _m = (value & 0x20) > 0;
            _x = (value & 0x10) > 0;
            _d = (value & 0x08) > 0;
            _i = (value & 0x04) > 0;
            _z = (value & 0x02) > 0;
            _c = (value & 0x01) > 0;
            if (_x)
            {
                _br[X] &= 0xff;
                _br[Y] &= 0xff;
            }
        }

        private void SetZAndN(int value, bool byt)
        {
            if (byt)
            {
                _z = (value & 0xff) == 0;
                _n = (value & 0x80) > 0;
                return;
            }
            _z = (value & 0xffff) == 0;
            _n = (value & 0x8000) > 0;
        }

        private static int GetSigned(int value, bool byt)
        {
            if (byt)
            {
                return (value & 0xff) > 127 ? -(256 - (value & 0xff)) : value & 0xff;
            }
            return value > 32767 ? -(65536 - value) : value;
        }

        private void DoBranch(bool check, int rel)
        {
            if (check)
            {
                CyclesLeft++;
                _br[PC] = (ushort)(_br[PC] + rel);
            }
        }

        private void PushByte(int value)
        {
            Write(_br[SP], value);
            _br[SP]--;
        }

        private int PullByte()
        {
            _br[SP]++;
            return Read(_br[SP]);
        }

        private void PushWord(int value)
        {
            PushByte((value & 0xff00) >> 8);
            PushByte(value & 0xff);
        }

        private int PullWord()
        {
            int value = PullByte();
            value |= PullByte() << 8;
            return value;
        }

        private int ReadWord(int adr, int adrh)
        {
            int value = Read(adr);
            value |= Read(adrh) << 8;
            return value;
        }

        private void WriteWord(int adr, int adrh, int result, bool reversed = false)
        {
            if (reversed)
            {
                Write(adrh, (result & 0xff00) >> 8);
                Write(adr, result & 0xff);
            }
            else
            {
                Write(adr, result & 0xff);
                Write(adrh, (result & 0xff00) >> 8);
            }
        }

        private (int, int) GetAdr(int mode)
        {
            switch (mode)
            {
                case IMP:
                    return (0, 0);
                case IMM:
                    return ((_r[K] << 16) | _br[PC]++, 0);
                case IMMm:
                    if (_m)
                    {
                        return ((_r[K] << 16) | _br[PC]++, 0);
                    }
                    else
                    {
                        int low = (_r[K] << 16) | _br[PC]++;
                        return (low, (_r[K] << 16) | _br[PC]++);
                    }
                case IMMx:
                    if (_x)
                    {
                        return ((_r[K] << 16) | _br[PC]++, 0);
                    }
                    else
                    {
                        int low = (_r[K] << 16) | _br[PC]++;
                        return (low, (_r[K] << 16) | _br[PC]++);
                    }
                case IMMl:
                    int low2 = (_r[K] << 16) | _br[PC]++;
                    return (low2, (_r[K] << 16) | _br[PC]++);
                case DP:
                    int adr = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    return ((_br[DPR] + adr) & 0xffff, (_br[DPR] + adr + 1) & 0xffff);
                case DPX:
                    int adr2 = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    return ((_br[DPR] + adr2 + _br[X]) & 0xffff, (_br[DPR] + adr2 + _br[X] + 1) & 0xffff);
                case DPY:
                    int adr3 = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    return ((_br[DPR] + adr3 + _br[Y]) & 0xffff, (_br[DPR] + adr3 + _br[Y] + 1) & 0xffff);
                case IDP:
                    int adr4 = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    int pointer = Read((_br[DPR] + adr4) & 0xffff);
                    pointer |= Read((_br[DPR] + adr4 + 1) & 0xffff) << 8;
                    return ((_r[DBR] << 16) + pointer, (_r[DBR] << 16) + pointer + 1);
                case IDX:
                    int adr5 = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    int pointer2 = Read((_br[DPR] + adr5 + _br[X]) & 0xffff);
                    pointer2 |= Read((_br[DPR] + adr5 + _br[X] + 1) & 0xffff) << 8;
                    return ((_r[DBR] << 16) + pointer2, (_r[DBR] << 16) + pointer2 + 1);
                case IDY:
                    int adr6 = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    int pointer3 = Read((_br[DPR] + adr6) & 0xffff);
                    pointer3 |= Read((_br[DPR] + adr6 + 1) & 0xffff) << 8;
                    return ((_r[DBR] << 16) + pointer3 + _br[Y], (_r[DBR] << 16) + pointer3 + _br[Y] + 1);
                case IDYr:
                    int adr7 = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    int pointer4 = Read((_br[DPR] + adr7) & 0xffff);
                    pointer4 |= Read((_br[DPR] + adr7 + 1) & 0xffff) << 8;
                    if (pointer4 >> 8 != (pointer4 + _br[Y]) >> 8 || !_x)
                    {
                        CyclesLeft++;
                    }
                    return ((_r[DBR] << 16) + pointer4 + _br[Y], (_r[DBR] << 16) + pointer4 + _br[Y] + 1);
                case IDL:
                    int adr8 = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    int pointer5 = Read((_br[DPR] + adr8) & 0xffff);
                    pointer5 |= Read((_br[DPR] + adr8 + 1) & 0xffff) << 8;
                    pointer5 |= Read((_br[DPR] + adr8 + 2) & 0xffff) << 16;
                    return (pointer5, pointer5 + 1);
                case ILY:
                    int adr9 = Read((_r[K] << 16) | _br[PC]++);
                    if ((_br[DPR] & 0xff) != 0)
                    {
                        CyclesLeft++;
                    }
                    int pointer6 = Read((_br[DPR] + adr9) & 0xffff);
                    pointer6 |= Read((_br[DPR] + adr9 + 1) & 0xffff) << 8;
                    pointer6 |= Read((_br[DPR] + adr9 + 2) & 0xffff) << 16;
                    return (pointer6 + _br[Y], pointer6 + _br[Y] + 1);
                case SR:
                    int adr10 = Read((_r[K] << 16) | _br[PC]++);
                    return ((_br[SP] + adr10) & 0xffff, (_br[SP] + adr10 + 1) & 0xffff);
                case ISY:
                    int adr11 = Read((_r[K] << 16) | _br[PC]++);
                    int pointer7 = Read((_br[SP] + adr11) & 0xffff);
                    pointer7 |= Read((_br[SP] + adr11 + 1) & 0xffff) << 8;
                    return ((_r[DBR] << 16) + pointer7 + _br[Y], (_r[DBR] << 16) + pointer7 + _br[Y] + 1);
                case ABS:
                    int adr12 = Read((_r[K] << 16) | _br[PC]++);
                    adr12 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    return ((_r[DBR] << 16) + adr12, (_r[DBR] << 16) + adr12 + 1);
                case ABX:
                    int adr13 = Read((_r[K] << 16) | _br[PC]++);
                    adr13 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    return ((_r[DBR] << 16) + adr13 + _br[X], (_r[DBR] << 16) + adr13 + _br[X] + 1);
                case ABXr:
                    int adr14 = Read((_r[K] << 16) | _br[PC]++);
                    adr14 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    if (adr14 >> 8 != (adr14 + _br[X]) >> 8 || !_x)
                    {
                        CyclesLeft++;
                    }
                    return ((_r[DBR] << 16) + adr14 + _br[X], (_r[DBR] << 16) + adr14 + _br[X] + 1);
                case ABY:
                    int adr15 = Read((_r[K] << 16) | _br[PC]++);
                    adr15 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    return ((_r[DBR] << 16) + adr15 + _br[Y], (_r[DBR] << 16) + adr15 + _br[Y] + 1);
                case ABYr:
                    int adr16 = Read((_r[K] << 16) | _br[PC]++);
                    adr16 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    if (adr16 >> 8 != (adr16 + _br[Y]) >> 8 || !_x)
                    {
                        CyclesLeft++;
                    }
                    return ((_r[DBR] << 16) + adr16 + _br[Y], (_r[DBR] << 16) + adr16 + _br[Y] + 1);
                case ABL:
                    int adr17 = Read((_r[K] << 16) | _br[PC]++);
                    adr17 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    adr17 |= Read((_r[K] << 16) | _br[PC]++) << 16;
                    return (adr17, adr17 + 1);
                case ALX:
                    int adr18 = Read((_r[K] << 16) | _br[PC]++);
                    adr18 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    adr18 |= Read((_r[K] << 16) | _br[PC]++) << 16;
                    return (adr18 + _br[X], adr18 + _br[X] + 1);
                case IND:
                    int adr19 = Read((_r[K] << 16) | _br[PC]++);
                    adr19 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    int pointer8 = Read(adr19);
                    pointer8 |= Read((adr19 + 1) & 0xffff) << 8;
                    return ((_r[K] << 16) + pointer8, 0);
                case IAX:
                    int adr20 = Read((_r[K] << 16) | _br[PC]++);
                    adr20 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    int pointer9 = Read((_r[K] << 16) | ((adr20 + _br[X]) & 0xffff));
                    pointer9 |= Read((_r[K] << 16) | ((adr20 + _br[X] + 1) & 0xffff)) << 8;
                    return ((_r[K] << 16) + pointer9, 0);
                case IAL:
                    int adr21 = Read((_r[K] << 16) | _br[PC]++);
                    adr21 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    int pointer10 = Read(adr21);
                    pointer10 |= Read((adr21 + 1) & 0xffff) << 8;
                    pointer10 |= Read((adr21 + 2) & 0xffff) << 16;
                    return (pointer10, 0);
                case REL:
                    int rel = Read((_r[K] << 16) | _br[PC]++);
                    return (GetSigned(rel, true), 0);
                case RLL:
                    int rel2 = Read((_r[K] << 16) | _br[PC]++);
                    rel2 |= Read((_r[K] << 16) | _br[PC]++) << 8;
                    return (GetSigned(rel2, false), 0);
                case BM:
                    int dest = Read((_r[K] << 16) | _br[PC]++);
                    int src = Read((_r[K] << 16) | _br[PC]++);
                    return (dest, src);
            }
            return (0, 0);
        }

        private static void Uni() { }

        internal void Adc(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                int result;
                if (_d)
                {
                    result = (_br[A] & 0xf) + (value & 0xf) + (_c ? 1 : 0);
                    result += result > 9 ? 6 : 0;
                    result = (_br[A] & 0xf0) + (value & 0xf0) + (result > 0xf ? 0x10 : 0) + (result & 0xf);
                }
                else
                {
                    result = (_br[A] & 0xff) + value + (_c ? 1 : 0);
                }
                _v = (_br[A] & 0x80) == (value & 0x80) && (value & 0x80) != (result & 0x80);
                result += _d && result > 0x9f ? 0x60 : 0;
                _c = result > 0xff;
                SetZAndN(result, _m);
                _br[A] = (ushort)((_br[A] & 0xff00) | (result & 0xff));
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft++;
                int result;
                if (_d)
                {
                    result = (_br[A] & 0xf) + (value & 0xf) + (_c ? 1 : 0);
                    result += result > 9 ? 6 : 0;
                    result = (_br[A] & 0xf0) + (value & 0xf0) + (result > 0xf ? 0x10 : 0) + (result & 0xf);
                    result += result > 0x9f ? 0x60 : 0;
                    result = (_br[A] & 0xf00) + (value & 0xf00) + (result > 0xff ? 0x100 : 0) + (result & 0xff);
                    result += result > 0x9ff ? 0x600 : 0;
                    result = (_br[A] & 0xf000) + (value & 0xf000) + (result > 0xfff ? 0x1000 : 0) + (result & 0xfff);
                }
                else
                {
                    result = _br[A] + value + (_c ? 1 : 0);
                }
                _v = (_br[A] & 0x8000) == (value & 0x8000) && (value & 0x8000) != (result & 0x8000);
                result += _d && result > 0x9fff ? 0x6000 : 0;
                _c = result > 0xffff;
                SetZAndN(result, _m);
                _br[A] = (ushort)result;
            }
        }

        internal void Sbc(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr) ^ 0xff;
                int result;
                if (_d)
                {
                    result = (_br[A] & 0xf) + (value & 0xf) + (_c ? 1 : 0);
                    result -= result <= 0xf ? 6 : 0;
                    result = (_br[A] & 0xf0) + (value & 0xf0) + (result > 0xf ? 0x10 : 0) + (result & 0xf);
                }
                else
                {
                    result = (_br[A] & 0xff) + value + (_c ? 1 : 0);
                }
                _v = (_br[A] & 0x80) == (value & 0x80) && (value & 0x80) != (result & 0x80);
                result -= _d && result <= 0xff ? 0x60 : 0;
                _c = result > 0xff;
                SetZAndN(result, _m);
                _br[A] = (ushort)((_br[A] & 0xff00) | (result & 0xff));
            }
            else
            {
                int value = ReadWord(adr, adrh) ^ 0xffff;
                CyclesLeft++;
                int result;
                if (_d)
                {
                    result = (_br[A] & 0xf) + (value & 0xf) + (_c ? 1 : 0);
                    result -= result <= 0x0f ? 6 : 0;
                    result = (_br[A] & 0xf0) + (value & 0xf0) + (result > 0xf ? 0x10 : 0) + (result & 0xf);
                    result -= result <= 0xff ? 0x60 : 0;
                    result = (_br[A] & 0xf00) + (value & 0xf00) + (result > 0xff ? 0x100 : 0) + (result & 0xff);
                    result -= result <= 0xfff ? 0x600 : 0;
                    result = (_br[A] & 0xf000) + (value & 0xf000) + (result > 0xfff ? 0x1000 : 0) + (result & 0xfff);
                }
                else
                {
                    result = _br[A] + value + (_c ? 1 : 0);
                }
                _v = (_br[A] & 0x8000) == (value & 0x8000) && (value & 0x8000) != (result & 0x8000);
                result -= _d && result <= 0xffff ? 0x6000 : 0;
                _c = result > 0xffff;
                SetZAndN(result, _m);
                _br[A] = (ushort)result;
            }
        }

        internal void Cmp(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr) ^ 0xff;
                int result = (_br[A] & 0xff) + value + 1;
                _c = result > 0xff;
                SetZAndN(result, _m);
            }
            else
            {
                int value = ReadWord(adr, adrh) ^ 0xffff;
                CyclesLeft++;
                int result = _br[A] + value + 1;
                _c = result > 0xffff;
                SetZAndN(result, _m);
            }
        }

        internal void Cpx(int adr, int adrh)
        {
            if (_x)
            {
                int value = Read(adr) ^ 0xff;
                int result = (_br[X] & 0xff) + value + 1;
                _c = result > 0xff;
                SetZAndN(result, _x);
            }
            else
            {
                int value = ReadWord(adr, adrh) ^ 0xffff;
                CyclesLeft++;
                int result = _br[X] + value + 1;
                _c = result > 0xffff;
                SetZAndN(result, _x);
            }
        }

        internal void Cpy(int adr, int adrh)
        {
            if (_x)
            {
                int value = Read(adr) ^ 0xff;
                int result = (_br[Y] & 0xff) + value + 1;
                _c = result > 0xff;
                SetZAndN(result, _x);
            }
            else
            {
                int value = ReadWord(adr, adrh) ^ 0xffff;
                CyclesLeft++;
                int result = _br[Y] + value + 1;
                _c = result > 0xffff;
                SetZAndN(result, _x);
            }
        }

        internal void Dec(int adr, int adrh)
        {
            if (_m)
            {
                int result = (Read(adr) - 1) & 0xff;
                SetZAndN(result, _m);
                Write(adr, (byte)result);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft += 2;
                int result = (value - 1) & 0xffff;
                SetZAndN(result, _m);
                WriteWord(adr, adrh, result, true);
            }
        }

        internal void Deca(int adr, int adrh)
        {
            if (_m)
            {
                int result = ((_br[A] & 0xff) - 1) & 0xff;
                SetZAndN(result, _m);
                _br[A] = (ushort)(_br[A] & 0xff00 | result);
            }
            else
            {
                _br[A]--;
                SetZAndN(_br[A], _m);
            }
        }

        internal void Dex(int adr, int adrh)
        {
            if (_x)
            {
                int result = ((_br[X] & 0xff) - 1) & 0xff;
                SetZAndN(result, _x);
                _br[X] = (ushort)result;
            }
            else
            {
                _br[X]--;
                SetZAndN(_br[X], _x);
            }
        }

        internal void Dey(int adr, int adrh)
        {
            if (_x)
            {
                int result = ((_br[Y] & 0xff) - 1) & 0xff;
                SetZAndN(result, _x);
                _br[Y] = (ushort)result;
            }
            else
            {
                _br[Y]--;
                SetZAndN(_br[Y], _x);
            }
        }

        internal void Inc(int adr, int adrh)
        {
            if (_m)
            {
                int result = (Read(adr) + 1) & 0xff;
                SetZAndN(result, _m);
                Write(adr, (byte)result);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft += 2;
                int result = (value + 1) & 0xffff;
                SetZAndN(result, _m);
                WriteWord(adr, adrh, result, true);
            }
        }

        internal void Inca(int adr, int adrh)
        {
            if (_m)
            {
                int result = ((_br[A] & 0xff) + 1) & 0xff;
                SetZAndN(result, _m);
                _br[A] = (ushort)(_br[A] & 0xff00 | result);
            }
            else
            {
                _br[A]++;
                SetZAndN(_br[A], _m);
            }
        }

        internal void Inx(int adr, int adrh)
        {
            if (_x)
            {
                int result = ((_br[X] & 0xff) + 1) & 0xff;
                SetZAndN(result, _x);
                _br[X] = (ushort)result;
            }
            else
            {
                _br[X]++;
                SetZAndN(_br[X], _x);
            }
        }

        internal void Iny(int adr, int adrh)
        {
            if (_x)
            {
                int result = ((_br[Y] & 0xff) + 1) & 0xff;
                SetZAndN(result, _x);
                _br[Y] = (ushort)result;
            }
            else
            {
                _br[Y]++;
                SetZAndN(_br[Y], _x);
            }
        }

        internal void And(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                _br[A] = (ushort)((_br[A] & 0xff00) | (_br[A] & value & 0xff));
                SetZAndN(_br[A], _m);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft++;
                _br[A] &= (ushort)value;
                SetZAndN(_br[A], _m);
            }
        }

        internal void Eor(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                _br[A] = (ushort)((_br[A] & 0xff00) | ((_br[A] ^ value) & 0xff));
                SetZAndN(_br[A], _m);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft++;
                _br[A] ^= (ushort)value;
                SetZAndN(_br[A], _m);
            }
        }

        internal void Ora(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                _br[A] = (ushort)((_br[A] & 0xff00) | ((_br[A] | value) & 0xff));
                SetZAndN(_br[A], _m);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft++;
                _br[A] |= (ushort)value;
                SetZAndN(_br[A], _m);
            }
        }

        internal void Bit(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                int result = _br[A] & 0xff & value;
                _z = result == 0;
                _n = (value & 0x80) > 0;
                _v = (value & 0x40) > 0;
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft++;
                int result = _br[A] & value;
                _z = result == 0;
                _n = (value & 0x8000) > 0;
                _v = (value & 0x4000) > 0;
            }
        }

        internal void Biti(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                int result = _br[A] & 0xff & value;
                _z = result == 0;
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft++;
                int result = _br[A] & value;
                _z = result == 0;
            }
        }

        internal void Trb(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                int result = _br[A] & 0xff & value;
                value = value & ~(_br[A] & 0xff) & 0xff;
                _z = result == 0;
                Write(adr, (byte)value);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft += 2;
                int result = _br[A] & value;
                value = value & ~_br[A] & 0xffff;
                _z = result == 0;
                WriteWord(adr, adrh, value, true);
            }
        }

        internal void Tsb(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                int result = _br[A] & 0xff & value;
                value = (value | (_br[A] & 0xff)) & 0xff;
                _z = result == 0;
                Write(adr, (byte)value);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft += 2;
                int result = _br[A] & value;
                value = (value | _br[A]) & 0xffff;
                _z = result == 0;
                WriteWord(adr, adrh, value, true);
            }
        }

        internal void Asl(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                _c = (value & 0x80) > 0;
                value <<= 1;
                SetZAndN(value, _m);
                Write(adr, value);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft += 2;
                _c = (value & 0x8000) > 0;
                value <<= 1;
                SetZAndN(value, _m);
                WriteWord(adr, adrh, value, true);
            }
        }

        internal void Asla(int adr, int adrh)
        {
            if (_m)
            {
                int value = _br[A] & 0xff;
                _c = (value & 0x80) > 0;
                value <<= 1;
                SetZAndN(value, _m);
                _br[A] = (ushort)((_br[A] & 0xff00) | (value & 0xff));
            }
            else
            {
                _c = (_br[A] & 0x8000) > 0;
                CyclesLeft += 2;
                _br[A] <<= 1;
                SetZAndN(_br[A], _m);
            }
        }

        internal void Lsr(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                _c = (value & 0x1) > 0;
                value >>= 1;
                SetZAndN(value, _m);
                Write(adr, value);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft += 2;
                _c = (value & 0x1) > 0;
                value >>= 1;
                SetZAndN(value, _m);
                WriteWord(adr, adrh, value, true);
            }
        }

        internal void Lsra(int adr, int adrh)
        {
            if (_m)
            {
                int value = _br[A] & 0xff;
                _c = (value & 0x1) > 0;
                value >>= 1;
                SetZAndN(value, _m);
                _br[A] = (ushort)((_br[A] & 0xff00) | (value & 0xff));
            }
            else
            {
                _c = (_br[A] & 0x1) > 0;
                CyclesLeft += 2;
                _br[A] >>= 1;
                SetZAndN(_br[A], _m);
            }
        }

        internal void Rol(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                value = (value << 1) | (_c ? 1 : 0);
                _c = (value & 0x100) > 0;
                SetZAndN(value, _m);
                Write(adr, (byte)value);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft += 2;
                value = (value << 1) | (_c ? 1 : 0);
                _c = (value & 0x10000) > 0;
                SetZAndN(value, _m);
                WriteWord(adr, adrh, value, true);
            }
        }

        internal void Rola(int adr, int adrh)
        {
            if (_m)
            {
                int value = _br[A] & 0xff;
                value = (value << 1) | (_c ? 1 : 0);
                _c = (value & 0x100) > 0;
                SetZAndN(value, _m);
                _br[A] = (ushort)((_br[A] & 0xff00) | (value & 0xff));
            }
            else
            {
                CyclesLeft += 2;
                int value = (_br[A] << 1) | (_c ? 1 : 0);
                _c = (value & 0x10000) > 0;
                SetZAndN(value, _m);
                _br[A] = (ushort)value;
            }
        }

        internal void Ror(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                int carry = value & 0x1;
                value = (value >> 1) | (_c ? 0x80 : 0);
                _c = carry > 0;
                SetZAndN(value, _m);
                Write(adr, (byte)value);
            }
            else
            {
                int value = ReadWord(adr, adrh);
                CyclesLeft += 2;
                int carry = value & 0x1;
                value = (value >> 1) | (_c ? 0x8000 : 0);
                _c = carry > 0;
                SetZAndN(value, _m);
                WriteWord(adr, adrh, value, true);
            }
        }

        internal void Rora(int adr, int adrh)
        {
            if (_m)
            {
                int value = _br[A] & 0xff;
                int carry = value & 0x1;
                value = (value >> 1) | (_c ? 0x80 : 0);
                _c = carry > 0;
                SetZAndN(value, _m);
                _br[A] = (ushort)((_br[A] & 0xff00) | (value & 0xff));
            }
            else
            {
                CyclesLeft += 2;
                int carry = _br[A] & 0x1;
                int value = (_br[A] >> 1) | (_c ? 0x8000 : 0);
                _c = carry > 0;
                SetZAndN(value, _m);
                _br[A] = (ushort)value;
            }
        }

        internal void Bcc(int adr, int adrh)
        {
            DoBranch(!_c, adr);
        }

        internal void Bcs(int adr, int adrh)
        {
            DoBranch(_c, adr);
        }

        internal void Beq(int adr, int adrh)
        {
            DoBranch(_z, adr);
        }

        internal void Bmi(int adr, int adrh)
        {
            DoBranch(_n, adr);
        }

        internal void Bne(int adr, int adrh)
        {
            DoBranch(!_z, adr);
        }

        internal void Bpl(int adr, int adrh)
        {
            DoBranch(!_n, adr);
        }

        internal void Bra(int adr, int adrh)
        {
            _br[PC] = (ushort)(_br[PC] + adr);
        }

        internal void Bvc(int adr, int adrh)
        {
            DoBranch(!_v, adr);
        }

        internal void Bvs(int adr, int adrh)
        {
            DoBranch(_v, adr);
        }

        internal void Brl(int adr, int adrh)
        {
            _br[PC] = (ushort)(_br[PC] + adr);
        }

        internal void Jmp(int adr, int adrh)
        {
            _br[PC] = (ushort)(adr & 0xffff);
        }

        internal void Jml(int adr, int adrh)
        {
            _r[K] = (byte)((adr & 0xff0000) >> 16);
            _br[PC] = (ushort)(adr & 0xffff);
        }

        internal void Jsl(int adr, int adrh)
        {
            int pushPc = (_br[PC] - 1) & 0xffff;
            PushByte(_r[K]);
            PushWord(pushPc);
            _r[K] = (byte)((adr & 0xff0000) >> 16);
            _br[PC] = (ushort)(adr & 0xffff);
        }

        internal void Jsr(int adr, int adrh)
        {
            int pushPc = (_br[PC] - 1) & 0xffff;
            PushWord(pushPc);
            _br[PC] = (ushort)(adr & 0xffff);
        }

        internal void Rtl(int adr, int adrh)
        {
            int pullPc = PullWord();
            _r[K] = (byte)PullByte();
            _br[PC] = (ushort)(pullPc + 1);
        }

        internal void Rts(int adr, int adrh)
        {
            int pullPc = PullWord();
            _br[PC] = (ushort)(pullPc + 1);
        }

        internal void Brk(int adr, int adrh)
        {
            int pushPc = (_br[PC] + 1) & 0xffff;
            PushByte(_r[K]);
            PushWord(pushPc);
            PushByte(GetP());
            CyclesLeft++;
            _i = true;
            _d = false;
            _r[K] = 0;
            _br[PC] = (ushort)(Read(0xffe6) | (Read(0xffe7) << 8));
        }

        internal void Cop(int adr, int adrh)
        {
            PushByte(_r[K]);
            PushWord(_br[PC]);
            PushByte(GetP());
            CyclesLeft++;
            _i = true;
            _d = false;
            _r[K] = 0;
            _br[PC] = (ushort)(Read(0xffe4) | (Read(0xffe5) << 8));
        }

        internal void Abo(int adr, int adrh)
        {
            PushByte(_r[K]);
            PushWord(_br[PC]);
            PushByte(GetP());
            CyclesLeft++;
            _i = true;
            _d = false;
            _r[K] = 0;
            _br[PC] = (ushort)(Read(0xffe8) | (Read(0xffe9) << 8));
        }

        internal void Nmi(int adr, int adrh)
        {
            PushByte(_r[K]);
            PushWord(_br[PC]);
            PushByte(GetP());
            CyclesLeft++;
            _i = true;
            _d = false;
            _r[K] = 0;
            _br[PC] = (ushort)(Read(0xffea) | (Read(0xffeb) << 8));
        }

        internal void Irq(int adr, int adrh)
        {
            PushByte(_r[K]);
            PushWord(_br[PC]);
            PushByte(GetP());
            CyclesLeft++;
            _i = true;
            _d = false;
            _r[K] = 0;
            _br[PC] = (ushort)(Read(0xffee) | (Read(0xffef) << 8));
        }

        internal void Rti(int adr, int adrh)
        {
            SetP((byte)PullByte());
            CyclesLeft++;
            int pullPc = PullWord();
            _r[K] = (byte)PullByte();
            _br[PC] = (ushort)pullPc;
        }

        internal void Clc(int adr, int adrh)
        {
            _c = false;
        }

        internal void Cld(int adr, int adrh)
        {
            _d = false;
        }

        internal void Cli(int adr, int adrh)
        {
            _i = false;
        }

        internal void Clv(int adr, int adrh)
        {
            _v = false;
        }

        internal void Sec(int adr, int adrh)
        {
            _c = true;
        }

        internal void Sed(int adr, int adrh)
        {
            _d = true;
        }

        internal void Sei(int adr, int adrh)
        {
            _i = true;
        }

        internal void Rep(int adr, int adrh)
        {
            int value = Read(adr);
            SetP((byte)(GetP() & ~value));
        }

        internal void Sep(int adr, int adrh)
        {
            int value = Read(adr);
            SetP((byte)(GetP() | value));
        }

        internal void Lda(int adr, int adrh)
        {
            if (_m)
            {
                int value = Read(adr);
                _br[A] = (ushort)((_br[A] & 0xff00) | (value & 0xff));
                SetZAndN(value, _m);
            }
            else
            {
                CyclesLeft++;
                _br[A] = (ushort)ReadWord(adr, adrh);
                SetZAndN(_br[A], _m);
            }
        }

        internal void Ldx(int adr, int adrh)
        {
            if (_x)
            {
                _br[X] = (ushort)Read(adr);
                SetZAndN(_br[X], _x);
            }
            else
            {
                CyclesLeft++;
                _br[X] = (ushort)ReadWord(adr, adrh);
                SetZAndN(_br[X], _x);
            }
        }

        internal void Ldy(int adr, int adrh)
        {
            if (_x)
            {
                _br[Y] = (ushort)Read(adr);
                SetZAndN(_br[Y], _x);
            }
            else
            {
                CyclesLeft++;
                _br[Y] = (ushort)ReadWord(adr, adrh);
                SetZAndN(_br[Y], _x);
            }
        }

        internal void Sta(int adr, int adrh)
        {
            if (_m)
            {
                Write(adr, (byte)(_br[A] & 0xff));
            }
            else
            {
                CyclesLeft++;
                WriteWord(adr, adrh, _br[A]);
            }
        }

        internal void Stx(int adr, int adrh)
        {
            if (_x)
            {
                Write(adr, (byte)(_br[X] & 0xff));
            }
            else
            {
                CyclesLeft++;
                WriteWord(adr, adrh, _br[X]);
            }
        }

        internal void Sty(int adr, int adrh)
        {
            if (_x)
            {
                Write(adr, (byte)(_br[Y] & 0xff));
            }
            else
            {
                CyclesLeft++;
                WriteWord(adr, adrh, _br[Y]);
            }
        }

        internal void Stz(int adr, int adrh)
        {
            if (_m)
            {
                Write(adr, 0);
            }
            else
            {
                CyclesLeft++;
                WriteWord(adr, adrh, 0);
            }
        }

        internal void Mvn(int adr, int adrh)
        {
            _r[DBR] = (byte)adr;
            Write((adr << 16) | _br[Y], Read((adrh << 16) | _br[X]));
            _br[A]--;
            _br[X]++;
            _br[Y]++;
            if (_br[A] != 0xffff)
            {
                _br[PC] -= 3;
            }
            if (_x)
            {
                _br[X] &= 0xff;
                _br[Y] &= 0xff;
            }
        }

        internal void Mvp(int adr, int adrh)
        {
            _r[DBR] = (byte)adr;
            Write((adr << 16) | _br[Y], Read((adrh << 16) | _br[X]));
            _br[A]--;
            _br[X]--;
            _br[Y]--;
            if (_br[A] != 0xffff)
            {
                _br[PC] -= 3;
            }
            if (_x)
            {
                _br[X] &= 0xff;
                _br[Y] &= 0xff;
            }
        }

        internal static void Nop(int adr, int adrh) { }

        internal static void Wdm(int adr, int adrh) { }

        internal void Pea(int adr, int adrh)
        {
            PushWord(ReadWord(adr, adrh));
        }

        internal void Pei(int adr, int adrh)
        {
            PushWord(ReadWord(adr, adrh));
        }

        internal void Per(int adr, int adrh)
        {
            PushWord((_br[PC] + adr) & 0xffff);
        }

        internal void Pha(int adr, int adrh)
        {
            if (_m)
            {
                PushByte((byte)(_br[A] & 0xff));
            }
            else
            {
                CyclesLeft++;
                PushWord(_br[A]);
            }
        }

        internal void Phx(int adr, int adrh)
        {
            if (_x)
            {
                PushByte((byte)(_br[X] & 0xff));
            }
            else
            {
                CyclesLeft++;
                PushWord(_br[X]);
            }
        }

        internal void Phy(int adr, int adrh)
        {
            if (_x)
            {
                PushByte((byte)(_br[Y] & 0xff));
            }
            else
            {
                CyclesLeft++;
                PushWord(_br[Y]);
            }
        }

        internal void Pla(int adr, int adrh)
        {
            if (_m)
            {
                _br[A] = (ushort)((_br[A] & 0xff00) | (PullByte() & 0xff));
                SetZAndN(_br[A], _m);
            }
            else
            {
                CyclesLeft++;
                _br[A] = (ushort)PullWord();
                SetZAndN(_br[A], _m);
            }
        }

        internal void Plx(int adr, int adrh)
        {
            if (_x)
            {
                _br[X] = (ushort)PullByte();
                SetZAndN(_br[X], _m);
            }
            else
            {
                CyclesLeft++;
                _br[X] = (ushort)PullWord();
                SetZAndN(_br[X], _m);
            }
        }

        internal void Ply(int adr, int adrh)
        {
            if (_x)
            {
                _br[Y] = (ushort)PullByte();
                SetZAndN(_br[Y], _m);
            }
            else
            {
                CyclesLeft++;
                _br[Y] = (ushort)PullWord();
                SetZAndN(_br[Y], _m);
            }
        }

        internal void Phb(int adr, int adrh)
        {
            PushByte(_r[DBR]);
        }

        internal void Phd(int adr, int adrh)
        {
            PushWord(_br[DPR]);
        }

        internal void Phk(int adr, int adrh)
        {
            PushByte(_r[K]);
        }

        internal void Php(int adr, int adrh)
        {
            PushByte(GetP());
        }

        internal void Plb(int adr, int adrh)
        {
            _r[DBR] = (byte)PullByte();
            SetZAndN(_r[DBR], true);
        }

        internal void Pld(int adr, int adrh)
        {
            _br[DPR] = (ushort)PullWord();
            SetZAndN(_br[DPR], false);
        }

        internal void Plp(int adr, int adrh)
        {
            SetP((byte)PullByte());
        }

        internal void Stp(int adr, int adrh)
        {
            _stopped = true;
        }

        internal void Wai(int adr, int adrh)
        {
            _waiting = true;
        }

        internal void Tax(int adr, int adrh)
        {
            if (_x)
            {
                _br[X] = (ushort)(_br[A] & 0xff);
                SetZAndN(_br[X], _x);
            }
            else
            {
                _br[X] = _br[A];
                SetZAndN(_br[X], _x);
            }
        }

        internal void Tay(int adr, int adrh)
        {
            if (_x)
            {
                _br[Y] = (ushort)(_br[A] & 0xff);
                SetZAndN(_br[Y], _x);
            }
            else
            {
                _br[Y] = _br[A];
                SetZAndN(_br[Y], _x);
            }
        }

        internal void Tsx(int adr, int adrh)
        {
            if (_x)
            {
                _br[X] = (ushort)(_br[SP] & 0xff);
                SetZAndN(_br[X], _x);
            }
            else
            {
                _br[X] = _br[SP];
                SetZAndN(_br[X], _x);
            }
        }

        internal void Txa(int adr, int adrh)
        {
            if (_m)
            {
                _br[A] = (ushort)((_br[A] & 0xff00) | (_br[X] & 0xff));
                SetZAndN(_br[A], _m);
            }
            else
            {
                _br[A] = _br[X];
                SetZAndN(_br[A], _m);
            }
        }

        internal void Txs(int adr, int adrh)
        {
            _br[SP] = _br[X];
        }

        internal void Txy(int adr, int adrh)
        {
            if (_x)
            {
                _br[Y] = (ushort)(_br[X] & 0xff);
                SetZAndN(_br[Y], _x);
            }
            else
            {
                _br[Y] = _br[X];
                SetZAndN(_br[Y], _x);
            }
        }

        internal void Tya(int adr, int adrh)
        {
            if (_m)
            {
                _br[A] = (ushort)((_br[A] & 0xff00) | (_br[Y] & 0xff));
                SetZAndN(_br[A], _m);
            }
            else
            {
                _br[A] = _br[Y];
                SetZAndN(_br[A], _m);
            }
        }

        internal void Tyx(int adr, int adrh)
        {
            if (_x)
            {
                _br[X] = (ushort)(_br[Y] & 0xff);
                SetZAndN(_br[X], _x);
            }
            else
            {
                _br[X] = _br[Y];
                SetZAndN(_br[X], _x);
            }
        }

        internal void Tcd(int adr, int adrh)
        {
            _br[DPR] = _br[A];
            SetZAndN(_br[DPR], false);
        }

        internal void Tcs(int adr, int adrh)
        {
            _br[SP] = _br[A];
        }

        internal void Tdc(int adr, int adrh)
        {
            _br[A] = _br[DPR];
            SetZAndN(_br[A], false);
        }

        internal void Tsc(int adr, int adrh)
        {
            _br[A] = _br[SP];
            SetZAndN(_br[A], false);
        }

        internal void Xba(int adr, int adrh)
        {
            int low = _br[A] & 0xff;
            int high = (_br[A] & 0xff00) >> 8;
            _br[A] = (ushort)((low << 8) | high);
            SetZAndN(_br[A], true);
        }

        internal void Xce(int adr, int adrh)
        {
            bool temp = _c;
            _c = _e;
            _e = temp;
            if (_e)
            {
                _m = true;
                _x = true;
            }
            if (_x)
            {
                _br[X] &= 0xff;
                _br[Y] &= 0xff;
            }
        }
    }
}