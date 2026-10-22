using System.Management;
using System.Runtime.Versioning;
using ReaFreeze.Core.Models;

namespace ReaFreeze.Core.Services;

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

    public bool IsUwfInstalled()
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return false;
            var scope = GetScope();
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM UWF_Filter"));
            using var collection = searcher.Get();
            return collection.Count > 0;
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
            status.FilterInstalled = true;
            status.ThawOncePending = _thawOnceService.IsThawOnceActive();

            // 1. UWF_Filter
            using (var filterSearcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM UWF_Filter")))
            {
                foreach (ManagementObject obj in filterSearcher.Get())
                {
                    status.CurrentEnabled = Convert.ToBoolean(obj["CurrentEnabled"]);
                    status.NextEnabled = Convert.ToBoolean(obj["NextEnabled"]);
                    break;
                }
            }

            // 2. UWF_Volume
            using (var volumeSearcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM UWF_Volume")))
            {
                foreach (ManagementObject obj in volumeSearcher.Get())
                {
                    string drive = obj["DriveLetter"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(drive))
                    {
                        drive = obj["VolumeName"]?.ToString() ?? "Desconhecido";
                    }
                    status.Volumes.Add(new VolumeStatus
                    {
                        DriveLetter = drive,
                        CurrentProtected = Convert.ToBoolean(obj["CurrentProtected"]),
                        NextProtected = Convert.ToBoolean(obj["NextProtected"])
                    });
                }
            }

            // 3. UWF_Overlay
            using (var overlaySearcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM UWF_Overlay")))
            {
                foreach (ManagementObject obj in overlaySearcher.Get())
                {
                    status.Overlay.Type = (OverlayType)Convert.ToInt32(obj["OverlayType"]);
                    status.Overlay.MaximumSizeMb = Convert.ToUInt32(obj["MaximumSize"]);
                    status.Overlay.WarningThresholdMb = Convert.ToUInt32(obj["WarningThreshold"]);
                    status.Overlay.CriticalThresholdMb = Convert.ToUInt32(obj["CriticalThreshold"]);
                    break;
                }
            }

            // Consumo de Overlay
            using (var overlayConfigSearcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM UWF_OverlayConfig")))
            {
                foreach (ManagementObject obj in overlayConfigSearcher.Get())
                {
                    if (obj["OverlaySize"] != null)
                    {
                        status.Overlay.CurrentUsageMb = Convert.ToUInt32(obj["OverlaySize"]);
                    }
                    break;
                }
            }

            // 4. File Exclusions (Volume C:)
            using (var fileFilter = new ManagementClass(scope, new ManagementPath("UWF_FileFilter"), null))
            {
                try
                {
                    var inParams = fileFilter.GetMethodParameters("GetExclusions");
                    inParams["Volume"] = "C:";
                    var outParams = fileFilter.InvokeMethod("GetExclusions", inParams, null);
                    if (outParams != null && outParams["ExcludedFileList"] is string[] files)
                    {
                        status.FileExclusions.AddRange(files);
                    }
                }
                catch
                {
                    // Sem exclusões ou método indisponível
                }
            }

            // 5. Registry Exclusions
            using (var regFilter = new ManagementClass(scope, new ManagementPath("UWF_RegistryFilter"), null))
            {
                try
                {
                    var outParams = regFilter.InvokeMethod("GetExclusions", null, null);
                    if (outParams != null && outParams["ExcludedKeyList"] is string[] keys)
                    {
                        status.RegistryExclusions.AddRange(keys);
                    }
                }
                catch
                {
                    // Sem exclusões ou método indisponível
                }
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
        using (var filterClass = new ManagementClass(scope, new ManagementPath("UWF_Filter"), null))
        {
            filterClass.InvokeMethod("Enable", null, null);
        }

        // 2. Proteger o volume especificado
        using (var volumeSearcher = new ManagementObjectSearcher(scope, new ObjectQuery($"SELECT * FROM UWF_Volume WHERE DriveLetter = '{volume}'")))
        {
            foreach (ManagementObject vol in volumeSearcher.Get())
            {
                vol.InvokeMethod("Protect", null, null);
            }
        }

        // Limpa qualquer flag de Thaw-Once pendente
        _thawOnceService.ClearThawOnce();

        _logger.Log(LogLevel.Info, "Sistema configurado para CONGELADO (Frozen) no próximo boot.");
    }

    public void Thaw()
    {
        var scope = GetScope();
        _logger.Log(LogLevel.Info, "Solicitando descongelamento (Thaw) do sistema...");

        using (var filterClass = new ManagementClass(scope, new ManagementPath("UWF_Filter"), null))
        {
            filterClass.InvokeMethod("Disable", null, null);
        }

        _thawOnceService.ClearThawOnce();
        _logger.Log(LogLevel.Info, "Sistema configurado para DESCONGELADO (Thawed) no próximo boot.");
    }

    public void ConfigureOverlay(OverlayType type, uint maxSizeMb, uint warningMb, uint criticalMb)
    {
        var scope = GetScope();
        _logger.Log(LogLevel.Info, $"Configurando overlay UWF: Tipo={type}, Max={maxSizeMb}MB, Warning={warningMb}MB, Critical={criticalMb}MB");

        using (var overlayClass = new ManagementClass(scope, new ManagementPath("UWF_Overlay"), null))
        {
            var setTypeParams = overlayClass.GetMethodParameters("SetType");
            setTypeParams["type"] = (uint)type;
            overlayClass.InvokeMethod("SetType", setTypeParams, null);

            var setSizeParams = overlayClass.GetMethodParameters("SetMaximumSize");
            setSizeParams["size"] = maxSizeMb;
            overlayClass.InvokeMethod("SetMaximumSize", setSizeParams, null);

            var setWarnParams = overlayClass.GetMethodParameters("SetWarningThreshold");
            setWarnParams["size"] = warningMb;
            overlayClass.InvokeMethod("SetWarningThreshold", setWarnParams, null);

            var setCritParams = overlayClass.GetMethodParameters("SetCriticalThreshold");
            setCritParams["size"] = criticalMb;
            overlayClass.InvokeMethod("SetCriticalThreshold", setCritParams, null);
        }
    }

    public void AddFileExclusion(string volume, string path)
    {
        var scope = GetScope();
        using (var fileFilter = new ManagementClass(scope, new ManagementPath("UWF_FileFilter"), null))
        {
            var inParams = fileFilter.GetMethodParameters("AddExclusion");
            inParams["Volume"] = volume;
            inParams["FileName"] = path;
            fileFilter.InvokeMethod("AddExclusion", inParams, null);
        }
        _logger.Log(LogLevel.Info, $"Exclusão de arquivo adicionada: [{volume}] {path}");
    }

    public void RemoveFileExclusion(string volume, string path)
    {
        var scope = GetScope();
        using (var fileFilter = new ManagementClass(scope, new ManagementPath("UWF_FileFilter"), null))
        {
            var inParams = fileFilter.GetMethodParameters("RemoveExclusion");
            inParams["Volume"] = volume;
            inParams["FileName"] = path;
            fileFilter.InvokeMethod("RemoveExclusion", inParams, null);
        }
        _logger.Log(LogLevel.Info, $"Exclusão de arquivo removida: [{volume}] {path}");
    }

    public void AddRegistryExclusion(string key)
    {
        var scope = GetScope();
        using (var regFilter = new ManagementClass(scope, new ManagementPath("UWF_RegistryFilter"), null))
        {
            var inParams = regFilter.GetMethodParameters("AddExclusion");
            inParams["RegistryKey"] = key;
            regFilter.InvokeMethod("AddExclusion", inParams, null);
        }
        _logger.Log(LogLevel.Info, $"Exclusão de registro adicionada: {key}");
    }

    public void RemoveRegistryExclusion(string key)
    {
        var scope = GetScope();
        using (var regFilter = new ManagementClass(scope, new ManagementPath("UWF_RegistryFilter"), null))
        {
            var inParams = regFilter.GetMethodParameters("RemoveExclusion");
            inParams["RegistryKey"] = key;
            regFilter.InvokeMethod("RemoveExclusion", inParams, null);
        }
        _logger.Log(LogLevel.Info, $"Exclusão de registro removida: {key}");
    }

    public void RestartSystem()
    {
        var scope = GetScope();
        using (var filterClass = new ManagementClass(scope, new ManagementPath("UWF_Filter"), null))
        {
            filterClass.InvokeMethod("RestartSystem", null, null);
        }
    }
}
