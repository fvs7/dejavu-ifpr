using Microsoft.Win32;

namespace Dejavu.Core.Services;

public class ThawOnceService
{
    private const string RegSubKey = @"SOFTWARE\Dejavu";
    private const string ValueName = "ThawOnceActive";
    private readonly LoggerService _logger;
    private readonly ScheduledTaskService _tasks;

    public ThawOnceService(LoggerService logger, ScheduledTaskService tasks)
    {
        _logger = logger;
        _tasks = tasks;
    }

    public bool IsThawOnceActive()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegSubKey);
            if (key != null)
            {
                var val = key.GetValue(ValueName);
                return val != null && Convert.ToInt32(val) == 1;
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning, "Falha ao verificar status do Thaw-Once no registro", ex);
        }
        return false;
    }

    public void ArmThawOnce()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.LocalMachine.CreateSubKey(RegSubKey);
                key.SetValue(ValueName, 1, RegistryValueKind.DWord);
            }

            // Tarefa executada como SYSTEM na inicialização descongelada, que programa o recongelamento.
            _tasks.CreateOnStartTask(ScheduledTaskService.ThawOnceTask, "--finalize-thaw-once");

            _logger.Log(LogLevel.Info, "Thaw-Once armado com sucesso: o sistema descongelará no próximo boot e recongelará automaticamente no boot seguinte.");
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "Erro ao armar o mecanismo Thaw-Once", ex);
            throw;
        }
    }

    public void ClearThawOnce()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.LocalMachine.OpenSubKey(RegSubKey, true);
                key?.DeleteValue(ValueName, false);
            }

            _tasks.DeleteTask(ScheduledTaskService.ThawOnceTask);
        }
        catch
        {
            // Silencioso se a chave ou tarefa não existir
        }
    }
}
