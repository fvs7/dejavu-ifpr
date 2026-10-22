using Dejavu.Core.Infrastructure;

namespace Dejavu.Core.Services;

public class OverlayMonitorService
{
    public const int RebootDelaySeconds = 60;
    private const string RebootMessage = "O espaço temporário de memória do computador está esgotado. Para evitar congelamento do sistema, o computador será reiniciado em 60 segundos. Salve seu trabalho!";

    private readonly LoggerService _logger;
    private readonly IUwfController _uwf;
    private readonly IProcessRunner _runner;

    public OverlayMonitorService(LoggerService logger, IUwfController uwf, IProcessRunner runner)
    {
        _logger = logger;
        _uwf = uwf;
        _runner = runner;
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
            _logger.Log(LogLevel.Critical, $"[Overlay Crítico] Uso atingiu {current}MB (Limite: {crit}MB). Notificando e agendando reboot preventivo em {RebootDelaySeconds} segundos!");
            TriggerRebootNotification();
        }
        else if (warn > 0 && current >= warn)
        {
            _logger.Log(LogLevel.Warning, $"[Overlay Alerta] Uso atingiu {current}MB (Limite Alerta: {warn}MB).");
        }
    }

    private void TriggerRebootNotification()
    {
        try
        {
            // Contagem regressiva com aviso ao usuário da sessão interativa. Se já houver um
            // desligamento agendado (verificação anterior), o shutdown.exe apenas retorna erro.
            _runner.Run("shutdown.exe", new[] { "/r", "/f", "/t", RebootDelaySeconds.ToString(), "/c", RebootMessage }, TimeSpan.FromMinutes(1));
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "[Overlay] Falha ao disparar comando de shutdown com aviso.", ex);
        }
    }
}
