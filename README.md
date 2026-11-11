# JollySnesEmu

**JollySnesEmu** is a work-in-progress Super Nintendo Entertainment System (SNES) emulator written in C# using **.NET 10**, **WPF**, and **Jolly2DEngine** for DirectX-based graphics output.

The project is being developed incrementally as a learning and experimentation project. It is **not yet a complete or fully accurate SNES emulator**.

## Current progress

- **CPU:** 65C816 CPU emulation, instruction execution, memory access and interrupts.
- **Cartridges:** ROM loading and LoROM support, including testing with *Super Mario World*.
- **Memory:** WRAM, cartridge mapping and hardware register access.
- **Graphics (PPU):** VRAM, CGRAM, OAM, tiled backgrounds, scrolling, sprites, priorities, 8×8/16×16 background tiles, scanline rendering, basic subscreen composition and colour maths.
- **Audio:** SPC700, APU/DSP emulation and audio output using NAudio. Audio accuracy and timing still need improvement.
- **Host application:** WPF interface with a Jolly2DEngine DirectX rendering control.

*Super Mario World* has reached its title screen with graphics and music, but game compatibility and correctness are still under development.

## Requirements

- Windows 10/11, 64-bit
- Visual Studio with .NET desktop development support
- .NET 10 SDK
- DirectX-compatible graphics hardware

The project targets `net10.0-windows` and `x64`.

## Building and running

1. Clone or download this repository.
2. Open `JollySnesEmu.slnx` in Visual Studio.
3. Select the **x64** configuration and restore NuGet packages.
4. Build and run the project.
5. Load a legally obtained SNES ROM using the application interface.

The application uses a local reference to `Libraries/Jolly2DEngine.dll`, which must be present alongside the project in the expected directory structure.

### DirectX shaders

Jolly2DEngine requires compiled DirectX shader files (`.cso`) at runtime. The project includes them under:

```text
JollySnesEmu/Libraries/Rendering/DirectX/Shaders/
```

The project file should copy these to the corresponding `Rendering/DirectX/Shaders/` directory in the build output. If the window opens and immediately closes, check the Visual Studio **Debug Output** for an exception, and verify that the shader files are present under the application's output directory. Missing shaders are one possible cause of startup failure, not a confirmed diagnosis.

## Project layout

```text
JollySnesEmu/
├── Audio/          # SPC700, APU, DSP and audio output
├── CPU/            # 65C816 processor implementation
├── Cartridge/      # ROM loading and cartridge mapping
├── Diagnostics/    # Execution tracing and debugging
├── Emulation/      # Emulator coordination
├── Game/           # Jolly2DEngine integration
├── Memory/         # SNES memory bus
├── Video/          # PPU and framebuffer
├── Libraries/      # Jolly2DEngine runtime dependency and shaders
├── App.xaml
├── MainWindow.xaml
└── JollySnesEmu.csproj
```

## Known limitations

JollySnesEmu is still experimental. Hardware timing, graphics features such as window masking, audio synchronisation, cartridge compatibility, and other SNES behaviours require further work. Successfully loading one game does not imply compatibility with other games.

## Development approach

The emulator is kept in a **single WPF project**. SNES hardware emulation remains separate from Jolly2DEngine, which provides the host rendering and application infrastructure. The engine can be extended as needed without coupling the emulator's hardware logic to DirectX.

## ROMs and legal notice

No commercial game ROMs or Nintendo-owned game assets are included in this repository. Users are responsible for obtaining and using ROMs in accordance with applicable law.

JollySnesEmu is an independent hobby project and is not affiliated with or endorsed by Nintendo.
