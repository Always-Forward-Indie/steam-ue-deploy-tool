using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Infrastructure.Discovery;

public sealed class EngineResolver : IEngineResolver
{
    public async Task<EngineInfo?> ResolveFromUProjectAsync(
        string uprojectPath,
        CancellationToken ct = default)
    {
        if (!File.Exists(uprojectPath))
            return null;

        var json = await File.ReadAllTextAsync(uprojectPath, ct);
        var node = JsonNode.Parse(json);
        if (node is null)
            return null;

        var association = node["EngineAssociation"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(association))
            return null;

        return await ResolveAssociationAsync(association, ct);
    }

    public string? ReadAssociationFromUProject(string uprojectPath)
    {
        if (!File.Exists(uprojectPath))
            return null;

        var json = File.ReadAllText(uprojectPath);
        var node = JsonNode.Parse(json);
        return node?["EngineAssociation"]?.GetValue<string>();
    }

    public Task<EngineInfo?> ResolveFromPathAsync(
        string enginePath,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(enginePath))
            return Task.FromResult<EngineInfo?>(null);

        var version = ReadEngineVersion(enginePath);
        var type = ClassifyPath(enginePath);

        return Task.FromResult<EngineInfo?>(new EngineInfo(enginePath, version, type));
    }

    private Task<EngineInfo?> ResolveAssociationAsync(
        string association,
        CancellationToken ct)
    {
        if (TryParseAsGuid(association, out var guid))
            return ResolveLauncherEngineAsync(guid);

        if (Directory.Exists(association))
            return ResolveSourceOrCustomAsync(association);

        var engine = TryResolveByVersionString(association);
        if (engine is not null)
            return Task.FromResult<EngineInfo?>(engine);

        return ResolveSourceOrCustomAsync(association);
    }

    private static bool TryParseAsGuid(string input, out Guid guid)
    {
        input = input.Trim('{', '}', '(', ')');

        if (Guid.TryParseExact(input, "D", out guid))
            return true;

        if (Guid.TryParseExact(input, "N", out guid))
            return true;

        return false;
    }

    private static EngineInfo? TryResolveByVersionString(string version)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var fromRegistry = TryReadVersionFromRegistry(version);
            if (fromRegistry is not null)
                return fromRegistry;

            return ScanForEngineDirectory(version, new[]
            {
                @"C:\Program Files\Epic Games",
                @"D:\Game Dev\UE",
                @"E:\Game Dev\UE",
                @"C:\UE",
                @"D:\UE",
                @"E:\UE",
                @"C:\Epic Games",
                @"D:\Epic Games"
            });
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ScanForEngineDirectory(version, new[]
            {
                "/Users/Shared/Epic/UnrealEngine",
                "/Applications/Epic Games"
            });
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return ScanForEngineDirectory(version, new[]
            {
                Path.Combine(home, "Epic Games"),
                "/opt/EpicGames"
            });
        }

        return null;
    }

#pragma warning disable CA1416
    private static EngineInfo? TryReadVersionFromRegistry(string version)
    {
        try
        {
            var basePath = @"SOFTWARE\EpicGames\Unreal Engine";
            var subKeys = new[] { version, $"UE_{version}", version.Replace(".", "") };

            foreach (var sk in subKeys)
            {
                var installDir = Registry.GetValue(
                    $@"HKEY_LOCAL_MACHINE\{basePath}\{sk}", "InstalledDirectory", null) as string;

                if (string.IsNullOrWhiteSpace(installDir))
                {
                    installDir = Registry.GetValue(
                        $@"HKEY_CURRENT_USER\{basePath}\{sk}", "InstalledDirectory", null) as string;
                }

                if (!string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir))
                {
                    var engineVersion = ReadEngineVersion(installDir);
                    return new EngineInfo(installDir, engineVersion, EngineAssociationType.Launcher);
                }
            }
        }
        catch { }

        return null;
    }

    private static EngineInfo? ScanForEngineDirectory(string version, IEnumerable<string> basePaths)
    {
        foreach (var basePath in basePaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            foreach (var dir in Directory.GetDirectories(basePath))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.Contains(version, StringComparison.OrdinalIgnoreCase)
                    || dirName.Equals($"UE_{version}", StringComparison.OrdinalIgnoreCase)
                    || dirName.Equals($"UE_{version.Replace(".", "")}", StringComparison.OrdinalIgnoreCase))
                {
                    var engineVersion = ReadEngineVersion(dir);
                    return new EngineInfo(dir, engineVersion, EngineAssociationType.Launcher);
                }
            }
        }

        return null;
    }

    private static Task<EngineInfo?> ResolveLauncherEngineAsync(Guid guid)
    {
        string? installDir = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            installDir = TryReadRegistryValue(guid);

            if (string.IsNullOrWhiteSpace(installDir))
            {
                var altPath = @"SOFTWARE\Epic Games\EpicGamesLauncher\AppDataPath";
                var appDataPath = Registry.GetValue(
                    $@"HKEY_LOCAL_MACHINE\{altPath}", "AppDataPath", null) as string;

                if (!string.IsNullOrWhiteSpace(appDataPath))
                {
                    var manifestPath = Path.Combine(appDataPath, "Manifests");
                    if (Directory.Exists(manifestPath))
                    {
                        var guidStr = guid.ToString("D").ToUpperInvariant();
                        foreach (var itemFile in Directory.GetFiles(manifestPath, "*.item"))
                        {
                            try
                            {
                                var json = File.ReadAllText(itemFile);
                                var node = JsonNode.Parse(json);
                                var catalogItemId = node?["CatalogItemId"]?.GetValue<string>();
                                if (catalogItemId is not null
                                    && catalogItemId.Contains(guidStr, StringComparison.OrdinalIgnoreCase))
                                {
                                    installDir = node?["InstallLocation"]?.GetValue<string>();
                                    if (!string.IsNullOrWhiteSpace(installDir))
                                        break;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var path = $"/Users/Shared/Epic/UnrealEngine/{guid:D}";
            if (Directory.Exists(path))
                installDir = path;
        }

        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            return Task.FromResult<EngineInfo?>(null);

        var version = ReadEngineVersion(installDir);
        return Task.FromResult<EngineInfo?>(new EngineInfo(installDir, version, EngineAssociationType.Launcher));
    }

    private static string? TryReadRegistryValue(Guid guid)
    {
        try
        {
            foreach (var format in new[] { "D", "B", "N" })
            {
                var guidStr = guid.ToString(format);
                var keyPath = $@"SOFTWARE\EpicGames\Unreal Engine\{guidStr}";
                var installDir = Registry.GetValue(
                    $@"HKEY_LOCAL_MACHINE\{keyPath}", "InstalledDirectory", null) as string;

                if (!string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir))
                    return installDir;
            }
        }
        catch { }

        return null;
    }
#pragma warning restore CA1416

    private static Task<EngineInfo?> ResolveSourceOrCustomAsync(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
            return Task.FromResult<EngineInfo?>(null);

        string enginePath;
        if (File.Exists(path))
            enginePath = Path.GetDirectoryName(path) ?? path;
        else
            enginePath = path;

        var version = ReadEngineVersion(enginePath);
        var type = ClassifyPath(enginePath);

        return Task.FromResult<EngineInfo?>(new EngineInfo(enginePath, version, type));
    }

    private static string ReadEngineVersion(string enginePath)
    {
        var versionFile = Path.Combine(enginePath, "Engine", "Build", "Build.version");
        if (!File.Exists(versionFile))
            return "unknown";

        try
        {
            var json = File.ReadAllText(versionFile);
            var node = JsonNode.Parse(json);
            var major = node?["MajorVersion"]?.GetValue<int>() ?? 0;
            var minor = node?["MinorVersion"]?.GetValue<int>() ?? 0;
            var patch = node?["PatchVersion"]?.GetValue<int>() ?? 0;
            return $"{major}.{minor}.{patch}";
        }
        catch
        {
            return "unknown";
        }
    }

    private static EngineAssociationType ClassifyPath(string enginePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (enginePath.Contains(@"\Epic Games\UE_", StringComparison.OrdinalIgnoreCase)
                || enginePath.Contains(@"\UE\UE_", StringComparison.OrdinalIgnoreCase)
                || enginePath.Contains(@"\EpicGames\UE_", StringComparison.OrdinalIgnoreCase))
                return EngineAssociationType.Launcher;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (enginePath.Contains("/Epic/UnrealEngine/", StringComparison.OrdinalIgnoreCase))
                return EngineAssociationType.Launcher;
        }

        if (Directory.Exists(Path.Combine(enginePath, ".git")))
            return EngineAssociationType.Source;

        return EngineAssociationType.Custom;
    }
}
