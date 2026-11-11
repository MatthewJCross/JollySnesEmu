
using System.Diagnostics;

namespace JollySnesEmu.Video
{
    /// <summary>
    /// First-pass SNES PPU implementation.
    /// Supports first-pass Mode 0 BG1 and Mode 1 BG1/BG2 background rendering.
    /// Uses 8x8 tiles, SNES planar 2bpp/4bpp graphics, tile maps, scrolling and CGRAM.
    /// Includes first-pass OBJ rendering. Windows, colour math and full PPU timing come later.
    /// </summary>
    public sealed class SnesPpu
    {
        public const int VramSize = 0x10000;
        public const int CgramSize = 0x0200;
        public const int OamSize = 0x0220;

        private const int ScreenWidth = 256;
        private const int ScreenHeight = 224;

        private const int MasterCyclesPerScanline = 1364;
        private const int HdmaMasterCycle = 1024;
        private const int ScanlinesPerFrame = 262;
        private const int VBlankStartScanline = 225;
        private const int MasterCyclesPerCpuCycle = 6;

        private int _scanline;
        private int _masterCyclesIntoScanline;

        public event Action? VBlankStarted;
        public event Action? FrameStarted;
        public event Action<int>? HdmaScanline;


        private readonly SnesFrameBuffer _framebuffer;
        private readonly byte[] _vram = new byte[VramSize];
        private readonly byte[] _cgram = new byte[CgramSize];
        private readonly byte[] _oam = new byte[OamSize];

        private byte _displayControl = 0x80;
        private byte _bgMode;
        private byte _bg1ScreenBase;
        private byte _bg2ScreenBase;
        private byte _bg3ScreenBase;
        private byte _bg12TileDataBase;
        private byte _bg34TileDataBase;
        private byte _mainScreenDesignation;
        private byte _subScreenDesignation;
        private byte _colourMathControl;
        private byte _colourMathDesignation;
        private int _fixedRed, _fixedGreen, _fixedBlue;
        private readonly byte[] _mainLayer = new byte[ScreenWidth * ScreenHeight];
        private readonly byte[] _subLayer = new byte[ScreenWidth * ScreenHeight];
        private byte _objSelection;
        private bool _oamPriorityRotation;

        private ushort _bg1HScroll;
        private ushort _bg1VScroll;
        private ushort _bg2HScroll;
        private ushort _bg2VScroll;
        private ushort _bg3HScroll;
        private ushort _bg3VScroll;

        private bool _scrollWriteSecond;
        private byte _scrollWriteLatch;

        private ushort _vramAddress;
        private byte _vramIncrementMode;
        private byte _vramRemapMode;
        private bool _vramIncrementOnHigh;

        private ushort _cgramAddress;
        private bool _cgramSecondWrite;
        private byte _cgramWriteLatch;

        private ushort _oamAddress;
        private bool _oamSecondWrite;
        private byte _oamWriteLatch;

        public int CurrentScanline => _scanline;
        public bool IsHBlank => _masterCyclesIntoScanline >= 1096;

        public SnesPpu(SnesFrameBuffer framebuffer)
        {
            _framebuffer = framebuffer ?? throw new ArgumentNullException(nameof(framebuffer));

            if (SnesFrameBuffer.Width != SnesFrameBuffer.DefaultWidth ||
                SnesFrameBuffer.Height < SnesFrameBuffer.DefaultHeight)
            {
                throw new ArgumentException("The SNES PPU requires a framebuffer at least 256x224 pixels.", nameof(framebuffer));
            }

            Reset();
        }

        public ReadOnlyMemory<byte> Vram => _vram;
        public ReadOnlyMemory<byte> Cgram => _cgram;
        public ReadOnlyMemory<byte> Oam => _oam;

        public void Reset()
        {
            Array.Clear(_vram);
            Array.Clear(_cgram);
            Array.Clear(_oam);

            _displayControl = 0x80;
            _bgMode = 0;
            _bg1ScreenBase = 0;
            _bg2ScreenBase = 0;
            _bg3ScreenBase = 0;
            _bg12TileDataBase = 0;
            _bg34TileDataBase = 0;
            _mainScreenDesignation = 0;
            _subScreenDesignation = 0;
            _colourMathControl = 0;
            _colourMathDesignation = 0;
            _fixedRed = _fixedGreen = _fixedBlue = 0;
            _objSelection = 0;
            _oamPriorityRotation = false;

            _vramAddress = 0;
            _vramIncrementMode = 1;
            _vramRemapMode = 0;
            _vramIncrementOnHigh = false;

            _cgramAddress = 0;
            _cgramSecondWrite = false;
            _cgramWriteLatch = 0;

            _oamAddress = 0;
            _oamSecondWrite = false;
            _oamWriteLatch = 0;

            _bg1HScroll = 0;
            _bg1VScroll = 0;
            _bg2HScroll = 0;
            _bg2VScroll = 0;

            _scrollWriteSecond = false;
            _scrollWriteLatch = 0;

            _scanline = 0;
            _masterCyclesIntoScanline = 0;

            _framebuffer.Clear();
        }

        /// <summary>Writes a value to a PPU register in the $2100-$213F range.</summary>
        public void WriteRegister(ushort address, byte value)
        {
            switch (address)
            {
                case 0x2100:
                    _displayControl = value;
                    break;

                case 0x2101:
                    _objSelection = value;
                    break;

                case 0x2105:
                    _bgMode = value;
                    break;

                case 0x2107:
                    _bg1ScreenBase = value;
                    break;

                case 0x2108:
                    _bg2ScreenBase = value;
                    break;

                case 0x2109:
                    _bg3ScreenBase = value;
                    break;

                case 0x210B:
                    _bg12TileDataBase = value;
                    break;

                case 0x210C:
                    _bg34TileDataBase = value;
                    break;

                case 0x212C:
                    _mainScreenDesignation = value;
                    Debug.WriteLine($"PPU LAYERS: ${value:X2}");
                    break;

                case 0x2115:
                    _vramIncrementOnHigh = (value & 0x80) != 0;
                    _vramRemapMode = (byte)((value >> 2) & 0x03);
                    _vramIncrementMode = (byte)(value & 0x03);
                    break;

                case 0x2116:
                    _vramAddress = (ushort)((_vramAddress & 0xFF00) | value);
                    break;

                case 0x2117:
                    _vramAddress = (ushort)((_vramAddress & 0x00FF) | (value << 8));
                    break;

                case 0x2118:
                    WriteVramLow(value);
                    break;

                case 0x2119:
                    WriteVramHigh(value);
                    break;

                case 0x2121:
                    _cgramAddress = (ushort)((value << 1) & 0x01FF);
                    _cgramSecondWrite = false;
                    break;

                case 0x2122:
                    WriteCgram(value);
                    break;

                case 0x2102:
                    _oamAddress = (ushort)((_oamAddress & 0x0200) | (value << 1));
                    _oamSecondWrite = false;
                    break;

                case 0x2103:
                    _oamAddress = (ushort)((_oamAddress & 0x01FE) | ((value & 0x01) << 9));
                    _oamPriorityRotation = (value & 0x80) != 0;
                    _oamSecondWrite = false;
                    break;

                case 0x2104:
                    WriteOam(value);
                    break;

                case 0x210D:
                    WriteScroll(ref _bg1HScroll, value);
                    break;

                case 0x210E:
                    WriteScroll(ref _bg1VScroll, value);
                    break;

                case 0x210F:
                    WriteScroll(ref _bg2HScroll, value);
                    break;

                case 0x2110:
                    WriteScroll(ref _bg2VScroll, value);
                    break;

                case 0x2111:
                    WriteScroll(ref _bg3HScroll, value);
                    break;

                case 0x2112:
                    WriteScroll(ref _bg3VScroll, value);
                    break;

                case 0x212D:
                    _subScreenDesignation = value;
                    break;

                case 0x2130:
                    _colourMathControl = value;
                    break;

                case 0x2131:
                    _colourMathDesignation = value;
                    break;

                case 0x2132:
                    if ((value & 0x20) != 0) _fixedRed = value & 31;
                    if ((value & 0x40) != 0) _fixedGreen = value & 31;
                    if ((value & 0x80) != 0) _fixedBlue = value & 31;
                    break;
            }
        }

        /// <summary>
        /// Reads a PPU register. Only the write-only register readback
        /// defaults are modelled for now.
        /// </summary>
        public byte ReadRegister(ushort address)
        {
            return address switch
            {
                0x2139 => ReadVramLow(),
                0x213A => ReadVramHigh(),
                0x213B => ReadCgram(),
                _ => 0x00
            };
        }

        /// <summary>Render one completed visible scanline using the current PPU state.</summary>
        public void RenderScanline(int scanline)
        {
            if ((uint)scanline >= ScreenHeight)
                return;

            int width = SnesFrameBuffer.Width;
            int height = Math.Min(SnesFrameBuffer.Height, ScreenHeight);
            int start = scanline * width * 4;
            int length = width * 4;
            Span<byte> main = _framebuffer.PixelData;
            Span<byte> sub = stackalloc byte[ScreenWidth * 4];
            ushort backdrop = ReadCgramColour(0);
            uint backdropBgra = ToBgra(backdrop);
            for (int x = 0; x < width; x++)
            {
                int index = start + x * 4;
                main[index] = (byte)backdropBgra;
                main[index + 1] = (byte)(backdropBgra >> 8);
                main[index + 2] = (byte)(backdropBgra >> 16);
                main[index + 3] = 255;
                int subIndex = x * 4;
                sub[subIndex] = main[index];
                sub[subIndex + 1] = main[index + 1];
                sub[subIndex + 2] = main[index + 2];
                sub[subIndex + 3] = 255;
                _mainLayer[scanline * width + x] = 5;
                _subLayer[scanline * width + x] = 5;
            }

            if ((_displayControl & 0x80) != 0 || (_displayControl & 15) == 0)
            {
                main.Slice(start, length).Clear();
                return;
            }

            // The subscreen uses the same tile/sprite priority rules as the main screen.
            RenderLayers(main, width, height, _mainScreenDesignation, scanline, _mainLayer);
            RenderLayers(sub, width, height, _subScreenDesignation, scanline, _subLayer, true);

            bool useSubscreen = (_colourMathControl & 0x02) != 0;
            bool subtract = (_colourMathDesignation & 0x80) != 0;
            bool half = (_colourMathDesignation & 0x40) != 0;
            int brightness = _displayControl & 15;

            for (int x = 0; x < width; x++)
            {
                int index = start + x * 4;
                int si = x * 4;
                int pixel = scanline * width + x;
                int layer = _mainLayer[pixel];
                bool mathEnabled = (_colourMathDesignation & (1 << layer)) != 0;
                int r = main[index + 2] >> 3;
                int g = main[index + 1] >> 3;
                int b = main[index] >> 3;
                if (mathEnabled)
                {
                    bool hasSubPixel = useSubscreen && _subLayer[pixel] != 5;
                    int r2 = hasSubPixel ? sub[si + 2] >> 3 : _fixedRed;
                    int g2 = hasSubPixel ? sub[si + 1] >> 3 : _fixedGreen;
                    int b2 = hasSubPixel ? sub[si] >> 3 : _fixedBlue;
                    r = Math.Clamp(subtract ? r - r2 : r + r2, 0, 31);
                    g = Math.Clamp(subtract ? g - g2 : g + g2, 0, 31);
                    b = Math.Clamp(subtract ? b - b2 : b + b2, 0, 31);
                    if (half && (!useSubscreen || hasSubPixel))
                    {
                        r >>= 1;
                        g >>= 1;
                        b >>= 1;
                    }
                }
                main[index] = (byte)((b * 255 * brightness) / (31 * 15));
                main[index + 1] = (byte)((g * 255 * brightness) / (31 * 15));
                main[index + 2] = (byte)((r * 255 * brightness) / (31 * 15));
                main[index + 3] = 255;
            }
        }

        // Kept for callers that explicitly need to redraw a complete frame.
        public void RenderFrame()
        {
            for (int y = 0; y < ScreenHeight; y++)
                RenderScanline(y);
        }

        private void RenderLayers(Span<byte> pixels, int width, int height, byte designation, int scanline, byte[] layerIds, bool sub = false)
        {
            int mode = _bgMode & 7;
            int bg1ScreenBase = (_bg1ScreenBase & 0xFC) << 9;
            int bg2ScreenBase = (_bg2ScreenBase & 0xFC) << 9;
            int bg3ScreenBase = (_bg3ScreenBase & 0xFC) << 9;
            int bg1TileBase = (_bg12TileDataBase & 15) << 13;
            int bg2TileBase = ((_bg12TileDataBase >> 4) & 15) << 13;
            int bg3TileBase = (_bg34TileDataBase & 15) << 13;
            byte[] priorities = new byte[width * height];
            bool bg3High = (_bgMode & 8) != 0;
            int offset = sub ? scanline * width * 4 : 0;

            if (mode == 0 || mode == 1)
            {
                if ((designation & 4) != 0 && mode == 1)
                {
                    RenderBackground(pixels, width, height, _bg3ScreenBase, bg3ScreenBase, bg3TileBase, _bg3HScroll, _bg3VScroll, 2, false, priorities, 1, (_bgMode & 0x40) != 0, scanline, layerIds, 2, offset);
                    RenderBackground(pixels, width, height, _bg3ScreenBase, bg3ScreenBase, bg3TileBase, _bg3HScroll, _bg3VScroll, 2, true, priorities, (byte)(bg3High ? 10 : 3), (_bgMode & 0x40) != 0, scanline, layerIds, 2, offset);
                }
                if ((designation & 2) != 0 && mode == 1)
                {
                    RenderBackground(pixels, width, height, _bg2ScreenBase, bg2ScreenBase, bg2TileBase, _bg2HScroll, _bg2VScroll, 4, false, priorities, 5, (_bgMode & 0x20) != 0, scanline, layerIds, 1, offset);
                    RenderBackground(pixels, width, height, _bg2ScreenBase, bg2ScreenBase, bg2TileBase, _bg2HScroll, _bg2VScroll, 4, true, priorities, 8, (_bgMode & 0x20) != 0, scanline, layerIds, 1, offset);
                }
                if ((designation & 1) != 0)
                {
                    int bpp = mode == 0 ? 2 : 4;
                    RenderBackground(pixels, width, height, _bg1ScreenBase, bg1ScreenBase, bg1TileBase, _bg1HScroll, _bg1VScroll, bpp, false, priorities, 6, (_bgMode & 0x10) != 0, scanline, layerIds, 0, offset);
                    RenderBackground(pixels, width, height, _bg1ScreenBase, bg1ScreenBase, bg1TileBase, _bg1HScroll, _bg1VScroll, bpp, true, priorities, 9, (_bgMode & 0x10) != 0, scanline, layerIds, 0, offset);
                }
            }
            if ((designation & 0x10) != 0)
                RenderSprites(pixels, width, height, priorities, scanline, layerIds, offset);
        }

        private void RenderBackground(Span<byte> pixels, int width, int height, byte screenSizeRegister, int screenBase, int tileBase, ushort horizontalScroll, ushort verticalScroll, int bitsPerPixel, bool highPriority, byte[]? priorities = null, byte layerPriority = 0, bool largeTiles = false, int onlyScanline = -1, byte[]? layerIds = null, byte layerId = 0, int pixelOffset = 0)
        {
            int tileMapWidth = (screenSizeRegister & 0x01) != 0 ? 64 : 32;
            int tileMapHeight = (screenSizeRegister & 0x02) != 0 ? 64 : 32;

            int tileSize = largeTiles ? 16 : 8;
            int mapPixelWidth = tileMapWidth * tileSize;
            int mapPixelHeight = tileMapHeight * tileSize;

            int scrollX = horizontalScroll % mapPixelWidth;
            int scrollY = verticalScroll % mapPixelHeight;

            int bytesPerTile = bitsPerPixel == 4 ? 32 : 16;

            for (int y = Math.Max(0, onlyScanline); y < (onlyScanline >= 0 ? onlyScanline + 1 : height); y++)
            {
                int sourceY = (y + scrollY) % mapPixelHeight;
                int tileY = sourceY / tileSize;
                int pixelY = sourceY % tileSize;

                for (int x = 0; x < width; x++)
                {
                    int sourceX = (x + scrollX) % mapPixelWidth;
                    int tileX = sourceX / tileSize;
                    int pixelX = sourceX % tileSize;

                    int mapOffset = GetTileMapAddress(screenBase, tileX, tileY, tileMapWidth);
                    ushort entry = ReadVramWord(mapOffset);

                    bool tileHighPriority = (entry & 0x2000) != 0;

                    if (tileHighPriority != highPriority)
                        continue;

                    int tileNumber = entry & 0x03FF;
                    int palette = (entry >> 10) & 0x07;

                    bool horizontalFlip = (entry & 0x4000) != 0;
                    bool verticalFlip = (entry & 0x8000) != 0;

                    int sourcePixelX = horizontalFlip ? tileSize - 1 - pixelX : pixelX;
                    int sourcePixelY = verticalFlip ? tileSize - 1 - pixelY : pixelY;

                    if (largeTiles)
                    {
                        tileNumber = (tileNumber + (sourcePixelX / 8) + ((sourcePixelY / 8) * 16)) & 0x03FF;
                    }

                    sourcePixelX &= 7;
                    sourcePixelY &= 7;

                    int tileAddress = tileBase + (tileNumber * bytesPerTile);

                    int planeAddress = tileAddress + (sourcePixelY * 2);

                    int bit = 7 - sourcePixelX;

                    byte plane0 = _vram[planeAddress & 0xFFFF];
                    byte plane1 = _vram[(planeAddress + 1) & 0xFFFF];

                    int colourIndex =
                        ((plane0 >> bit) & 1) |
                        (((plane1 >> bit) & 1) << 1);

                    if (bitsPerPixel == 4)
                    {
                        byte plane2 = _vram[(planeAddress + 16) & 0xFFFF];
                        byte plane3 = _vram[(planeAddress + 17) & 0xFFFF];
                        colourIndex |= (((plane2 >> bit) & 1) << 2) | (((plane3 >> bit) & 1) << 3);
                    }

                    if (colourIndex == 0)
                        continue;

                    int coloursPerPalette = 1 << bitsPerPixel;
                    int colour = (palette * coloursPerPalette) + colourIndex;

                    uint bgra = ToBgra(ReadCgramColour(colour));

                    int pixelIndex = ((y * width) + x) * 4 - pixelOffset;

                    int priorityIndex = y * width + x;
                    if (priorities != null && layerPriority < priorities[priorityIndex])
                        continue;

                    if (priorities != null)
                        priorities[priorityIndex] = layerPriority;
                    if (layerIds != null) layerIds[priorityIndex] = layerId;

                    pixels[pixelIndex] = (byte)(bgra & 0xFF);
                    pixels[pixelIndex + 1] = (byte)((bgra >> 8) & 0xFF);
                    pixels[pixelIndex + 2] = (byte)((bgra >> 16) & 0xFF);
                    pixels[pixelIndex + 3] = 255;
                }
            }
        }

        private void WriteVramLow(byte value)
        {
            int address = GetRemappedVramAddress(_vramAddress);

            _vram[(address * 2) & 0xFFFF] = value;

            if (!_vramIncrementOnHigh)
                IncrementVramAddress();
        }

        private void WriteVramHigh(byte value)
        {
            int address = GetRemappedVramAddress(_vramAddress);

            _vram[((address * 2) + 1) & 0xFFFF] = value;

            if (_vramIncrementOnHigh)
                IncrementVramAddress();
        }

        private byte ReadVramLow()
        {
            int address = GetRemappedVramAddress(_vramAddress);

            byte value = _vram[(address * 2) & 0xFFFF];

            if (!_vramIncrementOnHigh)
                IncrementVramAddress();

            return value;
        }

        private byte ReadVramHigh()
        {
            int address = GetRemappedVramAddress(_vramAddress);

            byte value = _vram[((address * 2) + 1) & 0xFFFF];

            if (_vramIncrementOnHigh)
                IncrementVramAddress();

            return value;
        }

        /// <summary>
        /// Applies the VRAM address remapping selected by bits 2-3 of VMAIN ($2115).
        /// </summary>
        private int GetRemappedVramAddress(int address)
        {
            return _vramRemapMode switch
            {
                0 => address,
                1 => (address & 0xFF00) | ((address & 0x001F) << 3) | ((address & 0x00E0) >> 5),
                2 => (address & 0xFE00) | ((address & 0x003F) << 3) | ((address & 0x01C0) >> 6),
                3 => (address & 0xFC00) | ((address & 0x007F) << 3) | ((address & 0x0380) >> 7),
                _ => address
            };
        }

        private void IncrementVramAddress()
        {
            int increment = _vramIncrementMode switch
            {
                0 => 1,
                1 => 32,
                2 => 128,
                _ => 128
            };

            _vramAddress = (ushort)(_vramAddress + increment);
        }

        private void WriteCgram(byte value)
        {
            if (!_cgramSecondWrite)
            {
                _cgramWriteLatch = value;
                _cgramSecondWrite = true;
                return;
            }

            int address = _cgramAddress & 0x01FE;

            _cgram[address] = _cgramWriteLatch;
            _cgram[address + 1] = (byte)(value & 0x7F);

            _cgramAddress = (ushort)((_cgramAddress + 2) & 0x01FF);
            _cgramSecondWrite = false;
        }

        private byte ReadCgram()
        {
            int address = _cgramAddress & 0x01FE;

            byte value = _cgramSecondWrite
                ? (byte)(_cgram[address + 1] & 0x7F)
                : _cgram[address];

            if (_cgramSecondWrite)
            {
                _cgramAddress = (ushort)((_cgramAddress + 2) & 0x01FF);
                _cgramSecondWrite = false;
            }
            else
            {
                _cgramSecondWrite = true;
            }

            return value;
        }

        private void WriteOam(byte value)
        {
            int address = GetOamAddress(_oamAddress);

            if (address >= 0x0200)
            {
                // The high OAM table is written one byte at a time.
                _oam[address] = value;
                _oamAddress = (ushort)((_oamAddress + 1) & 0x03FF);
                _oamSecondWrite = false;
                return;
            }

            // The low OAM table commits two bytes together.
            if (!_oamSecondWrite)
            {
                _oamWriteLatch = value;
                _oamSecondWrite = true;
                _oamAddress = (ushort)((_oamAddress + 1) & 0x03FF);
                return;
            }

            _oam[GetOamAddress(_oamAddress - 1)] = _oamWriteLatch;
            _oam[address] = value;
            _oamAddress = (ushort)((_oamAddress + 1) & 0x03FF);
            _oamSecondWrite = false;
        }

        private static int GetOamAddress(int address)
        {
            address &= 0x03FF;
            return address < 0x0200 ? address : 0x0200 + (address & 0x001F);
        }

        /// <summary>
        /// First-pass 4bpp OBJ rendering. Implements OAM coordinates, high-table
        /// size/X bits, flips, palettes, tile addressing and sprite ordering.
        /// Full per-scanline sprite limits and BG/OBJ priority mixing remain TODOs.
        /// </summary>
        private void RenderSprites(Span<byte> pixels, int width, int height, byte[]? priorities = null, int onlyScanline = -1, byte[]? layerIds = null, int pixelOffset = 0)
        {
            int sizeMode = (_objSelection >> 5) & 7;
            int[] smallSizes = [8, 8, 8, 16, 16, 32, 16, 16];
            int[] largeSizes = [16, 32, 64, 32, 64, 64, 32, 32];
            int baseAddress = (_objSelection & 0x07) << 14;
            int secondNameOffset = (((_objSelection >> 3) & 0x03) + 1) << 13;
            int firstSprite = _oamPriorityRotation ? ((_oamAddress >> 2) & 127) : 0;

            // Later OAM entries are behind earlier entries when sprites overlap.
            for (int order = 127; order >= 0; order--)
            {
                int sprite = (firstSprite + order) & 127;
                int oamIndex = sprite * 4;
                int highBits = (_oam[0x200 + sprite / 4] >> ((sprite & 3) * 2)) & 3;
                int x = _oam[oamIndex] | ((highBits & 1) << 8);
                int y = _oam[oamIndex + 1];
                int tile = _oam[oamIndex + 2];
                byte attributes = _oam[oamIndex + 3];
                int size = (highBits & 2) != 0 ? largeSizes[sizeMode] : smallSizes[sizeMode];
                bool flipX = (attributes & 0x40) != 0;
                bool flipY = (attributes & 0x80) != 0;
                int palette = (attributes >> 1) & 7;
                int objPriority = (attributes >> 4) & 3;
                byte layerPriority = (byte)(objPriority switch { 0 => 2, 1 => 4, 2 => 7, _ => 11 });
                int nameSelect = (attributes & 1) != 0 ? secondNameOffset : 0;

                if (x >= 256)
                    x -= 512;

                // SNES OBJ Y wraps at 256, so a sprite at Y=250 can extend onto the top.
                for (int row = 0; row < size; row++)
                {
                    int screenY = (y + row) & 0xFF;
                    if (screenY >= height || (onlyScanline >= 0 && screenY != onlyScanline))
                        continue;

                    int sourceY = flipY ? size - 1 - row : row;
                    int tileRow = sourceY / 8;
                    int pixelY = sourceY & 7;

                    for (int column = 0; column < size; column++)
                    {
                        int screenX = x + column;
                        if ((uint)screenX >= (uint)width)
                            continue;

                        int sourceX = flipX ? size - 1 - column : column;
                        int tileColumn = sourceX / 8;
                        int pixelX = sourceX & 7;
                        int tileNumber = (tile + tileColumn + tileRow * 16) & 0xFF;
                        int tileAddress = (baseAddress + nameSelect + tileNumber * 32) & 0xFFFF;
                        int planeAddress = (tileAddress + pixelY * 2) & 0xFFFF;
                        int bit = 7 - pixelX;
                        int colour =
                            ((_vram[planeAddress] >> bit) & 1) |
                            (((_vram[(planeAddress + 1) & 0xFFFF] >> bit) & 1) << 1) |
                            (((_vram[(planeAddress + 16) & 0xFFFF] >> bit) & 1) << 2) |
                            (((_vram[(planeAddress + 17) & 0xFFFF] >> bit) & 1) << 3);

                        if (colour == 0)
                            continue;

                        uint bgra = ToBgra(ReadCgramColour(128 + palette * 16 + colour));
                        int priorityIndex = screenY * width + screenX;
                        if (priorities != null && layerPriority < priorities[priorityIndex])
                            continue;

                        if (priorities != null)
                            priorities[priorityIndex] = layerPriority;
                        if (layerIds != null) layerIds[priorityIndex] = 4;

                        int index = priorityIndex * 4 - pixelOffset;
                        pixels[index] = (byte)bgra;
                        pixels[index + 1] = (byte)(bgra >> 8);
                        pixels[index + 2] = (byte)(bgra >> 16);
                        pixels[index + 3] = 255;
                    }
                }
            }
        }

        private ushort ReadVramWord(int byteAddress)
        {
            int address = byteAddress & 0xFFFF;

            byte low = _vram[address];
            byte high = _vram[(address + 1) & 0xFFFF];

            return (ushort)(low | (high << 8));
        }

        private ushort ReadCgramColour(int colourIndex)
        {
            int address = (colourIndex * 2) & 0x01FE;
            return (ushort)(_cgram[address] | ((_cgram[address + 1] & 0x7F) << 8));
        }

        private static uint ToBgra(ushort colour)
        {
            int red = colour & 0x1F;
            int green = (colour >> 5) & 0x1F;
            int blue = (colour >> 10) & 0x1F;

            red = (red << 3) | (red >> 2);
            green = (green << 3) | (green >> 2);
            blue = (blue << 3) | (blue >> 2);

            return 0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | (uint)blue;
        }

        private int GetTileMapAddress(int screenBase, int tileX, int tileY, int tileMapWidth)
        {
            int screenBlockX = tileX / 32;
            int screenBlockY = tileY / 32;

            int blocksPerRow = tileMapWidth / 32;
            int screenBlock = screenBlockX + (screenBlockY * blocksPerRow);

            int localX = tileX % 32;
            int localY = tileY % 32;

            return screenBase + (screenBlock * 0x800) + ((localY * 32 + localX) * 2);
        }

        private void WriteScroll(ref ushort scroll, byte value)
        {
            if (!_scrollWriteSecond)
            {
                _scrollWriteLatch = value;
                scroll = (ushort)((scroll & 0x0300) | value);
                _scrollWriteSecond = true;
            }
            else
            {
                scroll = (ushort)((_scrollWriteLatch | ((value & 0x03) << 8)) & 0x03FF);

                _scrollWriteSecond = false;
            }
        }

        // Timing boundaries follow SuperNintendoEmulator SNESSystem.Cycle():
        // HDMA at XPos=1024, frame reset at XPos=0, 1364 master cycles per line.
        // CPU-to-master conversion remains approximate until CPU bus timing is ported.
        public void AdvanceCpuCycles(int cpuCycles)
        {
            if (cpuCycles < 0)
                throw new ArgumentOutOfRangeException(nameof(cpuCycles));

            int remaining = checked(cpuCycles * MasterCyclesPerCpuCycle);

            while (remaining > 0)
            {
                int boundary = _masterCyclesIntoScanline < HdmaMasterCycle
                    ? HdmaMasterCycle
                    : MasterCyclesPerScanline;
                int advance = Math.Min(remaining, boundary - _masterCyclesIntoScanline);
                _masterCyclesIntoScanline += advance;
                remaining -= advance;

                if (_masterCyclesIntoScanline == HdmaMasterCycle && _scanline < VBlankStartScanline)
                    HdmaScanline?.Invoke(_scanline);

                if (_masterCyclesIntoScanline == MasterCyclesPerScanline)
                {
                    // Finish the visible line before advancing to the next one.
                    if (_scanline > 0 && _scanline <= ScreenHeight)
                        RenderScanline(_scanline - 1);
                    _masterCyclesIntoScanline = 0;
                    _scanline++;

                    if (_scanline == VBlankStartScanline)
                        VBlankStarted?.Invoke();

                    if (_scanline >= ScanlinesPerFrame)
                    {
                        _scanline = 0;
                        FrameStarted?.Invoke();
                    }
                }
            }
        }

        public void DumpMemorySummary()
        {
            Debug.WriteLine("========== PPU MEMORY ==========");

            Debug.WriteLine($"VRAM non-zero bytes: {_vram.Count(x => x != 0)}");
            Debug.WriteLine($"CGRAM non-zero bytes: {_cgram.Count(x => x != 0)}");
            Debug.WriteLine($"OAM non-zero bytes: {_oam.Count(x => x != 0)}");

            DumpVramRange(0x0000, 0x40);
            DumpVramRange(0x2000, 0x40);
            DumpVramRange(0x4000, 0x40);
            DumpVramRange(0x8000, 0x40);

            Debug.WriteLine("================================");
        }

        private void DumpVramRange(int address, int length)
        {
            Debug.WriteLine($"VRAM ${address:X4}:");

            for (int i = 0; i < length; i += 16)
            {
                Debug.WriteLine(string.Join(" ", _vram.Skip(address + i).Take(16).Select(x => x.ToString("X2"))));
            }
        }

        public void DumpPpuConfiguration()
        {
            Debug.WriteLine("========== PPU CONFIGURATION ==========");
            Debug.WriteLine($"Display:      ${_displayControl:X2}");
            Debug.WriteLine($"BG Mode:      ${_bgMode:X2}");
            Debug.WriteLine($"BG1 Screen:   ${_bg1ScreenBase:X2}");
            Debug.WriteLine($"BG2 Screen:   ${_bg2ScreenBase:X2}");
            Debug.WriteLine($"BG Tile Base: ${_bg12TileDataBase:X2}");
            Debug.WriteLine($"Main Screen:  ${_mainScreenDesignation:X2}");
            Debug.WriteLine($"OBJ Select:   ${_objSelection:X2}");
            Debug.WriteLine($"BG1 Scroll:   {_bg1HScroll}, {_bg1VScroll}");
            Debug.WriteLine($"BG2 Scroll:   {_bg2HScroll}, {_bg2VScroll}");
            Debug.WriteLine("=======================================");
        }
    }
}

