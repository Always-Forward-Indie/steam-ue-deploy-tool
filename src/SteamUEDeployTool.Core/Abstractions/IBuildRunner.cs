using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Abstractions;

public interface IBuildRunner
{
    Task<BuildResult> RunAsync(
        BuildProfile profile,
        EngineInfo engine,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default);
}
