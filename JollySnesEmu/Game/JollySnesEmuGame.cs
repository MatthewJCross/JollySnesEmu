using Jolly2DEngine.Core;
using Jolly2DEngine.Rendering.DirectX;
using JollySnesEmu.Audio;
using JollySnesEmu.Cartridge;
using JollySnesEmu.CPU;
using JollySnesEmu.Memory;
using JollySnesEmu.Video;

namespace JollySnesEmu.Game
{
    /// <summary>
    /// Hosts and coordinates the SNES hardware within Jolly2DEngine.
    /// Timing and HDMA scheduling follow the Stage 3 PPU/MemoryBus integration,
    /// adapted from SuperNintendoEmulator's SNESSystem.
    /// </summary>
    public sealed class JollySnesEmuGame : JollyGame
    {
        private const int CpuCyclesPerUpdate = 14915;
        private const double CpuToApuCycleRatio = 3.5;

        private readonly DirectXControl _gameView;

        private DirectXJollyRenderer? _renderer;
        private SnesFrameBuffer? _framebuffer;
        private SnesPpu? _ppu;
        private SnesApu? _apu;
        private SnesAudioOutput? _audioOutput;
        private Spc700? _spc700;
        private MemoryBus? _memoryBus;
        private Cpu65816? _cpu;
        private bool _cartridgeInserted;

        public JollySnesEmuGame(DirectXControl gameView)
        {
            _gameView = gameView ?? throw new ArgumentNullException(nameof(gameView));
            GameLoopFPS = 240.0;
            RenderFPS = 60.0;
        }

        protected override void OnInitialize()
        {
            _renderer = new DirectXJollyRenderer(_gameView);
            _framebuffer = new SnesFrameBuffer(_renderer);
            _ppu = new SnesPpu(_framebuffer);
            _apu = new SnesApu();
            _audioOutput = new SnesAudioOutput();
            _apu.SamplesReady += _audioOutput.Submit;
            _spc700 = new Spc700(_apu);
            _memoryBus = new MemoryBus(_ppu, _apu);
            _cpu = new Cpu65816(_memoryBus);

            _framebuffer.DrawTestPattern();
            _framebuffer.Present();
        }

        public void InsertCartridge(SnesCartridge cartridge)
        {
            ArgumentNullException.ThrowIfNull(cartridge);

            if (_memoryBus is null || _cpu is null || _apu is null || _spc700 is null || _ppu is null)
                throw new InvalidOperationException("The SNES game has not been initialized.");

            _cartridgeInserted = false;
            _memoryBus.InsertCartridge(cartridge);
            _apu.Reset();
            _spc700.Reset();
            _ppu.Reset();
            _cpu.Reset();

            // SuperNintendoEmulator initializes HDMA at the start of each frame.
            // MemoryBus also initializes it on subsequent PPU FrameStarted events.
            _memoryBus.InitializeHdmaFrame();
            _cartridgeInserted = true;
        }

        protected override void OnUpdate(JollyGameTime gameTime)
        {
            if (!_cartridgeInserted || _cpu is null || _spc700 is null || _memoryBus is null || _ppu is null)
                return;

            int cpuCyclesExecuted = 0;
            int apuCyclesExecuted = 0;

            while (cpuCyclesExecuted < CpuCyclesPerUpdate)
            {
                int cycles = _cpu.Step();
                cycles += _memoryBus.ConsumeDmaCycles();
                cpuCyclesExecuted += cycles;

                // AdvanceCpuCycles raises scanline, vblank and HDMA events.
                // MemoryBus subscribes to these events in its constructor.
                _ppu.AdvanceCpuCycles(cycles);

                int targetApuCycles = (int)(cpuCyclesExecuted / CpuToApuCycleRatio);
                while (apuCyclesExecuted < targetApuCycles)
                {
                    int spcCycles = _spc700.Step();
                    apuCyclesExecuted += spcCycles;
                    _memoryBus.Apu.AdvanceTimers(spcCycles);
                    _memoryBus.Apu.AdvanceDsp(spcCycles);
                }
            }

            _framebuffer?.Present();
        }

        protected override void OnShutdown()
        {
            _cartridgeInserted = false;
            _audioOutput?.Dispose();
            _audioOutput = null;
            _cpu = null;
            _memoryBus = null;
            _spc700 = null;
            _apu = null;
            _ppu = null;
            _framebuffer = null;
            _renderer = null;
        }
    }
}
