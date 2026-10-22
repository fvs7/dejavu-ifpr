using Dejavu.Core.Models;
using Dejavu.Core.Services;

namespace Dejavu.Tests;

public class MockUwfController : IUwfController
{
    public bool Installed { get; set; } = true;
    public bool CurrentEnabled { get; set; } = true;
    public bool NextEnabled { get; set; } = true;
    public List<string> AddedFileExclusions { get; } = new();
    public List<string> AddedRegistryExclusions { get; } = new();
    public OverlayConfig Overlay { get; set; } = new();
    public int RestartCount { get; private set; }

    public bool IsUwfInstalled() => Installed;

    public UwfStatus GetStatus() => new UwfStatus
    {
        FilterInstalled = Installed,
        CurrentEnabled = CurrentEnabled,
        NextEnabled = NextEnabled,
        Overlay = Overlay,
        FileExclusions = new List<string>(AddedFileExclusions),
        RegistryExclusions = new List<string>(AddedRegistryExclusions)
    };

    public void Freeze(string volume = "C:")
    {
        NextEnabled = true;
    }

    public void Thaw()
    {
        NextEnabled = false;
    }

    public void ConfigureOverlay(OverlayType type, uint maxSizeMb, uint warningMb, uint criticalMb)
    {
        Overlay.Type = type;
        Overlay.MaximumSizeMb = maxSizeMb;
        Overlay.WarningThresholdMb = warningMb;
        Overlay.CriticalThresholdMb = criticalMb;
    }

    public void AddFileExclusion(string volume, string path)
    {
        AddedFileExclusions.Add(path);
    }

    public void RemoveFileExclusion(string volume, string path)
    {
        AddedFileExclusions.Remove(path);
    }

    public void AddRegistryExclusion(string key)
    {
        AddedRegistryExclusions.Add(key);
    }

    public void RemoveRegistryExclusion(string key)
    {
        AddedRegistryExclusions.Remove(key);
    }

    public void RestartSystem() => RestartCount++;
}
