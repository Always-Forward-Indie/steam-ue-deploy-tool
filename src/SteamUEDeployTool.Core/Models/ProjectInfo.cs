namespace SteamUEDeployTool.Core.Models;

public sealed record ProjectInfo(
    string UProjectPath,
    string Name,
    EngineInfo Engine,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Plugins);
