using System.Reflection;

namespace SteamUEDeployTool.Core.Validation;

public static class SteamCmdValidator
{
    private static readonly string[] KnownNames = OperatingSystem.IsWindows()
        ? ["steamcmd.exe"]
        : ["steamcmd", "steamcmd.sh"];

    public static ValidationResult ValidateInstallation(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            return ValidateAtPath(customPath);
        }

        var resolved = ResolveExecutablePath(customPath);
        if (resolved is not null)
            return ValidationResult.Success();

        return ValidationResult.Failure(
            "steamcmd not found.\n" +
            "- Install: https://developer.valvesoftware.com/wiki/SteamCMD\n" +
            "- Or place steamcmd.exe in the tools/steamcmd folder next to the app.\n" +
            "- Or add steamcmd to your PATH.");
    }

    public static ValidationResult ValidateAtPath(string path)
    {
        if (File.Exists(path))
            return ValidationResult.Success();

        foreach (var name in KnownNames)
        {
            var combined = Path.Combine(path, name);
            if (File.Exists(combined))
                return ValidationResult.Success();
        }

        return ValidationResult.Failure($"steamcmd not found at '{path}'.");
    }

    public static string? ResolveExecutablePath(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
                return customPath;

            foreach (var name in KnownNames)
            {
                var combined = Path.Combine(customPath, name);
                if (File.Exists(combined))
                    return combined;
            }
        }

        var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
        if (appDir is not null)
        {
            var bundledPaths = new[]
            {
                Path.Combine(appDir, "tools", "steamcmd"),
                Path.Combine(appDir, "..", "..", "tools", "steamcmd"),
                Path.Combine(appDir, "..", "..", "..", "tools", "steamcmd"),
                Path.Combine(appDir, "..", "..", "..", "..", "tools", "steamcmd")
            };

            foreach (var bundledPath in bundledPaths)
            {
                foreach (var name in KnownNames)
                {
                    var fullPath = Path.Combine(bundledPath, name);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }
        }

        return KnownNames.Select(FindInPath).FirstOrDefault(p => p is not null);
    }

    private static string? FindInPath(string executableName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVar))
            return null;

        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        foreach (var dir in pathVar.Split(separator))
        {
            var fullPath = Path.Combine(dir.Trim(), executableName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}
