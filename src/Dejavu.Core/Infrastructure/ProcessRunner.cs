using System.Diagnostics;

namespace Dejavu.Core.Infrastructure;

public interface IProcessRunner
{
    /// <summary>
    /// Executa um processo e retorna o código de saída (-1 se estourar o tempo limite).
    /// </summary>
    int Run(string fileName, IEnumerable<string> arguments, TimeSpan? timeout = null);
}

public class ProcessRunner : IProcessRunner
{
    public int Run(string fileName, IEnumerable<string> arguments, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ArgumentList faz o escape correto de espaços e aspas em cada argumento.
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Não foi possível iniciar o processo: {fileName}");

        if (timeout is null)
        {
            proc.WaitForExit();
            return proc.ExitCode;
        }

        if (!proc.WaitForExit(timeout.Value))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return -1;
        }
        return proc.ExitCode;
    }
}
