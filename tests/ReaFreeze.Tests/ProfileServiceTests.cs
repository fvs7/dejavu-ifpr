using ReaFreeze.Core.Services;
using Xunit;

namespace ReaFreeze.Tests;

public class ProfileServiceTests
{
    [Fact]
    public void Should_Load_Default_Exclusion_Profile()
    {
        var logger = new LoggerService();
        var service = new ProfileService(logger);

        string tempFile = Path.GetTempFileName();
        try
        {
            string jsonContent = "{\"version\": \"1.0\", \"description\": \"Teste\", \"fileAndFolderExclusions\": [\"C:\\TestFolder\"], \"registryExclusions\": [\"HKLM\\Software\\TestKey\"]}";
            File.WriteAllText(tempFile, jsonContent);

            var profile = service.LoadProfile(tempFile);
            Assert.NotNull(profile);
            Assert.Equal("1.0", profile.Version);
            Assert.Single(profile.FileAndFolderExclusions);
            Assert.Single(profile.RegistryExclusions);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Should_Apply_Profile_Exclusions_To_Controller()
    {
        var logger = new LoggerService();
        var service = new ProfileService(logger);
        var mock = new MockUwfController();

        var profile = new ReaFreeze.Core.Models.ExclusionProfile
        {
            Version = "1.0",
            FileAndFolderExclusions = { @"C:\ProgramData\Veyon", @"C:\Persistente" },
            RegistryExclusions = { @"HKLM\SYSTEM\CurrentControlSet\Services\Netlogon\Parameters" }
        };

        service.ApplyProfile(mock, profile);

        Assert.Equal(2, mock.AddedFileExclusions.Count);
        Assert.Single(mock.AddedRegistryExclusions);
        Assert.Contains(@"C:\Persistente", mock.AddedFileExclusions);
        Assert.Contains(@"HKLM\SYSTEM\CurrentControlSet\Services\Netlogon\Parameters", mock.AddedRegistryExclusions);
    }
}
