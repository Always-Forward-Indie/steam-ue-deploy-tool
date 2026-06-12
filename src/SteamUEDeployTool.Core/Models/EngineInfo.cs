using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Core.Models;

public sealed record EngineInfo(
    string Path,
    string Version,
    EngineAssociationType Type);
