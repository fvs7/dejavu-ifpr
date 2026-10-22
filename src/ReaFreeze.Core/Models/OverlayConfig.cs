namespace ReaFreeze.Core.Models;

public class OverlayConfig
{
    public OverlayType Type { get; set; } = OverlayType.RAM;
    public uint MaximumSizeMb { get; set; } = 4096;
    public uint WarningThresholdMb { get; set; } = 3072;
    public uint CriticalThresholdMb { get; set; } = 3840;
    public uint CurrentUsageMb { get; set; } = 0;
}
