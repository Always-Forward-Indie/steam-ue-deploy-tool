namespace SteamUEDeployTool.Core.Abstractions;

public interface ISecureCredentialStore
{
    Task SaveAsync(string accountId, string password, CancellationToken ct = default);
    Task<string?> GetAsync(string accountId, CancellationToken ct = default);
    Task<bool> DeleteAsync(string accountId, CancellationToken ct = default);
}
