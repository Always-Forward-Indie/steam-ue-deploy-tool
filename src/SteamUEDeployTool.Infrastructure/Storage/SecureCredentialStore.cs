using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SteamUEDeployTool.Core.Abstractions;

namespace SteamUEDeployTool.Infrastructure.Storage;

public sealed class SecureCredentialStore : ISecureCredentialStore
{
    private readonly string _basePath;

    public SecureCredentialStore(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamUEDeployTool",
            "credentials");

        Directory.CreateDirectory(_basePath);
    }

    public Task SaveAsync(string accountId, string password, CancellationToken ct = default)
    {
        var filePath = GetCredentialPath(accountId);
        var protectedData = Protect(password, accountId);
        return File.WriteAllTextAsync(filePath, protectedData, ct);
    }

    public Task<string?> GetAsync(string accountId, CancellationToken ct = default)
    {
        var filePath = GetCredentialPath(accountId);
        if (!File.Exists(filePath))
            return Task.FromResult<string?>(null);

        var protectedData = File.ReadAllText(filePath);
        var result = Unprotect(protectedData, accountId);
        return Task.FromResult(result);
    }

    public Task<bool> DeleteAsync(string accountId, CancellationToken ct = default)
    {
        var filePath = GetCredentialPath(accountId);
        if (!File.Exists(filePath))
            return Task.FromResult(false);

        File.Delete(filePath);
        return Task.FromResult(true);
    }

    private string GetCredentialPath(string accountId)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(accountId)));
        return Path.Combine(_basePath, $"{hash}.dat");
    }

    private static string Protect(string data, string entropy)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ProtectWindows(data, entropy)
            : ProtectAes(data, entropy);
    }

    private static string? Unprotect(string protectedData, string entropy)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? UnprotectWindows(protectedData, entropy)
            : UnprotectAes(protectedData, entropy);
    }

#pragma warning disable CA1416
    private static string ProtectWindows(string data, string entropy)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var entropyBytes = Encoding.UTF8.GetBytes(entropy);
        var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
            dataBytes, entropyBytes, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string? UnprotectWindows(string protectedData, string entropy)
    {
        try
        {
            var dataBytes = Convert.FromBase64String(protectedData);
            var entropyBytes = Encoding.UTF8.GetBytes(entropy);
            var unprotectedBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                dataBytes, entropyBytes, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unprotectedBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
#pragma warning restore CA1416

    private static string ProtectAes(string data, string entropy)
    {
        var key = DeriveKey(entropy);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(data);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    private static string? UnprotectAes(string protectedData, string entropy)
    {
        try
        {
            var key = DeriveKey(entropy);
            var fullBytes = Convert.FromBase64String(protectedData);
            var iv = new byte[16];
            var cipherBytes = new byte[fullBytes.Length - 16];

            Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
            Buffer.BlockCopy(fullBytes, 16, cipherBytes, 0, cipherBytes.Length);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static byte[] DeriveKey(string entropy)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes($"SdtCredential:{entropy}"));
    }
}
