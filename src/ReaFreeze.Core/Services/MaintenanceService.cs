using System.Diagnostics;
using Microsoft.Win32;

namespace ReaFreeze.Core.Services;

public class MaintenanceService
{
    private readonly LoggerService _logger;
    private readonly IUwfController _uwf;
    private readonly ThawOnceService _thawOnce;
    private const string LockKeyPath = @"SOFTWARE\ReaFreeze\Maintenance";

    public MaintenanceService(LoggerService logger, IUwfController uwf, ThawOnceService thawOnce)
    {
        _logger = logger;
        _uwf = uwf;
        _thawOnce = thawOnce;
    }

    public void ScheduleMaintenanceWindow()
    {
        _logger.Log(LogLevel.Info, "[Manutenção] Agendando janela de manutenção (Thaw-Once)...");
        SetMaintenanceState("SCHEDULED");
        _thawOnce.ArmThawOnce();
    }

    public void ExecuteMaintenanceRoutine()
    {
        if (!OperatingSystem.IsWindows()) return;

        string state = GetMaintenanceState();
        if (state == "RUNNING")
        {
            _logger.Log(LogLevel.Error, "[Manutenção] Loop de reboot detectado! Interrompendo manutenção e recongelando por segurança.");
            SetMaintenanceState("ABORTED_LOOP_DETECTED");
            _uwf.Freeze("C:");
            return;
        }

        SetMaintenanceState("RUNNING");
        _logger.Log(LogLevel.Info, "[Manutenção] Iniciando rotina de atualizações do laboratório...");

        try
        {
            // 1. Atualização via winget (softwares básicos)
            RunProcess("winget", "upgrade --all --accept-source-agreements --accept-package-agreements --silent");

            // 2. Windows Update trigger
            RunProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"if (Get-Module -ListAvailable -Name PSWindowsUpdate) { Get-WindowsUpdate -AcceptAll -Install -IgnoreReboot }\"");

            // 3. Atualização de políticas de grupo (GPO)
            RunProcess("gpupdate.exe", "/force");

            _logger.Log(LogLevel.Info, "[Manutenção] Rotinas finalizadas com sucesso. Programando recongelamento e reinicialização.");
            SetMaintenanceState("COMPLETED");
            _uwf.Freeze("C:");
            _uwf.RestartSystem();
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "[Manutenção] Erro durante a execução da manutenção.", ex);
            SetMaintenanceState("FAILED");
            _uwf.Freeze("C:");
        }
    }

    private void RunProcess(string fileName, string args)
    {
        try
        {
            _logger.Log(LogLevel.Info, $"[Manutenção] Executando: {fileName} {args}");
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(600000); // 10 minutos por etapa
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning, $"[Manutenção] Aviso ao rodar {fileName}: {ex.Message}");
        }
    }

    private void SetMaintenanceState(string state)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Registry.LocalMachine.CreateSubKey(LockKeyPath, true);
        key?.SetValue("State", state, RegistryValueKind.String);
        key?.SetValue("LastRun", DateTime.UtcNow.ToString("o"), RegistryValueKind.String);
    }

    public string GetMaintenanceState()
    {
        if (!OperatingSystem.IsWindows()) return "NONE";
        using var key = Registry.LocalMachine.OpenSubKey(LockKeyPath, false);
        return key?.GetValue("State") as string ?? "NONE";
    }
}
