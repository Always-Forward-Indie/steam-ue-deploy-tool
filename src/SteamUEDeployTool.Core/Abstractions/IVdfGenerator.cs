using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Abstractions;

public interface IVdfGenerator
{
    GeneratedVdfFiles Generate(DeployTarget target, string buildPath, string? outputDirectory = null);
}

public sealed record GeneratedVdfFiles(
    string AppBuildVdfPath,
    IReadOnlyList<string> DepotVdfPaths);
