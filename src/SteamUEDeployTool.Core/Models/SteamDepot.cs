namespace SteamUEDeployTool.Core.Models;

public sealed record FileMapping(
    string LocalPath = "*",
    string DepotPath = ".",
    bool Recursive = true);

public sealed class SteamDepot
{
    public uint DepotId { get; set; }
    public string ContentRoot { get; set; } = string.Empty;
    public List<FileMapping> Mappings { get; set; } = [new()];
}
