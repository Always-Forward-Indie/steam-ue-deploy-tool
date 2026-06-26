using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Abstractions;

public interface IVCRedistBundler
{
    Task<bool> BundleAsync(
        EngineInfo engine,
        string buildOutputPath,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default);
}
