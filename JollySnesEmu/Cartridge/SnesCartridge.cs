using System.IO;
using System.Text;

namespace JollySnesEmu.Cartridge
{
    public sealed class SnesCartridge
    {
        private readonly CartridgeImage _image;

        private SnesCartridge(CartridgeImage image, CartridgeInfo info)
        {
            _image = image;
            Info = info;
        }

        public CartridgeInfo Info { get; }
        public string FilePath => _image.FilePath;
        public int Length => _image.Length;

        public static async Task<SnesCartridge> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            CartridgeImage image = await CartridgeImage.LoadAsync(filePath, cancellationToken);
            CartridgeInfo info = ReadHeader(image.Data);

            return new SnesCartridge(image, info);
        }

        public byte ReadByte(int offset)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            // Mirror ROM data when the mapped address exceeds the image size.
            return _image.Data[offset % _image.Length];
        }

        private static CartridgeInfo ReadHeader(byte[] data)
        {
            int headerOffset = FindHeader(data);

            if (headerOffset < 0)
                throw new InvalidDataException("Could not find a valid SNES cartridge header.");

            string title = Encoding.ASCII.GetString(data, headerOffset, 21).TrimEnd('\0', ' ');

            byte mappingByte = data[headerOffset + 0x15];
            CartridgeMapping mapping = GetMapping(mappingByte);

            byte romSizeCode = data[headerOffset + 0x17];
            byte sramSizeCode = data[headerOffset + 0x18];

            int romSize = romSizeCode <= 20 ? 1 << (romSizeCode + 10) : 0;
            int sramSize = sramSizeCode == 0 ? 0 : sramSizeCode <= 20 ? 1 << (sramSizeCode + 10) : 0;

            byte countryCode = data[headerOffset + 0x19];
            byte version = data[headerOffset + 0x1B];

            return new CartridgeInfo(title, mapping, romSize, sramSize, countryCode, version);
        }

        private static int FindHeader(byte[] data)
        {
            int[] candidates =
            [
                0x7FC0,    // LoROM
                0xFFC0,    // HiROM
                0x407FC0,  // ExLoROM
                0x40FFC0   // ExHiROM
            ];

            int bestOffset = -1;
            int bestScore = 0;

            foreach (int offset in candidates)
            {
                if (offset + 0x40 > data.Length)
                    continue;

                int score = ScoreHeader(data, offset);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestOffset = offset;
                }
            }

            return bestScore >= 3 ? bestOffset : -1;
        }

        private static int ScoreHeader(byte[] data, int offset)
        {
            int score = 0;
            byte mappingByte = data[offset + 0x15];

            if (GetMapping(mappingByte) != CartridgeMapping.Unknown)
                score += 2;

            ushort complement = (ushort)(data[offset + 0x1C] | (data[offset + 0x1D] << 8));
            ushort checksum = (ushort)(data[offset + 0x1E] | (data[offset + 0x1F] << 8));

            if ((complement ^ checksum) == 0xFFFF)
                score += 2;

            int printable = 0;

            for (int i = 0; i < 21; i++)
            {
                byte value = data[offset + i];

                if (value >= 0x20 && value <= 0x7E)
                    printable++;
            }

            if (printable >= 10)
                score++;

            return score;
        }

        private static CartridgeMapping GetMapping(byte value)
        {
            return value switch
            {
                0x20 or 0x30 => CartridgeMapping.LoRom,
                0x21 or 0x31 => CartridgeMapping.HiRom,
                0x25 => CartridgeMapping.ExHiRom,
                0x32 => CartridgeMapping.ExLoRom,
                _ => CartridgeMapping.Unknown
            };
        }
    }
}
