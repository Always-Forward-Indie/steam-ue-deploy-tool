using System.Text;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Infrastructure.Vdf;

public sealed class VdfGenerator : IVdfGenerator
{
    public GeneratedVdfFiles Generate(DeployTarget target, string buildPath, string? outputDirectory = null)
    {
        var outputDir = outputDirectory
            ?? Path.Combine(Path.GetTempPath(), "SteamUEDeployTool", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(outputDir);

        var depotVdfPaths = new List<string>(target.Depots.Count);
        var depotEntries = new Dictionary<string, object>();

        foreach (var depot in target.Depots)
        {
            var depotFileName = $"depot_build_{depot.DepotId}.vdf";
            var depotFilePath = Path.Combine(outputDir, depotFileName);

            var depotVdf = BuildDepotVdf(depot);
            File.WriteAllText(depotFilePath, depotVdf);

            depotVdfPaths.Add(depotFilePath);
            depotEntries[depot.DepotId.ToString()] = depotFileName;
        }

        var appBuildFileName = $"app_build_{target.AppId}.vdf";
        var appBuildFilePath = Path.Combine(outputDir, appBuildFileName);

        var appBuildVdf = BuildAppVdf(target, buildPath, depotEntries);
        File.WriteAllText(appBuildFilePath, VdfSerializer.Serialize(appBuildVdf));

        return new GeneratedVdfFiles(appBuildFilePath, depotVdfPaths);
    }

    private static Dictionary<string, object> BuildAppVdf(
        DeployTarget target,
        string buildPath,
        Dictionary<string, object> depotEntries)
    {
        var root = new Dictionary<string, object>
        {
            ["appid"] = target.AppId,
            ["desc"] = target.BuildDescription,
            ["buildoutput"] = Path.GetFullPath(buildPath),
            ["contentroot"] = Path.GetFullPath(buildPath)
        };

        if (target.SetLiveAfterUpload)
        {
            root["setlive"] = target.BranchName;
        }

        root["depots"] = new Dictionary<string, object>(depotEntries);

        return new Dictionary<string, object>
        {
            ["AppBuild"] = root
        };
    }

    public static string BuildDepotVdf(SteamDepot depot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"DepotBuildConfig\"");
        sb.AppendLine("{");
        sb.Append('\t');
        sb.Append('"').Append("DepotID").Append('"');
        sb.Append("\t\t");
        sb.Append('"').Append(depot.DepotId).Append('"');
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(depot.ContentRoot))
        {
            sb.Append('\t');
            sb.Append('"').Append("ContentRoot").Append('"');
            sb.Append("\t\t");
            sb.Append('"').Append(VdfEscape(Path.GetFullPath(depot.ContentRoot))).Append('"');
            sb.AppendLine();
        }

        foreach (var mapping in depot.Mappings)
        {
            sb.AppendLine("\t\"FileMapping\"");
            sb.AppendLine("\t{");
            sb.Append('\t').Append('\t');
            sb.Append('"').Append("LocalPath").Append('"');
            sb.Append("\t\t");
            sb.Append('"').Append(VdfEscape(mapping.LocalPath)).Append('"');
            sb.AppendLine();
            sb.Append('\t').Append('\t');
            sb.Append('"').Append("DepotPath").Append('"');
            sb.Append("\t\t");
            sb.Append('"').Append(VdfEscape(mapping.DepotPath)).Append('"');
            sb.AppendLine();
            sb.Append('\t').Append('\t');
            sb.Append('"').Append("recursive").Append('"');
            sb.Append("\t\t");
            sb.Append('"').Append(mapping.Recursive ? "1" : "0").Append('"');
            sb.AppendLine();
            sb.AppendLine("\t}");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    internal static string VdfEscape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
