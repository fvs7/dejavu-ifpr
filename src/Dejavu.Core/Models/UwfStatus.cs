namespace Dejavu.Core.Models;

public enum OverlayType
{
    RAM = 0,
    Disk = 1
}

public class VolumeStatus
{
    public string DriveLetter { get; set; } = string.Empty;
    public bool CurrentProtected { get; set; }
    public bool NextProtected { get; set; }
}

public class UwfStatus
{
    public bool FilterInstalled { get; set; }
    public bool CurrentEnabled { get; set; }
    public bool NextEnabled { get; set; }
    public bool ThawOncePending { get; set; }
    public OverlayConfig Overlay { get; set; } = new();
    public List<VolumeStatus> Volumes { get; set; } = new();
    public List<string> FileExclusions { get; set; } = new();
    public List<string> RegistryExclusions { get; set; } = new();
}
