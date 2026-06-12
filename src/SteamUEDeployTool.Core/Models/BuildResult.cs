namespace SteamUEDeployTool.Core.Models;

public sealed record BuildResult(
    bool Success,
    string? OutputPath,
    TimeSpan Duration,
    int ExitCode,
    IReadOnlyList<LogEntry> Logs,
    string? ErrorMessage);
