using ReaFreeze.Core.Models;

namespace ReaFreeze.Core.Services;

public interface IUwfController
{
    bool IsUwfInstalled();
    UwfStatus GetStatus();
    void Freeze(string volume = "C:");
    void Thaw();
    void ConfigureOverlay(OverlayType type, uint maxSizeMb, uint warningMb, uint criticalMb);
    void AddFileExclusion(string volume, string path);
    void RemoveFileExclusion(string volume, string path);
    void AddRegistryExclusion(string key);
    void RemoveRegistryExclusion(string key);
    void RestartSystem();
}
