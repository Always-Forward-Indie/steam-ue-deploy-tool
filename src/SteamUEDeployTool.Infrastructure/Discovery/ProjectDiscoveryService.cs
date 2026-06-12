using System.Text.Json;
using System.Text.Json.Nodes;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Infrastructure.Discovery;

public sealed class ProjectDiscoveryService : IProjectDiscoveryService
{
    private readonly IEngineResolver _engineResolver;

    private static readonly string[] ExcludedDirectories =
    [
        "binaries", "intermediate", "deriveddatacache", "saved", "build",
        ".git", ".svn", ".vs", "node_modules", "__pycache__",
        "platforms", "config", "content", "plugins"
    ];

    private static readonly EnumerationOptions SearchOptions = new()
    {
        RecurseSubdirectories = true,
        MaxRecursionDepth = 8,
        IgnoreInaccessible = true
    };

    public ProjectDiscoveryService(IEngineResolver engineResolver)
    {
        _engineResolver = engineResolver;
    }

    public async Task<IReadOnlyList<ProjectInfo>> DiscoverAsync(
        IEnumerable<string> rootPaths,
        CancellationToken ct = default)
    {
        var uprojectFiles = new List<string>();

        foreach (var rootPath in rootPaths)
        {
            if (!Directory.Exists(rootPath))
                continue;

            try
            {
                var files = Directory.GetFiles(rootPath, "*.uproject", SearchOptions);
                uprojectFiles.AddRange(files);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        var results = new List<ProjectInfo>(uprojectFiles.Count);

        foreach (var file in uprojectFiles)
        {
            ct.ThrowIfCancellationRequested();

            var project = await ParseProjectAsync(file, ct);
            if (project is not null)
                results.Add(project);
        }

        return results;
    }

    public async Task<ProjectInfo?> ParseProjectAsync(
        string uprojectPath,
        CancellationToken ct = default)
    {
        if (!File.Exists(uprojectPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(uprojectPath, ct);
            var node = JsonNode.Parse(json);
            if (node is null)
                return null;

            var name = Path.GetFileNameWithoutExtension(uprojectPath);

            var modules = new List<string>();
            var modulesNode = node["Modules"]?.AsArray();
            if (modulesNode is not null)
            {
                foreach (var module in modulesNode)
                {
                    var moduleName = module?["Name"]?.GetValue<string>();
                    if (moduleName is not null)
                        modules.Add(moduleName);
                }
            }

            var plugins = new List<string>();
            var pluginsNode = node["Plugins"]?.AsArray();
            if (pluginsNode is not null)
            {
                foreach (var plugin in pluginsNode)
                {
                    var pluginName = plugin?["Name"]?.GetValue<string>();
                    if (pluginName is not null)
                        plugins.Add(pluginName);
                }
            }

            var engine = await _engineResolver.ResolveFromUProjectAsync(uprojectPath, ct);
            if (engine is null)
                return null;

            return new ProjectInfo(uprojectPath, name, engine, modules, plugins);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
