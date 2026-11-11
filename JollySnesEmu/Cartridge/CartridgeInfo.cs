namespace JollySnesEmu.Cartridge;

public enum CartridgeMapping
{
    Unknown,
    LoRom,
    HiRom,
    ExLoRom,
    ExHiRom
}

public sealed record CartridgeInfo(
    string Title,
    CartridgeMapping Mapping,
    int RomSizeBytes,
    int SramSizeBytes,
    byte CountryCode,
    byte Version);
