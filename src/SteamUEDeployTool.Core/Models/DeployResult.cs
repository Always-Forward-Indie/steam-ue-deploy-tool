namespace SteamUEDeployTool.Core.Models;

public sealed record DeployResult(
    bool Success,
    ulong? ManifestId,
    TimeSpan Duration,
    int ExitCode,
    IReadOnlyList<LogEntry> Logs,
    string? ErrorMessage);
