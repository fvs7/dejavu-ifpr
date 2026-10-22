using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace ReaFreeze.Core.Services;

[SupportedOSPlatform("windows")]
public class ThawOnceService
{
    private const string RegSubKey = @"SOFTWARE\ReaFreeze";
    private const string ValueName = "ThawOnceActive";
    private const string TaskName = "ReaFreeze_FinalizeThawOnce";
    private readonly LoggerService _logger;

    public ThawOnceService(LoggerService logger)
    {
        _logger = logger;
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
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(RegSubKey);
            key.SetValue(ValueName, 1, RegistryValueKind.DWord);

            // Cria ou atualiza Tarefa Agendada no Windows para rodar como SYSTEM no próximo logon/inicialização
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "reafreeze.exe";
            string schtasksArgs = $"/Create /F /TN \"{TaskName}\" /TR \"\"{exePath}\" --finalize-thaw-once\" /SC ONSTART /RU SYSTEM /RL HIGHEST";

            var psi = new ProcessStartInfo(
                "schtasks.exe",
                schtasksArgs
            )
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi)?.WaitForExit();

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
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegSubKey, true);
            if (key != null)
            {
                key.DeleteValue(ValueName, false);
            }

            // Remove a tarefa agendada
            var psi = new ProcessStartInfo(
                "schtasks.exe",
                $"/Delete /F /TN \"{TaskName}\""
            )
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi)?.WaitForExit();
        }
        catch
        {
            // Silencioso se a chave ou tarefa não existir
        }
    }
}
