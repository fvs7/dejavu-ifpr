using Dejavu.Core.Infrastructure;

namespace Dejavu.Core.Services;

public static class MaintenanceState
{
    public const string None = "NONE";
    public const string Scheduled = "SCHEDULED";
    public const string Running = "RUNNING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string FailedStillFrozen = "FAILED_STILL_FROZEN";
    public const string AbortedLoopDetected = "ABORTED_LOOP_DETECTED";
}

/// <summary>
/// Janela de manutenção: descongela o sistema, executa as atualizações no boot seguinte
/// (tarefa agendada ONSTART) e recongela + reinicia ao final.
/// </summary>
public class MaintenanceService
{
    public const string StateKeyPath = @"SOFTWARE\Dejavu\Maintenance";
    private static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(60);

    private readonly LoggerService _logger;
    private readonly IUwfController _uwf;
    private readonly ScheduledTaskService _tasks;
    private readonly IProcessRunner _runner;
    private readonly IStateStore _state;

    public MaintenanceService(LoggerService logger, IUwfController uwf, ScheduledTaskService tasks, IProcessRunner runner, IStateStore state)
    {
        _logger = logger;
        _uwf = uwf;
        _tasks = tasks;
        _runner = runner;
        _state = state;
    }

    public void ScheduleMaintenanceWindow()
    {
        _logger.Log(LogLevel.Info, "[Manutenção] Agendando janela de manutenção...");
        try
        {
            SetMaintenanceState(MaintenanceState.Scheduled);
            _tasks.CreateOnStartTask(ScheduledTaskService.MaintenanceTask, "maintenance --run");
            // Thaw() também remove um Thaw-Once pendente, cuja tarefa recongelaria o sistema
            // antes de a manutenção terminar.
            _uwf.Thaw();
        }
        catch
        {
            _tasks.DeleteTask(ScheduledTaskService.MaintenanceTask);
            SetMaintenanceState(MaintenanceState.None);
            throw;
        }
    }

    public void ExecuteMaintenanceRoutine()
    {
        string state = GetMaintenanceState();

        if (state == MaintenanceState.Running)
        {
            _logger.Log(LogLevel.Error, "[Manutenção] Loop de reboot detectado! Interrompendo manutenção e recongelando por segurança.");
            SetMaintenanceState(MaintenanceState.AbortedLoopDetected);
            FinishAndRefreeze();
            return;
        }

        if (state != MaintenanceState.Scheduled)
        {
            _logger.Log(LogLevel.Warning, $"[Manutenção] Nenhuma janela agendada (estado: {state}). Removendo tarefa órfã.");
            _tasks.DeleteTask(ScheduledTaskService.MaintenanceTask);
            return;
        }

        if (_uwf.GetStatus().CurrentEnabled)
        {
            // Com o filtro ativo, as atualizações seriam descartadas no próximo boot.
            _logger.Log(LogLevel.Error, "[Manutenção] O sistema iniciou CONGELADO; as atualizações não seriam persistidas. Manutenção cancelada.");
            SetMaintenanceState(MaintenanceState.FailedStillFrozen);
            _tasks.DeleteTask(ScheduledTaskService.MaintenanceTask);
            return;
        }

        SetMaintenanceState(MaintenanceState.Running);
        _logger.Log(LogLevel.Info, "[Manutenção] Iniciando rotina de atualizações do laboratório...");

        try
        {
            // 1. Atualização via winget (softwares básicos)
            RunStep(ResolveWingetPath(), "upgrade", "--all", "--accept-source-agreements", "--accept-package-agreements", "--silent", "--disable-interactivity");

            // 2. Windows Update (requer o módulo PSWindowsUpdate instalado na imagem)
            RunStep("powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
                "if (Get-Module -ListAvailable -Name PSWindowsUpdate) { Get-WindowsUpdate -AcceptAll -Install -IgnoreReboot }");

            // 3. Atualização de políticas de grupo (GPO)
            RunStep("gpupdate.exe", "/force");

            _logger.Log(LogLevel.Info, "[Manutenção] Rotinas finalizadas com sucesso. Programando recongelamento e reinicialização.");
            SetMaintenanceState(MaintenanceState.Completed);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "[Manutenção] Erro durante a execução da manutenção.", ex);
            SetMaintenanceState(MaintenanceState.Failed);
        }

        FinishAndRefreeze();
    }

    private void FinishAndRefreeze()
    {
        _tasks.DeleteTask(ScheduledTaskService.MaintenanceTask);
        _uwf.Freeze("C:");
        _uwf.RestartSystem();
    }

    private void RunStep(string fileName, params string[] args)
    {
        try
        {
            _logger.Log(LogLevel.Info, $"[Manutenção] Executando: {fileName} {string.Join(' ', args)}");
            int exitCode = _runner.Run(fileName, args, StepTimeout);
            if (exitCode == -1)
            {
                _logger.Log(LogLevel.Warning, $"[Manutenção] {fileName} excedeu o tempo limite de {StepTimeout.TotalMinutes} minutos e foi encerrado.");
            }
            else if (exitCode != 0)
            {
                _logger.Log(LogLevel.Warning, $"[Manutenção] {fileName} terminou com código {exitCode}.");
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning, $"[Manutenção] Aviso ao rodar {fileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Como SYSTEM, o alias "winget" não está no PATH; procura o executável no pacote App Installer.
    /// </summary>
    private static string ResolveWingetPath()
    {
        if (!OperatingSystem.IsWindows()) return "winget";
        try
        {
            string windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            var candidate = Directory
                .GetDirectories(windowsApps, "Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe")
                .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => Path.Combine(d, "winget.exe"))
                .FirstOrDefault(File.Exists);
            return candidate ?? "winget";
        }
        catch
        {
            return "winget";
        }
    }

    private void SetMaintenanceState(string state)
    {
        _state.Set("State", state);
        _state.Set("LastRun", DateTime.UtcNow.ToString("o"));
    }

    public string GetMaintenanceState() => _state.Get("State") ?? MaintenanceState.None;
}
