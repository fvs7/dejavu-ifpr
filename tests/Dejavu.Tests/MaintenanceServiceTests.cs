using Dejavu.Core.Services;
using Xunit;

namespace Dejavu.Tests;

public class MaintenanceServiceTests
{
    private readonly FakeProcessRunner _runner = new();
    private readonly InMemoryStateStore _state = new();
    private readonly MockUwfController _uwf = new();
    private readonly MaintenanceService _service;

    public MaintenanceServiceTests()
    {
        var logger = new LoggerService();
        _service = new MaintenanceService(logger, _uwf, new ScheduledTaskService(_runner, logger, @"C:\Dejavu\dejavu.exe"), _runner, _state);
    }

    private List<string> NonSchtasksCalls => _runner.Calls.Where(c => c.FileName != "schtasks.exe").Select(c => c.FileName).ToList();

    [Fact]
    public void Schedule_Should_Thaw_And_Create_Run_Task()
    {
        _service.ScheduleMaintenanceWindow();

        Assert.False(_uwf.NextEnabled);
        Assert.Equal(MaintenanceState.Scheduled, _service.GetMaintenanceState());
        var create = Assert.Single(_runner.SchtasksCalls);
        Assert.Equal(ScheduledTaskService.MaintenanceTask, create[create.IndexOf("/TN") + 1]);
        Assert.EndsWith("maintenance --run", create[create.IndexOf("/TR") + 1]);
    }

    [Fact]
    public void Schedule_Should_Roll_Back_When_Task_Creation_Fails()
    {
        _runner.ExitCode = (file, args) => file == "schtasks.exe" && args[0] == "/Create" ? 1 : 0;

        Assert.Throws<InvalidOperationException>(() => _service.ScheduleMaintenanceWindow());

        Assert.True(_uwf.NextEnabled);
        Assert.Equal(MaintenanceState.None, _service.GetMaintenanceState());
    }

    [Fact]
    public void Run_Should_Update_Then_Refreeze_And_Restart()
    {
        _state.Set("State", MaintenanceState.Scheduled);
        _uwf.CurrentEnabled = false;
        _uwf.NextEnabled = false;

        _service.ExecuteMaintenanceRoutine();

        Assert.Equal(3, NonSchtasksCalls.Count);
        Assert.Equal("gpupdate.exe", NonSchtasksCalls.Last());
        Assert.Equal(MaintenanceState.Completed, _service.GetMaintenanceState());
        Assert.True(_uwf.NextEnabled);
        Assert.Equal(1, _uwf.RestartCount);
        Assert.Contains(_runner.SchtasksCalls, a => a[0] == "/Delete" && a.Contains(ScheduledTaskService.MaintenanceTask));
    }

    [Fact]
    public void Run_Should_Continue_When_A_Step_Fails()
    {
        _state.Set("State", MaintenanceState.Scheduled);
        _uwf.CurrentEnabled = false;
        _runner.ExitCode = (file, _) => file == "powershell.exe" ? 1 : 0;

        _service.ExecuteMaintenanceRoutine();

        Assert.Equal(3, NonSchtasksCalls.Count);
        Assert.Equal(MaintenanceState.Completed, _service.GetMaintenanceState());
    }

    [Fact]
    public void Run_Should_Abort_And_Refreeze_On_Reboot_Loop()
    {
        _state.Set("State", MaintenanceState.Running);
        _uwf.CurrentEnabled = false;
        _uwf.NextEnabled = false;

        _service.ExecuteMaintenanceRoutine();

        Assert.Empty(NonSchtasksCalls);
        Assert.Equal(MaintenanceState.AbortedLoopDetected, _service.GetMaintenanceState());
        Assert.True(_uwf.NextEnabled);
        Assert.Equal(1, _uwf.RestartCount);
    }

    [Fact]
    public void Run_Should_Not_Update_When_System_Is_Still_Frozen()
    {
        _state.Set("State", MaintenanceState.Scheduled);
        _uwf.CurrentEnabled = true;

        _service.ExecuteMaintenanceRoutine();

        Assert.Empty(NonSchtasksCalls);
        Assert.Equal(MaintenanceState.FailedStillFrozen, _service.GetMaintenanceState());
        Assert.Equal(0, _uwf.RestartCount);
    }

    [Fact]
    public void Run_Without_Schedule_Should_Only_Remove_Orphan_Task()
    {
        _uwf.CurrentEnabled = false;

        _service.ExecuteMaintenanceRoutine();

        Assert.Empty(NonSchtasksCalls);
        Assert.Equal(0, _uwf.RestartCount);
        Assert.Contains(_runner.SchtasksCalls, a => a[0] == "/Delete");
    }
}
