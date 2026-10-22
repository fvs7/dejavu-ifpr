using Dejavu.Core.Models;
using Dejavu.Core.Services;
using Xunit;

namespace Dejavu.Tests;

public class ClusterReportTests
{
    [Fact]
    public void Should_Generate_Html_And_Csv_Reports()
    {
        var results = new List<ClusterNodeResult>
        {
            new ClusterNodeResult
            {
                Host = "lab3-pc01.acd.ifpr.local",
                Success = true,
                Status = new UwfStatus
                {
                    FilterInstalled = true,
                    CurrentEnabled = true,
                    NextEnabled = true,
                    Overlay = new OverlayConfig { CurrentUsageMb = 250, MaximumSizeMb = 4096 }
                }
            },
            new ClusterNodeResult
            {
                Host = "lab3-pc02.acd.ifpr.local",
                Success = false,
                Message = "Connection timed out"
            }
        };

        string html = ClusterManagerService.GenerateHtmlReport(results);
        Assert.Contains("lab3-pc01.acd.ifpr.local", html);
        Assert.Contains("FROZEN", html);
        Assert.Contains("Connection timed out", html);

        string csv = ClusterManagerService.GenerateCsvReport(results);
        Assert.Contains("lab3-pc01.acd.ifpr.local,True,True,True", csv);
        Assert.Contains("lab3-pc02.acd.ifpr.local,false", csv);
    }
}
