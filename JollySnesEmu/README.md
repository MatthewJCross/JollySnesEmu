# JollySnesEmu

Single-project .NET 10 WPF SNES emulator using Jolly2DEngine.

## Engine integration

- WPF hosts `Jolly2DEngine.Rendering.DirectX.DirectXControl`.
- `JollySnesEmuGame` derives from `Jolly2DEngine.Core.JollyGame`.
- The game creates `DirectXJollyRenderer` and uses the engine lifecycle.
- SNES-specific hardware stays inside JollySnesEmu.

The project references the bundled `Libraries/Jolly2DEngine.dll`. Keep the `Libraries` directory alongside the project.
