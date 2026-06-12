using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Core.Models;

public sealed record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    string? Source = null);
