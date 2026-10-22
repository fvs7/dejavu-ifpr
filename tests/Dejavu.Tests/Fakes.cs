using Dejavu.Core.Infrastructure;

namespace Dejavu.Tests;

public class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, List<string> Args)> Calls { get; } = new();
    public Func<string, IReadOnlyList<string>, int> ExitCode { get; set; } = (_, _) => 0;

    public int Run(string fileName, IEnumerable<string> arguments, TimeSpan? timeout = null)
    {
        var args = arguments.ToList();
        Calls.Add((fileName, args));
        return ExitCode(fileName, args);
    }

    public IEnumerable<List<string>> SchtasksCalls =>
        Calls.Where(c => c.FileName == "schtasks.exe").Select(c => c.Args);
}

public class InMemoryStateStore : IStateStore
{
    public Dictionary<string, string> Values { get; } = new();

    public string? Get(string name) => Values.TryGetValue(name, out var v) ? v : null;

    public void Set(string name, string value) => Values[name] = value;
}
