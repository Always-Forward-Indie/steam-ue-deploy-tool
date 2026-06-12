namespace SteamUEDeployTool.Core.Models;

public sealed class SteamAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Username { get; set; } = string.Empty;
    public bool HasSsfn { get; set; }
    public bool HasCredential { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
