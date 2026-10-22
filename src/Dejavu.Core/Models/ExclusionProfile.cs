namespace Dejavu.Core.Models;

public class ExclusionProfile
{
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = "Perfil de exclusões persistentes Dejavu IFPR";
    public List<string> FileAndFolderExclusions { get; set; } = new();
    public List<string> RegistryExclusions { get; set; } = new();
}
