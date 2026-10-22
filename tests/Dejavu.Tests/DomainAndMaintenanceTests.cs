using Dejavu.Core.Models;
using Dejavu.Core.Services;
using Xunit;

namespace Dejavu.Tests;

public class DomainAndMaintenanceTests
{
    [Fact]
    public void Should_Add_ThawSpace_Exclusion()
    {
        var logger = new LoggerService();
        var mock = new MockUwfController();
        var thawSpace = new ThawSpaceService(logger);

        string target = Path.Combine(Path.GetTempPath(), "DejavuTestPersistente");
        try
        {
            thawSpace.EnsureDirectoryThawSpace(mock, target);
            Assert.Contains(target, mock.AddedFileExclusions);
            Assert.True(Directory.Exists(target));
        }
        finally
        {
            if (Directory.Exists(target)) Directory.Delete(target);
        }
    }
}
