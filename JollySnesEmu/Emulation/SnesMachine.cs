namespace JollySnesEmu.Emulation;

/// <summary>Coordinates SNES hardware. Independent of WPF and Jolly2DEngine.</summary>
public sealed class SnesMachine
{
    public bool IsRunning { get; private set; }
    public long MasterCycles { get; private set; }

    public void PowerOn()
    {
        MasterCycles = 0;
        IsRunning = true;
    }

    public void PowerOff() => IsRunning = false;

    public void RunCycles(int cycles)
    {
        if (cycles < 0)
            throw new ArgumentOutOfRangeException(nameof(cycles));

        if (IsRunning)
            MasterCycles += cycles;
    }
}
