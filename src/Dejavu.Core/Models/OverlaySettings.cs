namespace Dejavu.Core.Models;

public class OverlaySettings
{
    public OverlayType Type { get; set; } = OverlayType.RAM;
    public uint MaximumSizeMb { get; set; } = 4096;
    public uint WarningThresholdMb { get; set; } = 3072;
    public uint CriticalThresholdMb { get; set; } = 3840;

    /// <summary>
    /// Lê as opções do comando config-overlay (--type, --max-mb, --warn-mb, --crit-mb) e valida os valores.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> args, out OverlaySettings settings, out string error)
    {
        settings = new OverlaySettings();
        error = string.Empty;

        for (int i = 0; i < args.Count; i++)
        {
            string opt = args[i].ToLowerInvariant();
            if (opt is not ("--type" or "--max-mb" or "--warn-mb" or "--crit-mb"))
            {
                error = $"Opção desconhecida: {args[i]}";
                return false;
            }
            if (i + 1 >= args.Count)
            {
                error = $"A opção {args[i]} exige um valor.";
                return false;
            }

            string value = args[++i];
            if (opt == "--type")
            {
                if (value.Equals("ram", StringComparison.OrdinalIgnoreCase)) settings.Type = OverlayType.RAM;
                else if (value.Equals("disk", StringComparison.OrdinalIgnoreCase)) settings.Type = OverlayType.Disk;
                else
                {
                    error = $"Tipo de overlay inválido: '{value}' (use ram ou disk).";
                    return false;
                }
                continue;
            }

            if (!uint.TryParse(value, out uint mb))
            {
                error = $"Valor inválido para {args[i - 1]}: '{value}' (informe um número inteiro de MB).";
                return false;
            }

            switch (opt)
            {
                case "--max-mb": settings.MaximumSizeMb = mb; break;
                case "--warn-mb": settings.WarningThresholdMb = mb; break;
                case "--crit-mb": settings.CriticalThresholdMb = mb; break;
            }
        }

        string? validation = settings.Validate();
        if (validation != null)
        {
            error = validation;
            return false;
        }
        return true;
    }

    public string? Validate()
    {
        if (MaximumSizeMb < 1024)
            return "O tamanho máximo do overlay deve ser de pelo menos 1024 MB.";
        if (WarningThresholdMb == 0 || CriticalThresholdMb == 0)
            return "Os limites de alerta e crítico devem ser maiores que zero.";
        if (WarningThresholdMb >= CriticalThresholdMb)
            return "O limite de alerta (--warn-mb) deve ser menor que o limite crítico (--crit-mb).";
        if (CriticalThresholdMb > MaximumSizeMb)
            return "O limite crítico (--crit-mb) não pode ser maior que o tamanho máximo (--max-mb).";
        return null;
    }
}
