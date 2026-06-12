using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Abstractions;

public interface IEngineResolver
{
    Task<EngineInfo?> ResolveFromUProjectAsync(
        string uprojectPath,
        CancellationToken ct = default);

    Task<EngineInfo?> ResolveFromPathAsync(
        string enginePath,
        CancellationToken ct = default);
}
