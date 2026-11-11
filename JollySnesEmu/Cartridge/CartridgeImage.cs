using System.IO;

namespace JollySnesEmu.Cartridge
{
    public sealed class CartridgeImage
    {
        private CartridgeImage(string filePath, byte[] data)
        {
            FilePath = filePath;
            Data = data;
        }

        public string FilePath { get; }
        public byte[] Data { get; }
        public int Length => Data.Length;

        public static async Task<CartridgeImage> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            var data = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);

            if (data.Length == 0)
                throw new InvalidDataException("The cartridge image is empty.");

            return new CartridgeImage(Path.GetFullPath(filePath), data);
        }
    }
}