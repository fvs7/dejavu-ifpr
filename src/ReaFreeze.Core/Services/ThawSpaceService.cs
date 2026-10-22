using System.Diagnostics;
using System.IO;

namespace ReaFreeze.Core.Services;

public class ThawSpaceService
{
    private readonly LoggerService _logger;

    public ThawSpaceService(LoggerService logger)
    {
        _logger = logger;
    }

    public void EnsureDirectoryThawSpace(IUwfController uwf, string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                _logger.Log(LogLevel.Info, $"[ThawSpace] Diretório criado: {directoryPath}");
            }

            // Determina a letra da unidade (ex: "C:")
            string root = Path.GetPathRoot(directoryPath) ?? @"C:\";
            string drive = root.TrimEnd('\\');

            uwf.AddFileExclusion(drive, directoryPath);
            _logger.Log(LogLevel.Info, $"[ThawSpace] Exclusão permanente configurada para a pasta persistente: {directoryPath}");
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"[ThawSpace] Falha ao configurar Thaw Space na pasta '{directoryPath}'", ex);
            throw;
        }
    }
}
