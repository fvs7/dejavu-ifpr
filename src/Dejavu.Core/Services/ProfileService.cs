using System.Text.Json;
using Dejavu.Core.Models;

namespace Dejavu.Core.Services;

public class ProfileService
{
    private readonly LoggerService _logger;

    public ProfileService(LoggerService logger)
    {
        _logger = logger;
    }

    public ExclusionProfile LoadProfile(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException($"Arquivo de perfil não encontrado: {jsonPath}");
        }

        string json = File.ReadAllText(jsonPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var profile = JsonSerializer.Deserialize<ExclusionProfile>(json, options);

        if (profile == null)
        {
            throw new InvalidOperationException("Falha ao desserializar o perfil de exclusões JSON.");
        }

        return profile;
    }

    public void ApplyProfile(IUwfController controller, ExclusionProfile profile, string defaultVolume = "C:")
    {
        _logger.Log(LogLevel.Info, $"Iniciando aplicação do perfil de exclusões v{profile.Version} ({profile.Description})...");
        var currentStatus = controller.GetStatus();

        // 1. Arquivos e Pastas
        foreach (var path in profile.FileAndFolderExclusions)
        {
            string trimmed = path.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Identifica o volume se informado explicitamente (ex: D:\ ou C:\)
            string volume = defaultVolume;
            string relativeOrFullPath = trimmed;
            if (trimmed.Length >= 2 && trimmed[1] == ':')
            {
                volume = trimmed.Substring(0, 2).ToUpper();
            }

            bool alreadyExcluded = currentStatus.FileExclusions.Any(e => string.Equals(e, trimmed, StringComparison.OrdinalIgnoreCase));
            if (!alreadyExcluded)
            {
                try
                {
                    controller.AddFileExclusion(volume, relativeOrFullPath);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Falha ao aplicar exclusão de arquivo: {trimmed}", ex);
                }
            }
        }

        // 2. Chaves de Registro
        foreach (var regKey in profile.RegistryExclusions)
        {
            string trimmed = regKey.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            bool alreadyExcluded = currentStatus.RegistryExclusions.Any(e => string.Equals(e, trimmed, StringComparison.OrdinalIgnoreCase));
            if (!alreadyExcluded)
            {
                try
                {
                    controller.AddRegistryExclusion(trimmed);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Falha ao aplicar exclusão de registro: {trimmed}", ex);
                }
            }
        }

        _logger.Log(LogLevel.Info, "Perfil de exclusões aplicado com sucesso.");
    }
}
