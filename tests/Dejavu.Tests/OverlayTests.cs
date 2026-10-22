using Dejavu.Core.Models;
using Dejavu.Core.Services;
using Xunit;

namespace Dejavu.Tests;

public class OverlayTests
{
    private static (OverlayMonitorService Monitor, FakeProcessRunner Runner) CreateMonitor(MockUwfController uwf)
    {
        var runner = new FakeProcessRunner();
        return (new OverlayMonitorService(new LoggerService(), uwf, runner), runner);
    }

    private static MockUwfController WithUsage(uint usageMb, bool frozen = true) => new()
    {
        CurrentEnabled = frozen,
        Overlay = new OverlayConfig { MaximumSizeMb = 4096, WarningThresholdMb = 3072, CriticalThresholdMb = 3840, CurrentUsageMb = usageMb }
    };

    [Fact]
    public void Critical_Usage_Should_Schedule_Reboot()
    {
        var (monitor, runner) = CreateMonitor(WithUsage(3900));

        monitor.CheckOverlayAndEnforcePolicy();

        var call = Assert.Single(runner.Calls);
        Assert.Equal("shutdown.exe", call.FileName);
        Assert.Contains("/r", call.Args);
        Assert.Equal("60", call.Args[call.Args.IndexOf("/t") + 1]);
    }

    [Theory]
    [InlineData(1000u, true)]
    [InlineData(3200u, true)]
    [InlineData(3900u, false)]
    public void Should_Not_Reboot_Below_Critical_Or_When_Thawed(uint usage, bool frozen)
    {
        var (monitor, runner) = CreateMonitor(WithUsage(usage, frozen));

        monitor.CheckOverlayAndEnforcePolicy();

        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void Parse_Should_Use_Defaults_Without_Options()
    {
        Assert.True(OverlaySettings.TryParse(Array.Empty<string>(), out var s, out _));
        Assert.Equal(OverlayType.RAM, s.Type);
        Assert.Equal(4096u, s.MaximumSizeMb);
    }

    [Fact]
    public void Parse_Should_Read_All_Options()
    {
        Assert.True(OverlaySettings.TryParse(new[] { "--type", "disk", "--max-mb", "8192", "--warn-mb", "6000", "--crit-mb", "7500" }, out var s, out _));
        Assert.Equal(OverlayType.Disk, s.Type);
        Assert.Equal(8192u, s.MaximumSizeMb);
        Assert.Equal(6000u, s.WarningThresholdMb);
        Assert.Equal(7500u, s.CriticalThresholdMb);
    }

    [Theory]
    [InlineData("--max-mb", "abc")]
    [InlineData("--type", "ssd")]
    [InlineData("--warn-mb", "3900")]
    [InlineData("--crit-mb", "5000")]
    [InlineData("--max-mb", "512")]
    [InlineData("--tamanho", "10")]
    public void Parse_Should_Reject_Invalid_Values(string option, string value)
    {
        Assert.False(OverlaySettings.TryParse(new[] { option, value }, out _, out string error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Parse_Should_Reject_Option_Without_Value()
    {
        Assert.False(OverlaySettings.TryParse(new[] { "--max-mb" }, out _, out _));
    }

    [Theory]
    [InlineData("C:", @"C:\ProgramData\Veyon", @"\ProgramData\Veyon")]
    [InlineData("c:", @"c:\Persistente", @"\Persistente")]
    [InlineData("D:", @"\Dados", @"\Dados")]
    [InlineData(@"C:\", @"Users\Public", @"\Users\Public")]
    public void Should_Convert_Paths_To_Volume_Relative(string volume, string path, string expected)
    {
        Assert.Equal(expected, UwfPaths.ToVolumeRelative(volume, path));
    }

    [Fact]
    public void Should_Build_Full_Path_From_Uwf_Exclusion()
    {
        Assert.Equal(@"C:\ProgramData\Veyon", UwfPaths.ToFullPath("c:", @"\ProgramData\Veyon"));
    }
}
