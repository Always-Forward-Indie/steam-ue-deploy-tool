using System.Collections.Concurrent;
using System.Text.Json;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Infrastructure.Storage;

public sealed class AccountStore : IAccountStore
{
    private readonly string _basePath;
    private readonly string _accountsFilePath;
    private readonly ISecureCredentialStore _credentialStore;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AccountStore(ISecureCredentialStore credentialStore, string? basePath = null)
    {
        _credentialStore = credentialStore;
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamUEDeployTool");

        _accountsFilePath = Path.Combine(_basePath, "accounts.json");

        Directory.CreateDirectory(_basePath);
    }

    public IReadOnlyList<SteamAccount> GetAll() => GetAllAsync().GetAwaiter().GetResult();

    public SteamAccount? GetById(string id) => GetByIdAsync(id).GetAwaiter().GetResult();

    public void Save(SteamAccount account) => SaveAsync(account).GetAwaiter().GetResult();

    public bool Delete(string id) => DeleteAsync(id).GetAwaiter().GetResult();

    public async ValueTask<IReadOnlyList<SteamAccount>> GetAllAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_accountsFilePath))
            return Array.Empty<SteamAccount>();

        var json = await File.ReadAllTextAsync(_accountsFilePath, ct);
        var accounts = JsonSerializer.Deserialize<List<SteamAccount>>(json, JsonOptions);

        if (accounts is not null)
        {
            foreach (var account in accounts)
            {
                account.HasCredential = !string.IsNullOrEmpty(
                    await _credentialStore.GetAsync(account.Id, ct));
            }
        }

        return accounts ?? [];
    }

    public async ValueTask<SteamAccount?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(a => a.Id == id);
    }

    public async ValueTask SaveAsync(SteamAccount account, CancellationToken ct = default)
    {
        var all = (await GetAllAsync(ct)).ToList();
        var existingIndex = all.FindIndex(a => a.Id == account.Id);

        if (existingIndex >= 0)
            all[existingIndex] = account;
        else
            all.Add(account);

        var json = JsonSerializer.Serialize(all, JsonOptions);
        var tempPath = _accountsFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _accountsFilePath, overwrite: true);
    }

    public async ValueTask<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var all = (await GetAllAsync(ct)).ToList();
        var removed = all.RemoveAll(a => a.Id == id);

        if (removed == 0)
            return false;

        var json = JsonSerializer.Serialize(all, JsonOptions);
        var tempPath = _accountsFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _accountsFilePath, overwrite: true);

        await _credentialStore.DeleteAsync(id, ct);

        return true;
    }

    public async Task<LoginResult> LoginAsync(
        string accountId,
        Func<CancellationToken, Task<string>>? steamGuardProvider = null,
        CancellationToken ct = default)
    {
        var account = await GetByIdAsync(accountId, ct);
        if (account is null)
            return new LoginResult(false, false, $"Account '{accountId}' not found.");

        if (steamGuardProvider is null)
            return new LoginResult(false, true, "Steam Guard code required.");

        try
        {
            var code = await steamGuardProvider(ct);

            if (string.IsNullOrWhiteSpace(code))
                return new LoginResult(false, false, "Steam Guard code was empty.");

            return new LoginResult(true, false, null);
        }
        catch (OperationCanceledException)
        {
            return new LoginResult(false, false, "Login cancelled.");
        }
        catch (Exception ex)
        {
            return new LoginResult(false, false, $"Login failed: {ex.Message}");
        }
    }

    public void Logout(string accountId)
    {
        var account = GetById(accountId);
        if (account is null) return;

        account.HasSsfn = false;
        account.LastLoginAt = null;
        Save(account);
    }
}
