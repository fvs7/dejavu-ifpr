namespace Dejavu.Core.Services;

public static class UwfPaths
{
    /// <summary>
    /// Normaliza a letra do volume para o formato usado pelo UWF ("C:").
    /// </summary>
    public static string NormalizeVolume(string volume) => volume.Trim().TrimEnd('\\').ToUpperInvariant();

    /// <summary>
    /// O UWF_Volume.AddExclusion espera o caminho relativo ao volume (ex: "\ProgramData\Veyon"),
    /// sem a letra da unidade.
    /// </summary>
    public static string ToVolumeRelative(string volume, string path)
    {
        string vol = NormalizeVolume(volume);
        string p = path.Trim();
        if (p.StartsWith(vol, StringComparison.OrdinalIgnoreCase))
        {
            p = p.Substring(vol.Length);
        }
        if (!p.StartsWith('\\'))
        {
            p = "\\" + p;
        }
        return p;
    }

    /// <summary>
    /// Monta o caminho completo ("C:\ProgramData\Veyon") a partir do volume e do caminho relativo do UWF.
    /// </summary>
    public static string ToFullPath(string volume, string relativePath) => NormalizeVolume(volume) + ToVolumeRelative(volume, relativePath);
}
