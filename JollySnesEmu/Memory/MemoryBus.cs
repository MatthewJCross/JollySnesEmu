
using JollySnesEmu.Audio;
using JollySnesEmu.Cartridge;
using JollySnesEmu.Video;
using System.Diagnostics;

namespace JollySnesEmu.Memory
{
    /// <summary>SNES 24-bit address bus.</summary>
    public sealed class MemoryBus
    {
        private readonly SnesPpu _ppu;
        private readonly byte[] _wram = new byte[128 * 1024];
        private readonly byte[] _ioRegisters = new byte[0x10000];
        private readonly SnesApu _apu;
        // Adapted bus behaviour from SuperNintendoEmulator's SNESSystem:
        // 24-bit address decoding, open bus, WRAM data port and B-bus mirroring.
        // HDMA state follows SuperNintendoEmulator SNESSystem.InitHdma/HandleHdma.
        private readonly ushort[] _hdmaTableAddress = new ushort[8];
        private readonly ushort[] _hdmaIndirectAddress = new ushort[8];
        private readonly byte[] _hdmaLineCounter = new byte[8];
        private readonly bool[] _hdmaTransfer = new bool[8];
        private readonly bool[] _hdmaTerminated = new bool[8];
        private bool _hdmaFrameInitialized;
        private byte _openBus;
        private int _wramPortAddress;
        public byte OpenBus => _openBus;

        private bool _nmiEnabled;
        private bool _vBlankFlag;
        private bool _nmiPending;
        private bool _nmiOccurred;

        public int DmaTransferCount { get; private set; }
        public int PendingDmaCycles { get; private set; }

        private SnesCartridge? _cartridge;

        private int _ppuWriteLogCount;

        public SnesApu Apu => _apu;
        public bool NmiPending => _nmiPending;
        public int NmiCount => _nmiCount;
        public bool NmiEnabled => _nmiEnabled;

        public MemoryBus(SnesPpu ppu, SnesApu apu)
        {
            _ppu = ppu ?? throw new ArgumentNullException(nameof(ppu));
            _apu = apu ?? throw new ArgumentNullException(nameof(apu));

            _ppu.VBlankStarted += OnVBlankStarted;
            _ppu.FrameStarted += OnFrameStarted;
            _ppu.HdmaScanline += OnHdmaScanline;
        }

        private int _nmiCount;
        private void OnVBlankStarted()
        {
            _vBlankFlag = true;
            _nmiOccurred = true;

            if (_nmiEnabled)
            {
                _nmiPending = true;
                _nmiCount++;
            }
        }

        public int ConsumeDmaCycles()
        {
            int cycles = PendingDmaCycles;
            PendingDmaCycles = 0;
            return cycles;
        }

        private void OnHdmaScanline(int scanline)
        {
            if (_hdmaFrameInitialized)
                ExecuteHdmaScanline();
        }

        private readonly Stopwatch _frameTimer = Stopwatch.StartNew();
        private int _frameCount;
        private void OnFrameStarted()
        {
            _vBlankFlag = false;
            InitializeHdmaFrame();

            _frameCount++;

            if (_frameCount % 60 == 0)
            {


                _frameTimer.Restart();
            }
        }

        public byte ReadByte(uint address)
        {
            if (address > 0xFFFFFF)
                throw new ArgumentOutOfRangeException(nameof(address));

            if (TryGetWramAddress(address, out int wramAddress))
                return _wram[wramAddress];

            if (TryGetWramPort(address, out ushort wramPort))
                return ReadWramPort(wramPort);

            if (TryGetPpuRegister(address, out ushort register))
                return Latch(_ppu.ReadRegister(register));

            if (TryGetIoRegister(address, out register))
            {
                if (register == 0x4210)
                {
                    byte value = (byte)((_nmiOccurred ? 0x80 : 0x00) | 0x01);
                    _nmiOccurred = false;
                    return value;
                }

                if (register == 0x4212)
                {
                    byte value = 0;

                    if (_vBlankFlag)
                        value |= 0x80;

                    if (_ppu.IsHBlank)
                        value |= 0x40;

                    return value;
                }

                return _ioRegisters[register];
            }

            if (TryGetApuPort(address, out int port))
                return Latch(_apu.ReadCpuPort(port));

            if (TryGetLoRomAddress(address, out int romOffset))
            {
                if (_cartridge == null)
                    throw new InvalidOperationException("No cartridge has been inserted.");

                return Latch(_cartridge.ReadByte(romOffset));
            }

            return _openBus;
        }

        public void WriteByte(uint address, byte value)
        {
            if (address > 0xFFFFFF)
                throw new ArgumentOutOfRangeException(nameof(address));

            if (address == 0x2100 ||
                address == 0x2105 ||
                address == 0x2107 ||
                address == 0x2108 ||
                address == 0x2109 ||
                address == 0x210A ||
                address == 0x210B ||
                address == 0x210C ||
                address == 0x212C ||
                address == 0x212D)
            {
                ;
            }

            if (address >= 0x004300 && address <= 0x00437F)
            {
                ;
            }

            if (TryGetWramAddress(address, out int wramAddress))
            {
                if (wramAddress == 0x0010)
                {
                    ;

                    ;
                }
                _wram[wramAddress] = value;
                return;
            }

            if (TryGetWramPort(address, out ushort wramPort))
            {
                WriteWramPort(wramPort, value);
                return;
            }

            if (TryGetPpuRegister(address, out ushort register))
            {
                _ppu.WriteRegister(register, value);
                return;
            }

            if (TryGetIoRegister(address, out register))
            {
                _ioRegisters[register] = value;

                switch (register)
                {
                    case 0x4200:
                        {
                            bool wasEnabled = _nmiEnabled;
                            _nmiEnabled = (value & 0x80) != 0;

                            if (!wasEnabled && _nmiEnabled && _vBlankFlag)
                                _nmiPending = true;

                            break;
                        }

                    case 0x4203:
                        ExecuteMultiplication();
                        break;

                    case 0x4206:
                        ExecuteDivision();
                        break;

                    case 0x420B:
                        ExecuteDma(value);
                        break;

                    case 0x420C:
                        // HDMA enable mask; latched at frame initialization.
                        break;
                }

                return;
            }

            if (TryGetApuPort(address, out int port))
            {
                _apu.WriteCpuPort(port, value);
                return;
            }

            // Writes to unmapped/open-bus locations are ignored.
        }

        private byte Latch(byte value)
        {
            _openBus = value;
            return value;
        }

        private static bool TryGetWramPort(uint address, out ushort register)
        {
            int bank = (int)(address >> 16);
            register = (ushort)address;
            return (bank <= 0x3F || bank >= 0x80 && bank <= 0xBF)
                && register >= 0x2180 && register <= 0x2183;
        }

        private byte ReadWramPort(ushort register)
        {
            if (register != 0x2180)
                return _openBus;

            byte value = _wram[_wramPortAddress];
            _wramPortAddress = (_wramPortAddress + 1) & 0x1FFFF;
            return Latch(value);
        }

        private void WriteWramPort(ushort register, byte value)
        {
            switch (register)
            {
                case 0x2180:
                    _wram[_wramPortAddress] = value;
                    _wramPortAddress = (_wramPortAddress + 1) & 0x1FFFF;
                    break;
                case 0x2181:
                    _wramPortAddress = (_wramPortAddress & 0x1FF00) | value;
                    break;
                case 0x2182:
                    _wramPortAddress = (_wramPortAddress & 0x100FF) | (value << 8);
                    break;
                case 0x2183:
                    _wramPortAddress = (_wramPortAddress & 0x0FFFF) | ((value & 1) << 16);
                    break;
            }
        }

        // Call once at the beginning of the visible frame, before the first HDMA line.
        // Adapted from SuperNintendoEmulator SNESSystem.InitHdma().
        public void InitializeHdmaFrame()
        {
            _hdmaFrameInitialized = true;
            byte enabled = _ioRegisters[0x420C];

            for (int channel = 0; channel < 8; channel++)
            {
                int r = 0x4300 + channel * 0x10;
                _hdmaTerminated[channel] = (enabled & (1 << channel)) == 0;
                _hdmaTransfer[channel] = !_hdmaTerminated[channel];
                if (_hdmaTerminated[channel])
                    continue;

                ushort table = (ushort)(_ioRegisters[r + 2] | (_ioRegisters[r + 3] << 8));
                _hdmaTableAddress[channel] = table;
                LoadHdmaLineDescriptor(channel);
            }
        }

        // Call once per visible scanline at the HDMA transfer point.
        // Adapted from SuperNintendoEmulator SNESSystem.HandleHdma().
        public void ExecuteHdmaScanline()
        {
            if (!_hdmaFrameInitialized)
                throw new InvalidOperationException("InitializeHdmaFrame must run before ExecuteHdmaScanline.");

            for (int channel = 0; channel < 8; channel++)
            {
                if (_hdmaTerminated[channel])
                    continue;

                int r = 0x4300 + channel * 0x10;
                byte control = _ioRegisters[r];
                int mode = control & 7;
                int[] offsets = mode switch
                {
                    0 => [0],
                    1 => [0, 1],
                    2 => [0, 0],
                    3 => [0, 0, 1, 1],
                    4 => [0, 1, 2, 3],
                    5 => [0, 1, 0, 1],
                    6 => [0, 0],
                    _ => [0, 0, 1, 1]
                };

                if (_hdmaTransfer[channel])
                {
                    bool indirect = (control & 0x40) != 0;
                    bool bToA = (control & 0x80) != 0;
                    byte bank = indirect ? _ioRegisters[r + 7] : _ioRegisters[r + 4];

                    foreach (int offset in offsets)
                    {
                        ushort address = indirect ? _hdmaIndirectAddress[channel] : _hdmaTableAddress[channel];
                        uint aBus = ((uint)bank << 16) | address;
                        uint bBus = 0x2100u + (byte)(_ioRegisters[r + 1] + offset);

                        if (bToA)
                            WriteByte(aBus, ReadByte(bBus));
                        else
                            WriteByte(bBus, ReadByte(aBus));

                        if (indirect)
                            _hdmaIndirectAddress[channel]++;
                        else
                            _hdmaTableAddress[channel]++;
                    }
                }

                _hdmaLineCounter[channel]--;
                _hdmaTransfer[channel] = (_hdmaLineCounter[channel] & 0x80) != 0;
                if ((_hdmaLineCounter[channel] & 0x7F) == 0)
                    LoadHdmaLineDescriptor(channel);

                _ioRegisters[r + 8] = (byte)_hdmaTableAddress[channel];
                _ioRegisters[r + 9] = (byte)(_hdmaTableAddress[channel] >> 8);
                _ioRegisters[r + 10] = _hdmaLineCounter[channel];
            }
        }

        private void LoadHdmaLineDescriptor(int channel)
        {
            int r = 0x4300 + channel * 0x10;
            uint bank = (uint)_ioRegisters[r + 4] << 16;
            byte count = ReadByte(bank | _hdmaTableAddress[channel]++);
            _hdmaLineCounter[channel] = count;
            if (count == 0)
            {
                _hdmaTerminated[channel] = true;
                _hdmaTransfer[channel] = false;
                return;
            }

            if ((_ioRegisters[r] & 0x40) != 0)
            {
                byte low = ReadByte(bank | _hdmaTableAddress[channel]++);
                byte high = ReadByte(bank | _hdmaTableAddress[channel]++);
                _hdmaIndirectAddress[channel] = (ushort)(low | (high << 8));
            }

            _hdmaTransfer[channel] = true;
        }

        private void ExecuteDma(byte channelMask)
        {
            for (int channel = 0; channel < 8; channel++)
            {
                if ((channelMask & (1 << channel)) == 0)
                    continue;

                int registers = 0x4300 + (channel * 0x10);

                byte control = _ioRegisters[registers];
                byte bBusAddress = _ioRegisters[registers + 1];
                ushort aBusAddress = (ushort)(_ioRegisters[registers + 2] | (_ioRegisters[registers + 3] << 8));
                byte aBusBank = _ioRegisters[registers + 4];
                ushort transferSize = (ushort)(_ioRegisters[registers + 5] | (_ioRegisters[registers + 6] << 8));

                int remaining = transferSize == 0 ? 0x10000 : transferSize;
                int bytesTransferred = remaining;
                // Approximate DMA timing in the current CPU-cycle model.
                // SNES DMA uses 8 master cycles per byte, plus setup overhead.
                int masterCycles = 8 + (bytesTransferred * 8);
                PendingDmaCycles += (masterCycles + 5) / 6;
                bool bToA = (control & 0x80) != 0;
                bool fixedAddress = (control & 0x08) != 0;
                bool decrement = (control & 0x10) != 0;
                int mode = control & 0x07;

                int[] pattern = mode switch
                {
                    0 => [0],
                    1 => [0, 1],
                    2 => [0, 0],
                    3 => [0, 0, 1, 1],
                    4 => [0, 1, 2, 3],
                    5 => [0, 1, 0, 1],
                    6 => [0, 0],
                    _ => [0, 0, 1, 1]
                };

                int patternIndex = 0;

                while (remaining > 0)
                {
                    byte currentBBusAddress = (byte)(bBusAddress + pattern[patternIndex]);
                    uint aAddress = ((uint)aBusBank << 16) | aBusAddress;
                    uint bAddress = 0x002100u + currentBBusAddress;

                    if (bToA)
                    {
                        byte value = ReadByte(bAddress);
                        WriteByte(aAddress, value);
                    }
                    else
                    {
                        byte value = ReadByte(aAddress);
                        WriteByte(bAddress, value);
                    }

                    if (!fixedAddress)
                        aBusAddress = (ushort)(aBusAddress + (decrement ? -1 : 1));

                    remaining--;
                    patternIndex++;
                    if (patternIndex >= pattern.Length)
                        patternIndex = 0;
                }

                _ioRegisters[registers + 2] = (byte)(aBusAddress & 0xFF);
                _ioRegisters[registers + 3] = (byte)(aBusAddress >> 8);
                _ioRegisters[registers + 5] = 0;
                _ioRegisters[registers + 6] = 0;

                ;
            }

            // The hardware clears the enabled channel bits when the transfer completes.
            _ioRegisters[0x420B] = 0;

            DmaTransferCount++;

        }

        private static bool TryGetWramAddress(uint address, out int wramAddress)
        {
            wramAddress = 0;

            int bank = (int)(address >> 16);
            int offset = (int)(address & 0xFFFF);

            // Banks $7E and $7F: full 128 KB WRAM.
            if (bank == 0x7E || bank == 0x7F)
            {
                wramAddress = ((bank - 0x7E) * 0x10000) + offset;
                return true;
            }

            // Banks $00-$3F and $80-$BF: first 8 KB WRAM mirror.
            bool validBank = bank <= 0x3F || (bank >= 0x80 && bank <= 0xBF);

            if (validBank && offset <= 0x1FFF)
            {
                wramAddress = offset;
                return true;
            }

            return false;
        }

        private static bool TryGetPpuRegister(uint address, out ushort register)
        {
            register = 0;

            int bank = (int)(address >> 16);
            ushort offset = (ushort)(address & 0xFFFF);

            bool validBank = bank <= 0x3F || (bank >= 0x80 && bank <= 0xBF);

            if (!validBank || offset < 0x2100 || offset > 0x213F)
                return false;

            register = offset;
            return true;
        }

        private static bool TryGetLoRomAddress(uint address, out int romOffset)
        {
            romOffset = 0;

            int bank = (int)(address >> 16);
            int offset = (int)(address & 0xFFFF);

            if (offset < 0x8000)
                return false;

            if (bank == 0x7E || bank == 0x7F)
                return false;

            if (bank > 0x7D && bank < 0x80)
                return false;

            romOffset = ((bank & 0x7F) * 0x8000) + (offset & 0x7FFF);
            return true;
        }

        private static bool TryGetIoRegister(uint address, out ushort register)
        {
            register = 0;

            int bank = (int)(address >> 16);
            ushort offset = (ushort)(address & 0xFFFF);

            bool validBank = bank <= 0x3F || (bank >= 0x80 && bank <= 0xBF);

            if (!validBank || offset < 0x4200 || offset > 0x437F)
                return false;

            register = offset;
            return true;
        }

        private static bool TryGetApuPort(uint address, out int port)
        {
            port = 0;

            int bank = (int)(address >> 16);
            ushort offset = (ushort)(address & 0xFFFF);

            bool validBank = bank <= 0x3F || (bank >= 0x80 && bank <= 0xBF);

            if (!validBank || offset < 0x2140 || offset > 0x217F)
                return false;

            port = (offset - 0x2140) & 3;
            return true;
        }

        public void InsertCartridge(SnesCartridge cartridge)
        {
            _cartridge = cartridge ?? throw new ArgumentNullException(nameof(cartridge));

            if (_cartridge.Info.Mapping != CartridgeMapping.LoRom)
            {
                throw new NotSupportedException($"Cartridge mapping {_cartridge.Info.Mapping} is not supported yet.");
            }
        }

        public void SetVBlank(bool active)
        {
            if (active)
            {
                if (!_vBlankFlag)
                {
                    _vBlankFlag = true;

                    if (_nmiEnabled)
                        _nmiPending = true;
                }
            }
            else
            {
                _vBlankFlag = false;
            }
        }

        public void ClearNmiPending()
        {
            _nmiPending = false;
        }

        private void ExecuteMultiplication()
        {
            int operandA = _ioRegisters[0x4202];
            int operandB = _ioRegisters[0x4203];

            ushort result = (ushort)(operandA * operandB);

            _ioRegisters[0x4216] = (byte)result;
            _ioRegisters[0x4217] = (byte)(result >> 8);
        }

        private void ExecuteDivision()
        {
            ushort dividend = (ushort)(_ioRegisters[0x4204] | (_ioRegisters[0x4205] << 8));

            byte divisor = _ioRegisters[0x4206];

            ushort quotient;
            ushort remainder;

            if (divisor == 0)
            {
                quotient = 0xFFFF;
                remainder = dividend;
            }
            else
            {
                quotient = (ushort)(dividend / divisor);
                remainder = (ushort)(dividend % divisor);
            }

            _ioRegisters[0x4214] = (byte)quotient;
            _ioRegisters[0x4215] = (byte)(quotient >> 8);

            _ioRegisters[0x4216] = (byte)remainder;
            _ioRegisters[0x4217] = (byte)(remainder >> 8);
        }
    }
}
