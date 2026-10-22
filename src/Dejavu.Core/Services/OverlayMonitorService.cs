using System.Diagnostics;

namespace Dejavu.Core.Services;

public class OverlayMonitorService
{
    private readonly LoggerService _logger;
    private readonly IUwfController _uwf;

    public OverlayMonitorService(LoggerService logger, IUwfController uwf)
    {
        _logger = logger;
        _uwf = uwf;
    }

    public void CheckOverlayAndEnforcePolicy()
    {
        var status = _uwf.GetStatus();
        if (!status.CurrentEnabled || status.Overlay == null)
        {
            return;
        }

        uint current = status.Overlay.CurrentUsageMb;
        uint warn = status.Overlay.WarningThresholdMb;
        uint crit = status.Overlay.CriticalThresholdMb;

        if (crit > 0 && current >= crit)
        {
            _logger.Log(LogLevel.Critical, $"[Overlay Crítico] Uso atingiu {current}MB (Limite: {crit}MB). Notificando e agendando reboot preventivo em 60 segundos!");
            TriggerRebootNotification(60, "O espaço temporário de memória do computador está esgotado. Para evitar congelamento do sistema, o computador será reiniciado em 60 segundos. Salve seu trabalho!");
        }
        else if (warn > 0 && current >= warn)
        {
            _logger.Log(LogLevel.Warning, $"[Overlay Alerta] Uso atingiu {current}MB (Limite Alerta: {warn}MB).");
        }
    }

    private void TriggerRebootNotification(int seconds, string message)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            // Invoca shutdown.exe com contagem regressiva e aviso ao usuário da sessão interativa
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = $"/r /t {seconds} /c \"{message}\" /f",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "[Overlay] Falha ao disparar comando de shutdown com aviso.", ex);
        }
    }
}
