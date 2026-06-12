using Xunit;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Core.Tests.Models;

public class BuildProfileTests
{
    [Fact]
    public void NewProfile_HasNonEmptyId()
    {
        var profile = new BuildProfile();
        Assert.NotEqual(Guid.Empty, profile.Id);
    }

    [Fact]
    public void NewProfile_HasDefaultPlatform()
    {
        var profile = new BuildProfile();
        Assert.Equal(Platform.Win64, profile.Platform);
    }

    [Fact]
    public void NewProfile_HasDefaultConfig()
    {
        var profile = new BuildProfile();
        Assert.Equal(BuildConfiguration.Development, profile.BuildConfiguration);
    }

    [Fact]
    public void NewProfile_HasEmptyName()
    {
        var profile = new BuildProfile();
        Assert.Equal(string.Empty, profile.Name);
    }
}
