using Microsoft.Win32;

namespace Dejavu.Core.Infrastructure;

public interface IStateStore
{
    string? Get(string name);
    void Set(string name, string value);
}

/// <summary>
/// Armazena estado em HKLM\SOFTWARE\Dejavu\... (chave incluída nas exclusões do UWF,
/// portanto persiste entre reinicializações mesmo com o sistema congelado).
/// </summary>
public class RegistryStateStore : IStateStore
{
    private readonly string _subKey;

    public RegistryStateStore(string subKey)
    {
        _subKey = subKey;
    }

    public string? Get(string name)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var key = Registry.LocalMachine.OpenSubKey(_subKey, false);
        return key?.GetValue(name) as string;
    }

    public void Set(string name, string value)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Registry.LocalMachine.CreateSubKey(_subKey, true);
        key.SetValue(name, value, RegistryValueKind.String);
    }
}
