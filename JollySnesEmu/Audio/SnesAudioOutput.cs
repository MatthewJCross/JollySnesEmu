using NAudio.Wave;
using System.Diagnostics;

namespace JollySnesEmu.Audio
{
    /// <summary>Streams stereo PCM from the SNES DSP to the Windows audio device.</summary>
    public sealed class SnesAudioOutput : IDisposable
    {
        private readonly BufferedWaveProvider _buffer;
        private readonly WaveOutEvent _output;

        private int _submittedBuffers;
        private float _maximumSample;
        
        public SnesAudioOutput()
        {
            _buffer = new BufferedWaveProvider(new WaveFormat(32000, 16, 2))
            {
                DiscardOnBufferOverflow = true
            };

            _output = new WaveOutEvent();
            _output.Init(_buffer);
            _output.Play();
        }

        public void Submit(float[] left, float[] right, int count)
        {
            int samples = Math.Min(count, Math.Min(left.Length, right.Length));
            byte[] pcm = new byte[samples * 4];

            _submittedBuffers++;

            for (int i = 0; i < count; i++)
            {
                float peak = Math.Max(Math.Abs(left[i]), Math.Abs(right[i]));
                _maximumSample = Math.Max(_maximumSample, peak);
            }

            if (_submittedBuffers % 60 == 0)
            {
                _maximumSample = 0;
            }

            for (int i = 0; i < samples; i++)
            {
                short l = (short)(Math.Clamp(left[i], -1.0f, 1.0f) * 32767);
                short r = (short)(Math.Clamp(right[i], -1.0f, 1.0f) * 32767);
                int offset = i * 4;
                pcm[offset] = (byte)l;
                pcm[offset + 1] = (byte)(l >> 8);
                pcm[offset + 2] = (byte)r;
                pcm[offset + 3] = (byte)(r >> 8);
            }
            _buffer.AddSamples(pcm, 0, pcm.Length);
        }

        public void Dispose()
        {
            _output.Stop();
            _output.Dispose();
        }
    }
}
