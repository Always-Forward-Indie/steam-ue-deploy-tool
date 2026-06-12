using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Abstractions;

public interface ISteamDeployer
{
    Task<DeployResult> DeployAsync(
        DeployTarget target,
        string buildPath,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default);
}
