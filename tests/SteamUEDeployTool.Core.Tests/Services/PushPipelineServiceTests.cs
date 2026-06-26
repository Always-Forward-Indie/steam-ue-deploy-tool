using Xunit;
using NSubstitute;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Services;

namespace SteamUEDeployTool.Core.Tests.Services;

public class PushPipelineServiceTests
{
    private readonly IProfileRepository _repo = Substitute.For<IProfileRepository>();
    private readonly IBuildRunner _buildRunner = Substitute.For<IBuildRunner>();
    private readonly ISteamDeployer _deployer = Substitute.For<ISteamDeployer>();
    private readonly IEngineResolver _engineResolver = Substitute.For<IEngineResolver>();
    private readonly IVCRedistBundler _vcredistBundler = Substitute.For<IVCRedistBundler>();

    [Fact]
    public async Task PushAsync_MissingBuildProfile_ReturnsFailure()
    {
        var engineResolver = Substitute.For<IEngineResolver>();
        var buildOrch = new BuildOrchestrator(_buildRunner, engineResolver, _vcredistBundler);
        var deployOrch = new DeployOrchestrator(_deployer, Substitute.For<IAccountStore>(), Substitute.For<IVdfGenerator>());
        var pipeline = new PushPipelineService(buildOrch, deployOrch, _repo);

        var pushProfile = new PushProfile
        {
            Name = "Test",
            BuildProfileId = Guid.NewGuid(),
            DeployTargetId = Guid.NewGuid()
        };

        var result = await pipeline.PushAsync(pushProfile);

        Assert.False(result.Success);
        Assert.Null(result.BuildResult);
    }

    [Fact]
    public async Task PushAsync_MissingDeployTarget_ReturnsFailure()
    {
        var buildProfile = new BuildProfile
        {
            Id = Guid.NewGuid(),
            Name = "Test Build",
            UProjectPath = "test.uproject"
        };

        var engineResolver = Substitute.For<IEngineResolver>();
        var buildOrch = new BuildOrchestrator(_buildRunner, engineResolver, _vcredistBundler);
        var deployOrch = new DeployOrchestrator(_deployer, Substitute.For<IAccountStore>(), Substitute.For<IVdfGenerator>());
        var pipeline = new PushPipelineService(buildOrch, deployOrch, _repo);

        _repo.GetByIdAsync<BuildProfile>(Arg.Any<Guid>()).Returns(buildProfile);
        _repo.GetByIdAsync<DeployTarget>(Arg.Any<Guid>()).Returns((DeployTarget?)null);

        var pushProfile = new PushProfile
        {
            Name = "Test",
            BuildProfileId = buildProfile.Id,
            DeployTargetId = Guid.NewGuid()
        };

        var result = await pipeline.PushAsync(pushProfile);

        Assert.False(result.Success);
    }
}
