using Jolly2DEngine.Graphics;
using Jolly2DEngine.Rendering.DirectX;

namespace JollySnesEmu.Video
{
    public sealed class SnesFrameBuffer
    {
        public const int DefaultWidth = 256;
        public const int DefaultHeight = 224;

        public const int Width = DefaultWidth;
        public const int Height = DefaultHeight;

        private readonly DirectXJollyRenderer _renderer;
        private readonly byte[] _pixels;

        private readonly JollyTexture _texture;
        private readonly JollySprite _sprite;

        public Span<byte> PixelData => _pixels;
        
        public SnesFrameBuffer(DirectXJollyRenderer renderer)
        {
            ArgumentNullException.ThrowIfNull(renderer);

            _renderer = renderer;
            _pixels = new byte[Width * Height * 4];

            Clear(0, 0, 0);

            _texture = _renderer.CreateTextureFromBgraPixels(Width, Height, _pixels);

            _sprite = _renderer.CreateSprite(_texture);

            // Sprite positions are centre-based in Jolly2DEngine.
            _sprite.X = 0;
            _sprite.Y = 0;
            _sprite.Width = Width;
            _sprite.Height = Height;

            _renderer.SetLogicalResolution(Width, Height);
        }

        public void Clear()
        {
            Array.Fill(_pixels, (byte)0);
        }

        public void Clear(byte red, byte green, byte blue)
        {
            for (int i = 0; i < _pixels.Length; i += 4)
            {
                _pixels[i] = blue;
                _pixels[i + 1] = green;
                _pixels[i + 2] = red;
                _pixels[i + 3] = 255;
            }
        }

        public void SetPixel(int x, int y, byte red, byte green, byte blue)
        {
            if ((uint)x >= Width || (uint)y >= Height)
            {
                return;
            }

            int index = (y * Width + x) * 4;

            _pixels[index] = blue;
            _pixels[index + 1] = green;
            _pixels[index + 2] = red;
            _pixels[index + 3] = 255;
        }

        public void DrawTestPattern()
        {
            Clear(0, 0, 0);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (x < 64)
                    {
                        SetPixel(x, y, 255, 0, 0);
                    }
                    else if (x < 128)
                    {
                        SetPixel(x, y, 0, 255, 0);
                    }
                    else if (x < 192)
                    {
                        SetPixel(x, y, 0, 0, 255);
                    }
                    else
                    {
                        SetPixel(x, y, 255, 255, 255);
                    }
                }
            }
        }

        public void Present()
        {
            _renderer.UpdateTextureFromBgraPixels(_texture, _pixels);
            JollyRenderFrame frame = new([_sprite]);
            _renderer.SubmitFrame(frame);
        }

        public void Dispose()
        {
            //_sprite.Dispose();
            //_texture.Dispose();
        }
    }
}
