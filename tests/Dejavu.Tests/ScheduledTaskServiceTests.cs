using Dejavu.Core.Services;
using Xunit;

namespace Dejavu.Tests;

public class ScheduledTaskServiceTests
{
    private const string ExePath = @"C:\Program Files\Dejavu\dejavu.exe";

    [Fact]
    public void Should_Quote_Executable_Path_With_Spaces_In_Task_Command()
    {
        Assert.Equal("\"C:\\Program Files\\Dejavu\\dejavu.exe\" --finalize-thaw-once",
            ScheduledTaskService.BuildTaskCommand(ExePath, "--finalize-thaw-once"));
    }

    [Fact]
    public void Should_Create_OnStart_Task_As_System()
    {
        var runner = new FakeProcessRunner();
        var tasks = new ScheduledTaskService(runner, new LoggerService(), ExePath);

        tasks.CreateOnStartTask("Teste", "maintenance --run");

        var args = Assert.Single(runner.SchtasksCalls);
        Assert.Equal(new[]
        {
            "/Create", "/F", "/TN", "Teste",
            "/TR", "\"C:\\Program Files\\Dejavu\\dejavu.exe\" maintenance --run",
            "/SC", "ONSTART", "/RU", "SYSTEM", "/RL", "HIGHEST"
        }, args);
    }

    [Fact]
    public void Should_Create_Recurring_Task_With_Interval()
    {
        var runner = new FakeProcessRunner();
        var tasks = new ScheduledTaskService(runner, new LoggerService(), ExePath);

        tasks.CreateRecurringTask(ScheduledTaskService.OverlayMonitorTask, "check-overlay", 5);

        var args = Assert.Single(runner.SchtasksCalls);
        Assert.Contains("MINUTE", args);
        Assert.Equal("5", args[args.IndexOf("/MO") + 1]);
    }

    [Fact]
    public void Should_Throw_When_Schtasks_Fails()
    {
        var runner = new FakeProcessRunner { ExitCode = (_, _) => 1 };
        var tasks = new ScheduledTaskService(runner, new LoggerService(), ExePath);

        Assert.Throws<InvalidOperationException>(() => tasks.CreateOnStartTask("Teste", "status"));
    }

    [Fact]
    public void Should_Ignore_Failure_When_Deleting_Missing_Task()
    {
        var runner = new FakeProcessRunner { ExitCode = (_, _) => 1 };
        var tasks = new ScheduledTaskService(runner, new LoggerService(), ExePath);

        tasks.DeleteTask("Inexistente");

        Assert.Equal(new[] { "/Delete", "/F", "/TN", "Inexistente" }, Assert.Single(runner.SchtasksCalls));
    }

    [Fact]
    public void Should_Arm_ThawOnce_With_Startup_Task()
    {
        var runner = new FakeProcessRunner();
        var thawOnce = new ThawOnceService(new LoggerService(), new ScheduledTaskService(runner, new LoggerService(), ExePath));

        thawOnce.ArmThawOnce();

        var args = Assert.Single(runner.SchtasksCalls);
        Assert.Equal(ScheduledTaskService.ThawOnceTask, args[args.IndexOf("/TN") + 1]);
        Assert.EndsWith("--finalize-thaw-once", args[args.IndexOf("/TR") + 1]);
    }
}
