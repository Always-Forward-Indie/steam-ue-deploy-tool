using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Abstractions;

public interface IProjectDiscoveryService
{
    Task<IReadOnlyList<ProjectInfo>> DiscoverAsync(
        IEnumerable<string> rootPaths,
        CancellationToken ct = default);

    Task<ProjectInfo?> ParseProjectAsync(
        string uprojectPath,
        CancellationToken ct = default);
}
