using Microsoft.Win32;

namespace Dejavu.Core.Services;

public class DomainService
{
    private readonly LoggerService _logger;
    private const string NetlogonKeyPath = @"SYSTEM\CurrentControlSet\Services\Netlogon\Parameters";

    public DomainService(LoggerService logger)
    {
        _logger = logger;
    }

    public void ConfigureDomainTrustProtection()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.Log(LogLevel.Warning, "[Domínio] Configuração de registro disponível apenas no Windows.");
            return;
        }

        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(NetlogonKeyPath, writable: true);
            if (key != null)
            {
                // 1 = Desativa a rotação automática da senha de máquina pelo cliente,
                // impedindo a perda de relação de confiança com o Active Directory.
                key.SetValue("DisablePasswordChange", 1, RegistryValueKind.DWord);
                _logger.Log(LogLevel.Info, @"[Domínio] Chave Netlogon\DisablePasswordChange definida como 1 (proteção de confiança AD ativa).");
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, @"[Domínio] Falha ao configurar chave Netlogon\DisablePasswordChange no registro.", ex);
            throw;
        }
    }

    public bool IsDomainTrustProtectionEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(NetlogonKeyPath, writable: false);
            if (key != null)
            {
                var val = key.GetValue("DisablePasswordChange");
                if (val is int intVal && intVal == 1) return true;
            }
        }
        catch
        {
            // Ignora falhas de leitura e retorna falso
        }
        return false;
    }
}
