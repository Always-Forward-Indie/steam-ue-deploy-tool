using Xunit;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Infrastructure.Storage;

namespace SteamUEDeployTool.Infrastructure.Tests.Storage;

public class ProfileRepositoryTests
{
    private readonly string _testDir;

    public ProfileRepositoryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "SdtTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public async Task GetAll_EmptyRepository_ReturnsEmpty()
    {
        var repo = new ProfileRepository(_testDir);
        var result = await repo.GetAllAsync<BuildProfile>();
        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveAndGetById_RoundTrip_Works()
    {
        var repo = new ProfileRepository(_testDir);
        var profile = new BuildProfile
        {
            Id = Guid.NewGuid(),
            Name = "Test Build",
            UProjectPath = "/test/Test.uproject"
        };

        await repo.SaveAsync(profile);

        var loaded = await repo.GetByIdAsync<BuildProfile>(profile.Id);

        Assert.NotNull(loaded);
        Assert.Equal(profile.Name, loaded!.Name);
        Assert.Equal(profile.UProjectPath, loaded.UProjectPath);
    }

    [Fact]
    public async Task Delete_RemovesProfile()
    {
        var repo = new ProfileRepository(_testDir);
        var profile = new BuildProfile
        {
            Id = Guid.NewGuid(),
            Name = "To Delete"
        };

        await repo.SaveAsync(profile);
        var deleted = await repo.DeleteAsync<BuildProfile>(profile.Id);
        var loaded = await repo.GetByIdAsync<BuildProfile>(profile.Id);

        Assert.True(deleted);
        Assert.Null(loaded);
    }
}
