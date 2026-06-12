namespace SteamUEDeployTool.Core.Models;

public sealed class DeployTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public uint AppId { get; set; }
    public List<SteamDepot> Depots { get; set; } = [];
    public string BranchName { get; set; } = "default";
    public bool SetLiveAfterUpload { get; set; }
    public string BuildDescription { get; set; } = string.Empty;
    public string? SteamAccountId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
