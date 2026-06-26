using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Infrastructure.Runners;

public sealed class VCRedistBundler : IVCRedistBundler
{
    private static readonly string[] VcRedistDlls =
    [
        "msvcp140.dll",
        "vcruntime140.dll",
        "vcruntime140_1.dll"
    ];

    public Task<bool> BundleAsync(
        EngineInfo engine,
        string buildOutputPath,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default)
    {
        try
        {
            var targetBinariesDir = FindGameBinariesDir(buildOutputPath);
            if (targetBinariesDir is null)
            {
                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Warning,
                    $"No Binaries/Win64 directory found in staged build: {buildOutputPath}", nameof(VCRedistBundler)));
                return Task.FromResult(false);
            }

            var sourceDir = FindRedistSourceDir(engine.Path, logProgress);
            if (sourceDir is null)
            {
                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Error,
                    "Could not find VC++ Redist DLLs in any known location. "
                    + "Install Visual C++ 2015-2022 Redistributable x64 from Microsoft.",
                    nameof(VCRedistBundler)));
                return Task.FromResult(false);
            }

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Info,
                $"Bundling VC++ Redist DLLs from: {sourceDir}", nameof(VCRedistBundler)));

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Info,
                $"Bundling VC++ Redist DLLs to: {targetBinariesDir}", nameof(VCRedistBundler)));

            var bundled = 0;
            foreach (var dll in VcRedistDlls)
            {
                ct.ThrowIfCancellationRequested();

                var sourcePath = Path.Combine(sourceDir, dll);
                if (!File.Exists(sourcePath))
                {
                    logProgress?.Report(new LogEntry(
                        DateTime.UtcNow, LogLevel.Warning,
                        $"Skipping {dll} (not found at {sourcePath})", nameof(VCRedistBundler)));
                    continue;
                }

                var destPath = Path.Combine(targetBinariesDir, dll);

                if (File.Exists(destPath))
                {
                    logProgress?.Report(new LogEntry(
                        DateTime.UtcNow, LogLevel.Debug,
                        $"{dll} already present, skipping", nameof(VCRedistBundler)));
                    bundled++;
                    continue;
                }

                File.Copy(sourcePath, destPath, overwrite: false);
                bundled++;

                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Debug,
                    $"Bundled {dll}", nameof(VCRedistBundler)));
            }

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Success,
                $"Bundled {bundled} VC++ Redist DLL(s)", nameof(VCRedistBundler)));

            StageRedistInstaller(engine.Path, buildOutputPath, logProgress, ct);

            return Task.FromResult(bundled > 0);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Error,
                $"Failed to bundle VC++ Redist DLLs: {ex.Message}", nameof(VCRedistBundler)));
            return Task.FromResult(false);
        }
    }

    private static string? FindRedistSourceDir(string enginePath, IProgress<LogEntry>? logProgress)
    {
        var candidates = GetCandidateDirectories(enginePath);

        foreach (var dir in candidates)
        {
            if (!Directory.Exists(dir))
                continue;

            var hasAll = true;
            foreach (var dll in VcRedistDlls)
            {
                if (!File.Exists(Path.Combine(dir, dll)))
                {
                    hasAll = false;
                    break;
                }
            }

            if (hasAll)
            {
                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Debug,
                    $"Found all VC++ DLLs in: {dir}", nameof(VCRedistBundler)));
                return dir;
            }
        }

        return null;
    }

    private static string[] GetCandidateDirectories(string enginePath)
    {
        var dirs = new List<string>(4);

        dirs.Add(Path.Combine(enginePath, "Engine", "Binaries", "Win64"));

        var engineRedist = Path.Combine(enginePath, "Engine", "Extras", "Redist", "en-us");
        if (Directory.Exists(engineRedist))
            dirs.Add(engineRedist);

        try
        {
            var vs2022Base = @"C:\Program Files\Microsoft Visual Studio\2022";
            if (Directory.Exists(vs2022Base))
            {
                foreach (var edition in Directory.GetDirectories(vs2022Base))
                {
                    var redistBase = Path.Combine(edition, "VC", "Redist", "MSVC");
                    if (!Directory.Exists(redistBase)) continue;

                    foreach (var versionDir in Directory.GetDirectories(redistBase))
                    {
                        var crtDir = Path.Combine(versionDir, "x64", "Microsoft.VC143.CRT");
                        if (Directory.Exists(crtDir))
                            dirs.Add(crtDir);
                    }
                }
            }
        }
        catch { }

        dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.System));

        return dirs.ToArray();
    }

    private static string? FindGameBinariesDir(string buildOutputPath)
    {
        var windowsDir = Path.Combine(buildOutputPath, "Windows");
        if (!Directory.Exists(windowsDir))
            return null;

        foreach (var projectDir in Directory.GetDirectories(windowsDir))
        {
            var binariesWin64 = Path.Combine(projectDir, "Binaries", "Win64");
            if (!Directory.Exists(binariesWin64))
                continue;

            if (Directory.GetFiles(binariesWin64, "*.exe").Length > 0)
                return binariesWin64;
        }

        foreach (var projectDir in Directory.GetDirectories(windowsDir))
        {
            var binariesWin64 = Path.Combine(projectDir, "Binaries", "Win64");
            if (Directory.Exists(binariesWin64))
                return binariesWin64;
        }

        return null;
    }

    private static void StageRedistInstaller(
        string enginePath,
        string buildOutputPath,
        IProgress<LogEntry>? logProgress,
        CancellationToken ct)
    {
        try
        {
            var sourceRedistDir = Path.Combine(enginePath, "Engine", "Extras", "Redist", "en-us");
            var vcRedistSource = Path.Combine(sourceRedistDir, "vc_redist.x64.exe");
            if (!File.Exists(vcRedistSource))
            {
                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Debug,
                    $"vc_redist.x64.exe not found in engine Redist: {vcRedistSource}", nameof(VCRedistBundler)));
                return;
            }

            var windowsDir = Path.Combine(buildOutputPath, "Windows");
            if (!Directory.Exists(windowsDir))
            {
                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Warning,
                    $"Windows staging directory not found: {windowsDir}", nameof(VCRedistBundler)));
                return;
            }

            ct.ThrowIfCancellationRequested();

            var targetRedistDir = Path.Combine(windowsDir, "Engine", "Extras", "Redist", "en-us");
            Directory.CreateDirectory(targetRedistDir);

            var vcRedistDest = Path.Combine(targetRedistDir, "vc_redist.x64.exe");
            if (!File.Exists(vcRedistDest))
            {
                File.Copy(vcRedistSource, vcRedistDest, overwrite: false);
                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Info,
                    "Copied VC++ Redist installer to staged build", nameof(VCRedistBundler)));
            }

            ct.ThrowIfCancellationRequested();

            var installScriptPath = Path.Combine(windowsDir, "InstallScript.vdf");
            WriteInstallScript(installScriptPath);

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Info,
                "Generated InstallScript.vdf for Steam prerequisites", nameof(VCRedistBundler)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Warning,
                $"Failed to stage Redist installer: {ex.Message}", nameof(VCRedistBundler)));
        }
    }

    private static void WriteInstallScript(string path)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("\"InstallScript\"");
        sb.AppendLine("{");
        sb.Append('\t').AppendLine("\"Run Process\"");
        sb.Append('\t').AppendLine("{");
        sb.Append('\t').Append('\t').AppendLine("\"Visual C++ Redist\"");
        sb.Append('\t').Append('\t').AppendLine("{");
        sb.Append('\t').Append('\t').Append('\t');
        sb.Append('"').Append("process_1").Append('"');
        sb.Append('\t');
        sb.Append('"').Append("%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\vc_redist.x64.exe").Append('"');
        sb.AppendLine();
        sb.Append('\t').Append('\t').Append('\t');
        sb.Append('"').Append("command_1").Append('"');
        sb.Append('\t');
        sb.Append('"').Append("/install /quiet /norestart").Append('"');
        sb.AppendLine();
        sb.Append('\t').Append('\t').Append('\t');
        sb.Append('"').Append("NoCleanUp").Append('"');
        sb.Append('\t');
        sb.Append('"').Append("1").Append('"');
        sb.AppendLine();
        sb.Append('\t').Append('\t').AppendLine("}");
        sb.Append('\t').AppendLine("}");
        sb.AppendLine("}");

        File.WriteAllText(path, sb.ToString());
    }
}
