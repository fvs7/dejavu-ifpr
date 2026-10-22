using Dejavu.Core.Infrastructure;

namespace Dejavu.Core.Services;

/// <summary>
/// Cria e remove tarefas agendadas do Windows (schtasks.exe) que executam o dejavu.exe como SYSTEM.
/// </summary>
public class ScheduledTaskService
{
    public const string ThawOnceTask = "Dejavu_FinalizeThawOnce";
    public const string MaintenanceTask = "Dejavu_MaintenanceRun";
    public const string OverlayMonitorTask = "Dejavu_OverlayMonitor";

    private readonly IProcessRunner _runner;
    private readonly LoggerService _logger;
    private readonly string _exePath;

    public ScheduledTaskService(IProcessRunner runner, LoggerService logger, string? exePath = null)
    {
        _runner = runner;
        _logger = logger;
        _exePath = exePath ?? Environment.ProcessPath ?? "dejavu.exe";
    }

    /// <summary>
    /// Valor do parâmetro /TR: o caminho do executável entre aspas (pode conter espaços) seguido dos argumentos.
    /// </summary>
    public static string BuildTaskCommand(string exePath, string arguments) => $"\"{exePath}\" {arguments}";

    public void CreateOnStartTask(string taskName, string arguments)
    {
        Create(taskName, arguments, new[] { "/SC", "ONSTART" });
    }

    public void CreateRecurringTask(string taskName, string arguments, int intervalMinutes)
    {
        if (intervalMinutes < 1 || intervalMinutes > 1439)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMinutes), "O intervalo deve estar entre 1 e 1439 minutos.");
        }
        Create(taskName, arguments, new[] { "/SC", "MINUTE", "/MO", intervalMinutes.ToString() });
    }

    public void DeleteTask(string taskName)
    {
        // Código de saída diferente de zero quando a tarefa não existe: ignorado de propósito.
        _runner.Run("schtasks.exe", new[] { "/Delete", "/F", "/TN", taskName }, TimeSpan.FromMinutes(1));
    }

    private void Create(string taskName, string arguments, IEnumerable<string> schedule)
    {
        var args = new List<string>
        {
            "/Create", "/F",
            "/TN", taskName,
            "/TR", BuildTaskCommand(_exePath, arguments)
        };
        args.AddRange(schedule);
        args.AddRange(new[] { "/RU", "SYSTEM", "/RL", "HIGHEST" });

        int exitCode = _runner.Run("schtasks.exe", args, TimeSpan.FromMinutes(1));
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"schtasks.exe falhou ao criar a tarefa '{taskName}' (código {exitCode}).");
        }
        _logger.Log(LogLevel.Info, $"Tarefa agendada '{taskName}' criada: {BuildTaskCommand(_exePath, arguments)}");
    }
}
