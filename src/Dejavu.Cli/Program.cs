using System.Reflection;
using System.Text.Json;
using Dejavu.Core.Models;

namespace Dejavu.Cli;

class Program
{
    static int Main(string[] args)
    {
        var logger = new Dejavu.Core.Services.LoggerService();
        var thawOnce = new Dejavu.Core.Services.ThawOnceService(logger);
        var uwf = new Dejavu.Core.Services.UwfController(logger, thawOnce);
        var profileService = new Dejavu.Core.Services.ProfileService(logger);
        var domainService = new Dejavu.Core.Services.DomainService(logger);
        var thawSpaceService = new Dejavu.Core.Services.ThawSpaceService(logger);
        var maintenanceService = new Dejavu.Core.Services.MaintenanceService(logger, uwf, thawOnce);
        var overlayMonitor = new Dejavu.Core.Services.OverlayMonitorService(logger, uwf);
        var apiServer = new Dejavu.Core.Services.ApiServerService(logger, uwf, thawOnce, maintenanceService);
        var clusterManager = new Dejavu.Core.Services.ClusterManagerService(logger);

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        try
        {
            switch (command)
            {
                case "status":
                    return HandleStatus(uwf, args);

                case "freeze":
                    return HandleFreeze(uwf, args);

                case "thaw":
                    return HandleThaw(uwf, thawOnce, args);

                case "config-overlay":
                    return HandleConfigOverlay(uwf, args);

                case "apply-profile":
                    return HandleApplyProfile(uwf, profileService, args);

                case "setup-domain":
                    return HandleSetupDomain(domainService);

                case "setup-thawspace":
                    return HandleSetupThawSpace(uwf, thawSpaceService, args);

                case "maintenance":
                    return HandleMaintenance(maintenanceService, args);

                case "check-overlay":
                    return HandleCheckOverlay(overlayMonitor);

                case "serve":
                    return HandleServe(apiServer, args);

                case "cluster":
                    return HandleCluster(clusterManager, args).GetAwaiter().GetResult();

                case "--finalize-thaw-once":
                    return HandleFinalizeThawOnce(uwf, thawOnce, logger);

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Comando desconhecido: {args[0]}");
                    Console.ResetColor();
                    PrintHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERRO] {ex.Message}");
            Console.ResetColor();
            logger.Log(Dejavu.Core.Services.LogLevel.Error, $"Erro na execução do comando '{command}'", ex);
            return 1;
        }
    }

    static int HandleStatus(Dejavu.Core.Services.IUwfController uwf, string[] args)
    {
        bool jsonOutput = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));
        var status = uwf.GetStatus();

        if (jsonOutput)
        {
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine(JsonSerializer.Serialize(status, jsonOptions));
            return 0;
        }

        Console.WriteLine("====================================================");
        Console.WriteLine("             Dejavu IFPR — Estado do Sistema          ");
        Console.WriteLine("====================================================");
        Console.WriteLine($"Filtro UWF Instalado:    {(status.FilterInstalled ? "Sim" : "NÃO / Indisponível")}");

        string currentDesc = status.CurrentEnabled ? "CONGELADO (Frozen)" : "DESCONGELADO (Thawed)";
        string nextDesc = status.NextEnabled ? "CONGELADO (Frozen)" : "DESCONGELADO (Thawed)";

        Console.ForegroundColor = status.CurrentEnabled ? ConsoleColor.Cyan : ConsoleColor.Yellow;
        Console.WriteLine($"Estado Atual (Sessão):   {currentDesc}");
        Console.ForegroundColor = status.NextEnabled ? ConsoleColor.Cyan : ConsoleColor.Yellow;
        Console.WriteLine($"Estado Próximo Boot:     {nextDesc}");
        Console.ResetColor();

        if (status.ThawOncePending)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Modo Thaw-Once:          ATIVO (Recongelamento automático no próximo boot)");
            Console.ResetColor();
        }

        Console.WriteLine("----------------------------------------------------");
        Console.WriteLine("Configuração do Overlay:");
        Console.WriteLine($"  Tipo:                 {status.Overlay.Type}");
        Console.WriteLine($"  Tamanho Máximo:       {status.Overlay.MaximumSizeMb} MB");
        Console.WriteLine($"  Alerta (Warning):     {status.Overlay.WarningThresholdMb} MB");
        Console.WriteLine($"  Crítico (Critical):   {status.Overlay.CriticalThresholdMb} MB");
        Console.WriteLine($"  Uso Atual:            {status.Overlay.CurrentUsageMb} MB");

        Console.WriteLine("----------------------------------------------------");
        Console.WriteLine("Volumes Protegidos:");
        foreach (var vol in status.Volumes)
        {
            Console.WriteLine($"  Volume {vol.DriveLetter} -> Atual: {(vol.CurrentProtected ? "Protegido" : "Desprotegido")} | Próximo: {(vol.NextProtected ? "Protegido" : "Desprotegido")}");
        }

        Console.WriteLine("----------------------------------------------------");
        Console.WriteLine($"Exclusões de Arquivo ({status.FileExclusions.Count}):");
        foreach (var f in status.FileExclusions.Take(5))
        {
            Console.WriteLine($"  - {f}");
        }
        if (status.FileExclusions.Count > 5)
        {
            Console.WriteLine($"  ... e mais {status.FileExclusions.Count - 5} exclusões.");
        }

        Console.WriteLine($"Exclusões de Registro ({status.RegistryExclusions.Count}):");
        foreach (var r in status.RegistryExclusions.Take(5))
        {
            Console.WriteLine($"  - {r}");
        }
        if (status.RegistryExclusions.Count > 5)
        {
            Console.WriteLine($"  ... e mais {status.RegistryExclusions.Count - 5} exclusões.");
        }
        Console.WriteLine("====================================================");
        return 0;
    }

    static int HandleFreeze(Dejavu.Core.Services.IUwfController uwf, string[] args)
    {
        string volume = "C:";
        if (args.Length > 1 && !args[1].StartsWith("-"))
        {
            volume = args[1];
        }

        uwf.Freeze(volume);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[OK] Sistema configurado para CONGELADO (Frozen) no volume {volume} a partir do próximo boot.");
        Console.ResetColor();

        if (args.Any(a => a.Equals("--restart", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Reiniciando o sistema agora...");
            uwf.RestartSystem();
        }
        return 0;
    }

    static int HandleThaw(Dejavu.Core.Services.IUwfController uwf, Dejavu.Core.Services.ThawOnceService thawOnce, string[] args)
    {
        bool once = args.Any(a => a.Equals("--once", StringComparison.OrdinalIgnoreCase));

        uwf.Thaw();

        if (once)
        {
            thawOnce.ArmThawOnce();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("[OK] Modo Thaw-Once ATIVADO: O sistema estará DESCONGELADO no próximo boot e será recongelado automaticamente no boot seguinte.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[OK] Sistema configurado para DESCONGELADO (Thawed) permanente a partir do próximo boot.");
            Console.ResetColor();
        }

        if (args.Any(a => a.Equals("--restart", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Reiniciando o sistema agora...");
            uwf.RestartSystem();
        }
        return 0;
    }

    static int HandleConfigOverlay(Dejavu.Core.Services.IUwfController uwf, string[] args)
    {
        OverlayType type = OverlayType.RAM;
        uint maxMb = 4096;
        uint warnMb = 3072;
        uint critMb = 3840;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--type", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                type = args[++i].Equals("disk", StringComparison.OrdinalIgnoreCase) ? OverlayType.Disk : OverlayType.RAM;
            }
            else if (args[i].Equals("--max-mb", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                maxMb = uint.Parse(args[++i]);
            }
            else if (args[i].Equals("--warn-mb", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                warnMb = uint.Parse(args[++i]);
            }
            else if (args[i].Equals("--crit-mb", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                critMb = uint.Parse(args[++i]);
            }
        }

        uwf.ConfigureOverlay(type, maxMb, warnMb, critMb);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] Overlay configurado: Tipo={type}, Max={maxMb}MB, Warning={warnMb}MB, Critical={critMb}MB");
        Console.ResetColor();
        return 0;
    }

    static int HandleApplyProfile(Dejavu.Core.Services.IUwfController uwf, Dejavu.Core.Services.ProfileService profileService, string[] args)
    {
        if (args.Length < 2)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Uso: dejavu apply-profile <caminho-para-exclusions.json>");
            Console.ResetColor();
            return 1;
        }

        string jsonPath = args[1];
        var profile = profileService.LoadProfile(jsonPath);
        profileService.ApplyProfile(uwf, profile);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] Perfil '{profile.Description}' (v{profile.Version}) aplicado com sucesso.");
        Console.ResetColor();
        return 0;
    }

    static int HandleSetupDomain(Dejavu.Core.Services.DomainService domainService)
    {
        domainService.ConfigureDomainTrustProtection();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[OK] Proteção de relação de confiança com o Domínio AD configurada (DisablePasswordChange=1).");
        Console.ResetColor();
        return 0;
    }

    static int HandleSetupThawSpace(Dejavu.Core.Services.IUwfController uwf, Dejavu.Core.Services.ThawSpaceService thawSpaceService, string[] args)
    {
        string target = args.Length > 1 ? args[1] : @"C:\Persistente";
        thawSpaceService.EnsureDirectoryThawSpace(uwf, target);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] Thaw Space configurado com exclusão permanente em: {target}");
        Console.ResetColor();
        return 0;
    }

    static int HandleMaintenance(Dejavu.Core.Services.MaintenanceService maintenanceService, string[] args)
    {
        if (args.Length > 1 && args[1].Equals("--run", StringComparison.OrdinalIgnoreCase))
        {
            maintenanceService.ExecuteMaintenanceRoutine();
            return 0;
        }

        maintenanceService.ScheduleMaintenanceWindow();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("[OK] Janela de manutenção agendada! O sistema entrará em Thaw no próximo boot e executará as atualizações antes de recongelar.");
        Console.ResetColor();
        return 0;
    }

    static int HandleCheckOverlay(Dejavu.Core.Services.OverlayMonitorService overlayMonitor)
    {
        overlayMonitor.CheckOverlayAndEnforcePolicy();
        return 0;
    }

    static int HandleFinalizeThawOnce(Dejavu.Core.Services.IUwfController uwf, Dejavu.Core.Services.ThawOnceService thawOnce, Dejavu.Core.Services.LoggerService logger)
    {
        if (thawOnce.IsThawOnceActive())
        {
            logger.Log(Dejavu.Core.Services.LogLevel.Info, "[Thaw-Once] Detectada inicialização descongelada. Programando recongelamento automático para o próximo boot...");
            uwf.Freeze("C:");
            thawOnce.ClearThawOnce();
            logger.Log(Dejavu.Core.Services.LogLevel.Info, "[Thaw-Once] Recongelamento configurado com sucesso.");
        }
        return 0;
    }

    static int HandleServe(Dejavu.Core.Services.ApiServerService apiServer, string[] args)
    {
        int port = 7777;
        string token = "";

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                port = int.Parse(args[++i]);
            }
            else if (args[i].Equals("--token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                token = args[++i];
            }
        }

        Console.WriteLine($"Iniciando agente HTTP Dejavu na porta {port}... Pressione Ctrl+C para encerrar.");
        apiServer.Start(port, token);
        return 0;
    }

    static async Task<int> HandleCluster(Dejavu.Core.Services.ClusterManagerService clusterManager, string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Uso: dejavu cluster <status|freeze|thaw|restart> --hosts <arquivo> [opções]");
            return 1;
        }

        string subAction = args[1].ToLowerInvariant();
        string hostsFile = "";
        int port = 7777;
        string token = "";
        string htmlPath = "";
        string csvPath = "";
        bool restart = false;
        bool once = false;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].Equals("--hosts", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                hostsFile = args[++i];
            else if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                port = int.Parse(args[++i]);
            else if (args[i].Equals("--token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                token = args[++i];
            else if (args[i].Equals("--html", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                htmlPath = args[++i];
            else if (args[i].Equals("--csv", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                csvPath = args[++i];
            else if (args[i].Equals("--restart", StringComparison.OrdinalIgnoreCase))
                restart = true;
            else if (args[i].Equals("--once", StringComparison.OrdinalIgnoreCase))
                once = true;
        }

        if (string.IsNullOrEmpty(hostsFile) || !File.Exists(hostsFile))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Arquivo de hosts inválido ou não encontrado: {hostsFile}");
            Console.ResetColor();
            return 1;
        }

        var hosts = (await File.ReadAllLinesAsync(hostsFile))
            .Select(h => h.Trim())
            .Where(h => !string.IsNullOrEmpty(h) && !h.StartsWith("#"))
            .ToList();

        Console.WriteLine($"Disparando ação '{subAction}' para {hosts.Count} estações em paralelo...");

        string endpoint = subAction switch
        {
            "status" => "/api/v1/status",
            "freeze" => $"/api/v1/freeze?restart={restart}",
            "thaw" => $"/api/v1/thaw?once={once}&restart={restart}",
            "restart" => "/api/v1/restart",
            _ => throw new ArgumentException($"Subcomando de cluster desconhecido: {subAction}")
        };

        var method = subAction == "status" ? HttpMethod.Get : HttpMethod.Post;
        var results = await clusterManager.ExecuteActionAsync(hosts, port, token, endpoint, method);

        int successCount = results.Count(r => r.Success);
        Console.ForegroundColor = successCount == hosts.Count ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"[Resultado] {successCount}/{hosts.Count} máquinas responderam com sucesso.");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(htmlPath))
        {
            string html = Dejavu.Core.Services.ClusterManagerService.GenerateHtmlReport(results);
            await File.WriteAllTextAsync(htmlPath, html);
            Console.WriteLine($"Relatório HTML gravado em: {htmlPath}");
        }

        if (!string.IsNullOrEmpty(csvPath))
        {
            string csv = Dejavu.Core.Services.ClusterManagerService.GenerateCsvReport(results);
            await File.WriteAllTextAsync(csvPath, csv);
            Console.WriteLine($"Relatório CSV gravado em: {csvPath}");
        }

        return successCount == hosts.Count ? 0 : 1;
    }

    static void PrintHelp()
    {
        Console.WriteLine("Dejavu IFPR — Equivalente Open Source ao Deep Freeze (IFPR / REA)");
        Console.WriteLine("Uso: dejavu <comando> [opções]");
        Console.WriteLine();
        Console.WriteLine("Comandos:");
        Console.WriteLine("  status [--json]                        Exibe o estado de congelamento, volumes e overlay");
        Console.WriteLine("  freeze [volume] [--restart]            Congela o sistema no próximo boot (padrão: C:)");
        Console.WriteLine("  thaw [--once] [--restart]              Descongela o sistema. --once re-congela no boot seguinte");
        Console.WriteLine("  config-overlay [opções]                Ajusta tamanho e limites de aviso do overlay");
        Console.WriteLine("     --type <ram|disk>                    Tipo do cache de overlay");
        Console.WriteLine("     --max-mb <MB>                        Tamanho máximo do overlay (ex: 4096)");
        Console.WriteLine("     --warn-mb <MB>                       Alerta de limite (ex: 3072)");
        Console.WriteLine("     --crit-mb <MB>                       Limite crítico (ex: 3840)");
        Console.WriteLine("  apply-profile <arquivo.json>           Aplica perfil versionado de exclusões");
        Console.WriteLine("  setup-domain                           Configura registro para manter relação de confiança com AD");
        Console.WriteLine("  setup-thawspace [pasta]                Cria e exclui pasta persistente (padrão: C:\\Persistente)");
        Console.WriteLine("  maintenance [--run]                    Agenda janela de manutenção (--run executa a rotina)");
        Console.WriteLine("  check-overlay                          Verifica uso de overlay e reinicia se atingir nível crítico");
        Console.WriteLine("  serve [--port 7777] [--token <segredo>] Inicia o serviço de API REST local autenticado");
        Console.WriteLine("  cluster <ação> --hosts <txt> [opts] Executa ações em lote (status|freeze|thaw|restart)");
    }
}
