namespace SteamUEDeployTool.Core.Models;

public sealed record PushResult(
    bool Success,
    BuildResult? BuildResult,
    DeployResult? DeployResult,
    TimeSpan Duration);
