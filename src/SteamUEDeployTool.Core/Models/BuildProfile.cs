using System.Text.Json.Serialization;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Core.Models;

public sealed class BuildProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string UProjectPath { get; set; } = string.Empty;
    public string? CustomEnginePath { get; set; }
    public Platform Platform { get; set; } = Platform.Win64;
    public BuildConfiguration BuildConfiguration { get; set; } = BuildConfiguration.Development;
    public bool Cook { get; set; }
    public bool CleanBuild { get; set; }
    public bool BundleVCRedist { get; set; } = true;
    public string? ExtraArgs { get; set; }
    public string? OutputPathOverride { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
