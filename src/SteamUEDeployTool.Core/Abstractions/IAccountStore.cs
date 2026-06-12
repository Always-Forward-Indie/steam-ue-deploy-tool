using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Abstractions;

public interface IAccountStore
{
    IReadOnlyList<SteamAccount> GetAll();
    SteamAccount? GetById(string id);
    void Save(SteamAccount account);
    bool Delete(string id);

    Task<LoginResult> LoginAsync(
        string accountId,
        Func<CancellationToken, Task<string>>? steamGuardProvider = null,
        CancellationToken ct = default);

    void Logout(string accountId);

    ValueTask<IReadOnlyList<SteamAccount>> GetAllAsync(CancellationToken ct = default);
    ValueTask<SteamAccount?> GetByIdAsync(string id, CancellationToken ct = default);
    ValueTask SaveAsync(SteamAccount account, CancellationToken ct = default);
    ValueTask<bool> DeleteAsync(string id, CancellationToken ct = default);
}

public sealed record LoginResult(
    bool Success,
    bool RequiresSteamGuard,
    string? ErrorMessage);
