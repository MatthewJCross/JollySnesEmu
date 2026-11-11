using System.Diagnostics;
using JollySnesEmu.Diagnostics;

namespace JollySnesEmu.Audio
{
    public sealed class SnesApu
    {
        public const int RamSize = 0x10000;

        public byte[] RAM => _ram;
        public SnesDsp Dsp { get; } = new();
        private byte _dspAddress;
        private int _dspCycleRemainder;
        public event Action<float[], float[], int>? SamplesReady;

        private readonly byte[] _ram = new byte[RamSize];

        // Main CPU -> SPC700
        private readonly byte[] _cpuToApu = new byte[4];

        // SPC700 -> Main CPU
        private readonly byte[] _apuToCpu = new byte[4];

        private readonly byte[] _timerTargets = new byte[3];
        private readonly byte[] _timerCounters = new byte[3];
        private readonly int[] _timerDividers = new int[3];
        private static readonly int[] TimerPeriods = [128, 128, 16];
        private readonly int[] _timerStages = new int[3];

        private readonly ExecutionTracer _ioTrace = new("APU_IO_Trace.log", 500_000) { Enabled = false };

        // ------------------------------------------------------------
        // IPL ROM
        // ------------------------------------------------------------

        private static readonly byte[] IplRom =
        [
            0xCD, 0xEF, 0xBD, 0xE8, 0x00, 0xC6, 0x1D, 0xD0,
            0xFC, 0x8F, 0xAA, 0xF4, 0x8F, 0xBB, 0xF5, 0x78,
            0xCC, 0xF4, 0xD0, 0xFB, 0x2F, 0x19, 0xEB, 0xF4,
            0xD0, 0xFC, 0x7E, 0xF4, 0xD0, 0x0B, 0xE4, 0xF5,
            0xCB, 0xF4, 0xD7, 0x00, 0xFC, 0xD0, 0xF3, 0xAB,
            0x01, 0x10, 0xEF, 0x7E, 0xF4, 0x10, 0xEB, 0xBA,
            0xF6, 0xDA, 0x00, 0xBA, 0xF4, 0xC4, 0xF4, 0xDD,
            0x5D, 0xD0, 0xDB, 0x1F, 0x00, 0x00, 0xC0, 0xFF
        ];

        private byte _control = 0x80;
        public bool IplRomEnabled => (_control & 0x80) != 0;
        public ushort DebugSpcProgramCounter { get; set; }

        // ------------------------------------------------------------
        // Reset
        // ------------------------------------------------------------

        public void Reset()
        {
            Array.Clear(_ram);
            Array.Clear(_cpuToApu);
            Array.Clear(_apuToCpu);

            Array.Clear(_timerTargets);
            Array.Clear(_timerCounters);
            Array.Clear(_timerDividers);
            Array.Clear(_timerStages);

            _control = 0x80;
            _dspAddress = 0;
            _dspCycleRemainder = 0;
            Dsp.SetAPU(this);
            Dsp.Reset();
        }

        // ------------------------------------------------------------
        // Main CPU Communication
        // ------------------------------------------------------------

        public byte ReadCpuPort(int port)
        {
            ValidatePort(port);
            byte value = _apuToCpu[port];
            return value;
        }

        private int _apuTransferWrapCount;
        public void WriteCpuPort(int port, byte value)
        {
            ValidatePort(port);

            _cpuToApu[port] = value;

            if (port == 0 && value == 0x00)
            {
                _apuTransferWrapCount++;
            }
        }

        // ------------------------------------------------------------
        // SPC700 Communication
        // ------------------------------------------------------------

        public byte ReadApuPort(int port)
        {
            ValidatePort(port);
            byte value = _cpuToApu[port];
            return value;
        }

        public void WriteApuPort(int port, byte value)
        {
            ValidatePort(port);
            _apuToCpu[port] = value;
        }

        // ------------------------------------------------------------
        // APU Memory
        // ------------------------------------------------------------

        public byte ReadMemory(ushort address)
        {
            if (address == 0x00F2)
                return _dspAddress;

            if (address == 0x00F3)
                return Dsp.Read(_dspAddress);

            if (address >= 0x00F4 && address <= 0x00F7)
                return ReadApuPort(address - 0x00F4);

            if (address >= 0x00FD && address <= 0x00FF)
            {
                int timer = address - 0x00FD;
                byte value = _timerCounters[timer];
                _timerCounters[timer] = 0;
                return value;
            }

            if (address == 0x00F1)
                return 0;

            if ((_control & 0x80) != 0 && address >= 0xFFC0)
                return IplRom[address - 0xFFC0];

            return _ram[address];
        }

        public void WriteMemory(ushort address, byte value)
        {
            _ioTrace.Write($"APU WRITE: PC=${DebugSpcProgramCounter:X4} ADDRESS=${address:X4} VALUE=${value:X2}");
            _ram[address] = value;

            if (address == 0x00F2)
            {
                _dspAddress = (byte)(value & 0x7F);
                return;
            }

            if (address == 0x00F3)
            {
                Dsp.Write(_dspAddress, value);
                return;
            }

            // APU control register
            if (address == 0x00F1)
            {
                _ioTrace.Write($"SPC PC=${DebugSpcProgramCounter:X4} WRITE $00F1 = ${value:X2} CONTROL");
                byte previousControl = _control;
                _control = value;

                // Clear CPU -> APU input ports 0 and 1.
                if ((value & 0x10) != 0)
                {
                    _cpuToApu[0] = 0;
                    _cpuToApu[1] = 0;
                }

                // Clear CPU -> APU input ports 2 and 3.
                if ((value & 0x20) != 0)
                {
                    _cpuToApu[2] = 0;
                    _cpuToApu[3] = 0;
                }

                // Update timer enable states
                for (int timer = 0; timer < 3; timer++)
                {
                    int mask = 1 << timer;

                    bool wasEnabled = (previousControl & mask) != 0;
                    bool isEnabled = (value & mask) != 0;

                    if (!wasEnabled && isEnabled)
                    {
                        _timerDividers[timer] = 0;
                        _timerStages[timer] = 0;
                        _timerCounters[timer] = 0;
                    }
                }

                return;
            }

            // Timer target registers
            if (address >= 0x00FA && address <= 0x00FC)
            {
                int timer = address - 0x00FA;
                _timerTargets[timer] = value;
                _ioTrace.Write($"SPC PC=${DebugSpcProgramCounter:X4} WRITE ${address:X4} = ${value:X2} TIMER {timer} TARGET");
                return;
            }

            // CPU -> APU communication ports
            if (address >= 0x00F4 && address <= 0x00F7)
            {
                _ioTrace.Write($"SPC PC=${DebugSpcProgramCounter:X4} WRITE ${address:X4} = ${value:X2} COMMUNICATION PORT");
                WriteApuPort(address - 0x00F4, value);
                return;
            }
        }

        // ------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------

        private static void ValidatePort(int port)
        {
            if ((uint)port >= 4)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }
        }

        // The reference DSP produces one stereo sample per 32 SPC700 clock cycles.
        public void AdvanceDsp(int cycles)
        {
            _dspCycleRemainder += cycles;

            while (_dspCycleRemainder >= 32)
            {
                _dspCycleRemainder -= 32;

                Dsp.Cycle();

                if (Dsp.SampleOffset >= Dsp.SamplesL.Length)
                {
                    SamplesReady?.Invoke(Dsp.SamplesL, Dsp.SamplesR, Dsp.SampleOffset);
                    Dsp.SampleOffset = 0;
                }
            }
        }

        public void AdvanceTimers(int cycles)
        {
            if (cycles <= 0)
                return;

            for (int timer = 0; timer < 3; timer++)
            {
                if ((_control & (1 << timer)) == 0)
                    continue;

                _timerDividers[timer] += cycles;

                while (_timerDividers[timer] >= TimerPeriods[timer])
                {
                    _timerDividers[timer] -= TimerPeriods[timer];

                    _timerStages[timer]++;

                    int target = _timerTargets[timer];

                    if (target == 0)
                        target = 256;

                    if (_timerStages[timer] >= target)
                    {
                        _timerStages[timer] = 0;
                        _timerCounters[timer] = (byte)((_timerCounters[timer] + 1) & 0x0F);
                    }
                }
            }
        }

        public void DumpMemory(ushort start, int length)
        {
            for (int i = 0; i < length; i += 16)
            {
                string bytes = string.Join(" ", _ram.Skip(start + i).Take(Math.Min(16, length - i)).Select(b => b.ToString("X2")));
            }
        }
    }
}
