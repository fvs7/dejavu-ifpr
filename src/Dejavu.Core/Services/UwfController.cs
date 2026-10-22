using System.Management;
using System.Runtime.Versioning;
using Dejavu.Core.Models;

namespace Dejavu.Core.Services;

/// <summary>
/// Controle do Unified Write Filter via provedor WMI root\standardcimv2\embedded.
/// Classes usadas: UWF_Filter, UWF_Volume, UWF_Overlay, UWF_OverlayConfig e UWF_RegistryFilter.
/// Configurações que valem a partir do próximo boot são feitas nas instâncias com CurrentSession = FALSE.
/// </summary>
[SupportedOSPlatform("windows")]
public class UwfController : IUwfController
{
    private const string WmiNamespace = @"\\localhost\root\standardcimv2\embedded";
    private readonly LoggerService _logger;
    private readonly ThawOnceService _thawOnceService;

    public UwfController(LoggerService logger, ThawOnceService thawOnceService)
    {
        _logger = logger;
        _thawOnceService = thawOnceService;
    }

    private ManagementScope GetScope()
    {
        var scope = new ManagementScope(WmiNamespace);
        scope.Connect();
        return scope;
    }

    private static List<ManagementObject> Query(ManagementScope scope, string wql)
    {
        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(wql));
        return searcher.Get().Cast<ManagementObject>().ToList();
    }

    private static ManagementObject GetSingle(ManagementScope scope, string wql, string description)
    {
        return Query(scope, wql).FirstOrDefault()
            ?? throw new InvalidOperationException($"Instância WMI não encontrada: {description}. O UWF está instalado?");
    }

    private static ManagementObject GetFilter(ManagementScope scope) =>
        GetSingle(scope, "SELECT * FROM UWF_Filter", "UWF_Filter");

    private static ManagementObject GetNextRegistryFilter(ManagementScope scope) =>
        GetSingle(scope, "SELECT * FROM UWF_RegistryFilter WHERE CurrentSession = FALSE", "UWF_RegistryFilter (próxima sessão)");

    private ManagementObject GetOrCreateNextVolume(ManagementScope scope, string volume)
    {
        string drive = UwfPaths.NormalizeVolume(volume);
        string wql = $"SELECT * FROM UWF_Volume WHERE CurrentSession = FALSE AND DriveLetter = '{drive}'";
        var existing = Query(scope, wql).FirstOrDefault();
        if (existing != null) return existing;

        // Volume ainda sem configuração UWF para a próxima sessão: cria a instância.
        _logger.Log(LogLevel.Info, $"Criando configuração UWF_Volume para {drive} na próxima sessão.");
        using (var volumeClass = new ManagementClass(scope, new ManagementPath("UWF_Volume"), null))
        using (var instance = volumeClass.CreateInstance())
        {
            instance["DriveLetter"] = drive;
            instance["CurrentSession"] = false;
            instance.Put();
        }
        return GetSingle(scope, wql, $"UWF_Volume {drive} (próxima sessão)");
    }

    /// <summary>
    /// Invoca um método de instância e lança exceção se o ReturnValue indicar erro.
    /// </summary>
    private static ManagementBaseObject? Invoke(ManagementObject obj, string method, IDictionary<string, object>? parameters = null)
    {
        ManagementBaseObject? inParams = null;
        if (parameters != null)
        {
            inParams = obj.GetMethodParameters(method);
            foreach (var (name, value) in parameters)
            {
                inParams[name] = value;
            }
        }

        var outParams = obj.InvokeMethod(method, inParams, null);
        if (outParams?["ReturnValue"] is { } rv && Convert.ToUInt32(rv) != 0)
        {
            throw new InvalidOperationException($"{obj.ClassPath.ClassName}.{method} retornou o código {Convert.ToUInt32(rv)}.");
        }
        return outParams;
    }

    public bool IsUwfInstalled()
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return false;
            return Query(GetScope(), "SELECT * FROM UWF_Filter").Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public UwfStatus GetStatus()
    {
        var status = new UwfStatus();
        if (!OperatingSystem.IsWindows())
        {
            status.FilterInstalled = false;
            return status;
        }

        try
        {
            var scope = GetScope();
            status.ThawOncePending = _thawOnceService.IsThawOnceActive();

            // 1. UWF_Filter
            var filter = GetFilter(scope);
            status.FilterInstalled = true;
            status.CurrentEnabled = Convert.ToBoolean(filter["CurrentEnabled"]);
            status.NextEnabled = Convert.ToBoolean(filter["NextEnabled"]);

            // 2. UWF_Volume: uma instância por volume e por sessão (atual / próxima)
            var nextVolumes = new List<ManagementObject>();
            foreach (var obj in Query(scope, "SELECT * FROM UWF_Volume"))
            {
                string drive = obj["DriveLetter"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(drive))
                {
                    drive = obj["VolumeName"]?.ToString() ?? "Desconhecido";
                }

                var vol = status.Volumes.FirstOrDefault(v => v.DriveLetter.Equals(drive, StringComparison.OrdinalIgnoreCase));
                if (vol == null)
                {
                    vol = new VolumeStatus { DriveLetter = drive };
                    status.Volumes.Add(vol);
                }

                bool isProtected = Convert.ToBoolean(obj["Protected"]);
                if (Convert.ToBoolean(obj["CurrentSession"]))
                {
                    vol.CurrentProtected = isProtected;
                }
                else
                {
                    vol.NextProtected = isProtected;
                    nextVolumes.Add(obj);
                }
            }

            // 3. UWF_OverlayConfig (tipo e tamanho máximo da sessão atual)
            var overlayConfig = Query(scope, "SELECT * FROM UWF_OverlayConfig WHERE CurrentSession = TRUE").FirstOrDefault();
            if (overlayConfig != null)
            {
                status.Overlay.Type = (OverlayType)Convert.ToInt32(overlayConfig["Type"]);
                status.Overlay.MaximumSizeMb = Convert.ToUInt32(overlayConfig["MaximumSize"]);
            }

            // 4. UWF_Overlay (consumo atual e limites de alerta)
            var overlay = Query(scope, "SELECT * FROM UWF_Overlay").FirstOrDefault();
            if (overlay != null)
            {
                status.Overlay.CurrentUsageMb = Convert.ToUInt32(overlay["OverlayConsumption"]);
                status.Overlay.WarningThresholdMb = Convert.ToUInt32(overlay["WarningOverlayThreshold"]);
                status.Overlay.CriticalThresholdMb = Convert.ToUInt32(overlay["CriticalOverlayThreshold"]);
            }

            // 5. Exclusões de arquivo (configuração da próxima sessão, por volume)
            foreach (var vol in nextVolumes)
            {
                try
                {
                    string drive = vol["DriveLetter"]?.ToString() ?? string.Empty;
                    var outParams = Invoke(vol, "GetExclusions");
                    if (outParams?["ExcludedFiles"] is ManagementBaseObject[] files)
                    {
                        status.FileExclusions.AddRange(files
                            .Select(f => f["FileName"]?.ToString())
                            .Where(f => !string.IsNullOrEmpty(f))
                            .Select(f => string.IsNullOrEmpty(drive) ? f! : UwfPaths.ToFullPath(drive, f!)));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, "Falha ao listar exclusões de arquivo do UWF", ex);
                }
            }

            // 6. Exclusões de registro (configuração da próxima sessão)
            try
            {
                var outParams = Invoke(GetNextRegistryFilter(scope), "GetExclusions");
                if (outParams?["ExcludedKeys"] is ManagementBaseObject[] keys)
                {
                    status.RegistryExclusions.AddRange(keys
                        .Select(k => k["RegistryKey"]?.ToString())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .Select(k => k!));
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, "Falha ao listar exclusões de registro do UWF", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "Erro ao consultar status do UWF", ex);
            status.FilterInstalled = false;
        }

        return status;
    }

    public void Freeze(string volume = "C:")
    {
        var scope = GetScope();
        _logger.Log(LogLevel.Info, $"Solicitando congelamento (Freeze) do sistema para o volume {volume}...");

        // 1. Ativar o filtro para o próximo boot
        Invoke(GetFilter(scope), "Enable");

        // 2. Proteger o volume especificado a partir do próximo boot
        Invoke(GetOrCreateNextVolume(scope, volume), "Protect");

        // Limpa qualquer flag de Thaw-Once pendente
        _thawOnceService.ClearThawOnce();

        _logger.Log(LogLevel.Info, "Sistema configurado para CONGELADO (Frozen) no próximo boot.");
    }

    public void Thaw()
    {
        var scope = GetScope();
        _logger.Log(LogLevel.Info, "Solicitando descongelamento (Thaw) do sistema...");

        Invoke(GetFilter(scope), "Disable");

        _thawOnceService.ClearThawOnce();
        _logger.Log(LogLevel.Info, "Sistema configurado para DESCONGELADO (Thawed) no próximo boot.");
    }

    public void ConfigureOverlay(OverlayType type, uint maxSizeMb, uint warningMb, uint criticalMb)
    {
        var scope = GetScope();
        _logger.Log(LogLevel.Info, $"Configurando overlay UWF: Tipo={type}, Max={maxSizeMb}MB, Warning={warningMb}MB, Critical={criticalMb}MB");

        // Tipo e tamanho máximo valem a partir do próximo boot (UWF_OverlayConfig da próxima sessão).
        var nextConfig = GetSingle(scope, "SELECT * FROM UWF_OverlayConfig WHERE CurrentSession = FALSE", "UWF_OverlayConfig (próxima sessão)");
        Invoke(nextConfig, "SetType", new Dictionary<string, object> { ["type"] = (uint)type });
        Invoke(nextConfig, "SetMaximumSize", new Dictionary<string, object> { ["size"] = maxSizeMb });

        // Limites de alerta são definidos em UWF_Overlay.
        var overlay = GetSingle(scope, "SELECT * FROM UWF_Overlay", "UWF_Overlay");
        Invoke(overlay, "SetWarningThreshold", new Dictionary<string, object> { ["size"] = warningMb });
        Invoke(overlay, "SetCriticalThreshold", new Dictionary<string, object> { ["size"] = criticalMb });
    }

    public void AddFileExclusion(string volume, string path)
    {
        var scope = GetScope();
        string relative = UwfPaths.ToVolumeRelative(volume, path);
        Invoke(GetOrCreateNextVolume(scope, volume), "AddExclusion", new Dictionary<string, object> { ["FileName"] = relative });
        _logger.Log(LogLevel.Info, $"Exclusão de arquivo adicionada: [{UwfPaths.NormalizeVolume(volume)}] {relative}");
    }

    public void RemoveFileExclusion(string volume, string path)
    {
        var scope = GetScope();
        string relative = UwfPaths.ToVolumeRelative(volume, path);
        Invoke(GetOrCreateNextVolume(scope, volume), "RemoveExclusion", new Dictionary<string, object> { ["FileName"] = relative });
        _logger.Log(LogLevel.Info, $"Exclusão de arquivo removida: [{UwfPaths.NormalizeVolume(volume)}] {relative}");
    }

    public void AddRegistryExclusion(string key)
    {
        Invoke(GetNextRegistryFilter(GetScope()), "AddExclusion", new Dictionary<string, object> { ["RegistryKey"] = key });
        _logger.Log(LogLevel.Info, $"Exclusão de registro adicionada: {key}");
    }

    public void RemoveRegistryExclusion(string key)
    {
        Invoke(GetNextRegistryFilter(GetScope()), "RemoveExclusion", new Dictionary<string, object> { ["RegistryKey"] = key });
        _logger.Log(LogLevel.Info, $"Exclusão de registro removida: {key}");
    }

    public void RestartSystem()
    {
        Invoke(GetFilter(GetScope()), "RestartSystem");
    }
}
