using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Abstractions;

public interface ISteamCmdLoginService
{
    Task<LoginResult> LoginAsync(
        string username,
        string password,
        Func<CancellationToken, Task<string>>? steamGuardProvider = null,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default);

    bool HasCachedLogin();
}
