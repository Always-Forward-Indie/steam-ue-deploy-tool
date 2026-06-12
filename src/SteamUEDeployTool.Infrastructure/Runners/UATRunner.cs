using System.Runtime.InteropServices;
using System.Text;
using CliWrap;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;
using SteamUEDeployTool.Core.Validation;

namespace SteamUEDeployTool.Infrastructure.Runners;

public sealed class UATRunner : IBuildRunner
{
    public async Task<BuildResult> RunAsync(
        BuildProfile profile,
        EngineInfo engine,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var logs = new List<LogEntry>();

        try
        {
            var runUatPath = ProjectValidator.GetRunUatPath(engine.Path);

            var args = BuildArguments(profile);

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Info,
                $"Starting {Path.GetFileName(runUatPath)}...", "UAT"));

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Debug,
                $"Arguments: {args}", "UAT"));

            // UAT always calls CleanStagingDirectory before staging, regardless of -clean flag.
            // UE cooked files get ReadOnly attributes which causes Access Denied on Directory.Delete.
            PreCleanStagingDirectory(profile, logProgress);

            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            var result = await Cli.Wrap(runUatPath)
                .WithArguments(args)
                .WithWorkingDirectory(engine.Path)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                {
                    var entry = ParseUELine(line, stdOut);
                    logs.Add(entry);
                    logProgress?.Report(entry);
                }))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    var entry = ParseUELine(line, stdErr);
                    logs.Add(entry);
                    logProgress?.Report(entry);
                }))
                .ExecuteAsync(ct);

            var duration = DateTime.UtcNow - startTime;
            var success = result.ExitCode == 0;

            if (success)
            {
                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Success,
                    "Build completed successfully.", "UAT"));
            }

            return new BuildResult(
                success,
                ResolveOutputPath(profile),
                duration,
                result.ExitCode,
                logs,
                success ? null : $"Build failed with exit code {result.ExitCode}. Check logs.");
        }
        catch (OperationCanceledException)
        {
            var duration = DateTime.UtcNow - startTime;
            return new BuildResult(
                false, null, duration, -1, logs, "Build was cancelled.");
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            return new BuildResult(
                false, null, duration, -1, logs, ex.Message);
        }
    }

    private static string BuildArguments(BuildProfile profile)
    {
        var sb = new StringBuilder("BuildCookRun");

        sb.Append($" -project=\"{profile.UProjectPath}\"");
        sb.Append($" -platform={profile.Platform}");
        sb.Append($" -clientconfig={profile.BuildConfiguration}");

        if (profile.Cook)
            sb.Append(" -cook");

        if (profile.CleanBuild)
            sb.Append(" -clean");

        if (!string.IsNullOrWhiteSpace(profile.OutputPathOverride))
            sb.Append($" -archivedirectory=\"{profile.OutputPathOverride}\"");

        if (!string.IsNullOrWhiteSpace(profile.ExtraArgs))
        {
            sb.Append(' ');
            sb.Append(profile.ExtraArgs);
        }

        sb.Append(" -build -stage -pak -utf8output");

        return sb.ToString();
    }

    private static string? ResolveOutputPath(BuildProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.OutputPathOverride))
            return profile.OutputPathOverride;

        var projectDir = Path.GetDirectoryName(profile.UProjectPath);
        if (projectDir is null)
            return null;

        return Path.Combine(projectDir, "Saved", "StagedBuilds");
    }

    private static LogEntry ParseUELine(string line, StringBuilder buffer)
    {
        var level = LogLevel.Info;

        if (line.Contains("Error:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("FATAL", StringComparison.OrdinalIgnoreCase))
            level = LogLevel.Error;
        else if (line.Contains("Warning:", StringComparison.OrdinalIgnoreCase))
            level = LogLevel.Warning;
        else if (line.Contains("Success", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("BUILD SUCCESSFUL", StringComparison.OrdinalIgnoreCase))
            level = LogLevel.Success;

        return new LogEntry(DateTime.UtcNow, level, line, "UAT");
    }

    private static void PreCleanStagingDirectory(BuildProfile profile, IProgress<LogEntry>? logProgress)
    {
        string stagingDir;

        if (!string.IsNullOrWhiteSpace(profile.OutputPathOverride))
        {
            stagingDir = profile.OutputPathOverride;
        }
        else
        {
            var projectDir = Path.GetDirectoryName(profile.UProjectPath);
            if (projectDir is null) return;
            var platformDirName = MapPlatformToStagingDirName(profile.Platform);
            stagingDir = Path.Combine(projectDir, "Saved", "StagedBuilds", platformDirName);
        }

        if (!Directory.Exists(stagingDir)) return;

        try
        {
            logProgress?.Report(new LogEntry(DateTime.UtcNow, LogLevel.Info,
                $"Pre-cleaning staging directory (clearing read-only attributes): {stagingDir}", "UAT"));

            foreach (var file in Directory.EnumerateFiles(stagingDir, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
            }

            foreach (var dir in Directory.EnumerateDirectories(stagingDir, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(dir);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(dir, attrs & ~FileAttributes.ReadOnly);
            }
        }
        catch (Exception ex)
        {
            logProgress?.Report(new LogEntry(DateTime.UtcNow, LogLevel.Warning,
                $"Could not clear read-only attributes on staging directory: {ex.Message}", "UAT"));
        }
    }

    private static string MapPlatformToStagingDirName(Platform platform) => platform switch
    {
        Platform.Win64 => "Windows",
        Platform.Linux => "Linux",
        Platform.Mac   => "Mac",
        _              => platform.ToString()
    };
}
