using System.Diagnostics;
using CliWrap;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;
using SteamUEDeployTool.Core.Validation;

namespace SteamUEDeployTool.Infrastructure.Runners;

public sealed class SteamCmdRunner : ISteamDeployer, ISteamCmdLoginService
{
    private readonly IAccountStore _accountStore;
    private readonly ISecureCredentialStore _credentialStore;
    private readonly IVdfGenerator _vdfGenerator;
    private readonly string? _customSteamCmdPath;

    public SteamCmdRunner(
        IAccountStore accountStore,
        ISecureCredentialStore credentialStore,
        IVdfGenerator vdfGenerator,
        string? customSteamCmdPath = null)
    {
        _accountStore = accountStore;
        _credentialStore = credentialStore;
        _vdfGenerator = vdfGenerator;
        _customSteamCmdPath = customSteamCmdPath;
    }

    public bool HasCachedLogin()
    {
        var steamCmdPath = SteamCmdValidator.ResolveExecutablePath(_customSteamCmdPath);
        if (steamCmdPath is null) return false;
        var dir = Path.GetDirectoryName(steamCmdPath)!;
        return Directory.GetFiles(dir, "ssfn*").Length > 0;
    }

        public async Task<LoginResult> LoginAsync(
        string username,
        string password,
        Func<CancellationToken, Task<string>>? steamGuardProvider = null,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default)
    {
        var steamCmdPath = SteamCmdValidator.ResolveExecutablePath(_customSteamCmdPath);
        if (steamCmdPath is null)
            return new LoginResult(false, false, "steamcmd not found.");

        logProgress?.Report(new LogEntry(DateTime.UtcNow, LogLevel.Info,
            $"Launching steamcmd login for '{username}'...", "SteamCMD"));

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = steamCmdPath,
                Arguments = $"+login \"{username}\" \"{password}\" +quit",
                WorkingDirectory = Path.GetDirectoryName(steamCmdPath)!,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var outputBuilder = new System.Text.StringBuilder();
            var guardNeededTcs = new TaskCompletionSource<bool>();
            var resultTcs = new TaskCompletionSource<LoginResult>();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                var line = e.Data;
                outputBuilder.AppendLine(line);
                logProgress?.Report(ParseSteamCmdLine(line));

                if (!guardNeededTcs.Task.IsCompleted &&
                    (line.Contains("Steam Guard", StringComparison.OrdinalIgnoreCase)
                     || line.Contains("two-factor", StringComparison.OrdinalIgnoreCase)
                     || line.Contains("SteamGuard", StringComparison.OrdinalIgnoreCase)
                     || line.Contains("Enter the current code", StringComparison.OrdinalIgnoreCase)
                     || line.Contains("Please check your email", StringComparison.OrdinalIgnoreCase)
                     || (line.Contains("code", StringComparison.OrdinalIgnoreCase)
                         && line.Contains("email", StringComparison.OrdinalIgnoreCase))))
                {
                    guardNeededTcs.TrySetResult(true);
                }

                if (resultTcs.Task.IsCompleted) return;

                if (line.Contains("Steam Guard code:OK", StringComparison.OrdinalIgnoreCase))
                {
                    resultTcs.TrySetResult(new LoginResult(true, false, null));
                }
                else if (line.Contains("Steam Guard code:BAD", StringComparison.OrdinalIgnoreCase)
                         || line.Contains("Steam Guard code:FAIL", StringComparison.OrdinalIgnoreCase)
                         || line.Contains("Steam Guard code:ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    var reason = line.Contains("Rate Limit", StringComparison.OrdinalIgnoreCase)
                        ? "Rate limit exceeded. Wait 30-60 min, then request a new code."
                        : "Steam Guard code rejected. Request a fresh code from email.";
                    resultTcs.TrySetResult(new LoginResult(false, true, reason));
                }
                else if (line.Contains("InvalidPassword", StringComparison.OrdinalIgnoreCase))
                {
                    resultTcs.TrySetResult(new LoginResult(false, false, "Invalid username or password."));
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                logProgress?.Report(ParseSteamCmdLine(e.Data));
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var guardNeeded = false;
            try { guardNeeded = await guardNeededTcs.Task.WaitAsync(TimeSpan.FromSeconds(8), ct); }
            catch (TimeoutException) { if (!process.HasExited) guardNeeded = true; }

            if (guardNeeded && steamGuardProvider is not null)
            {
                logProgress?.Report(new LogEntry(DateTime.UtcNow, LogLevel.Info,
                    "Steam Guard required. Waiting for code...", "SteamCMD"));

                string guardCode;
                try { guardCode = await steamGuardProvider(ct); }
                catch (OperationCanceledException)
                { if (!process.HasExited) process.Kill(); return new LoginResult(false, false, "Cancelled."); }

                if (string.IsNullOrWhiteSpace(guardCode))
                { if (!process.HasExited) process.Kill(); return new LoginResult(false, false, "Code was empty."); }

                logProgress?.Report(new LogEntry(DateTime.UtcNow, LogLevel.Info,
                    "Sending guard code to Steam...", "SteamCMD"));

                await process.StandardInput.WriteLineAsync(guardCode);
                await process.StandardInput.FlushAsync();
                process.StandardInput.Close();
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var completedTask = await Task.WhenAny(resultTcs.Task,
                Task.Delay(Timeout.Infinite, linkedCts.Token));

            if (completedTask == resultTcs.Task)
            {
                var result = await resultTcs.Task;
                if (!process.HasExited) process.Kill();
                if (result.Success)
                    logProgress?.Report(new LogEntry(DateTime.UtcNow, LogLevel.Success,
                        "Login successful. SSFN cached by steamcmd.", "SteamCMD"));
                return result;
            }

            if (!process.HasExited) process.Kill();

            var lastLines = outputBuilder.ToString();
            if (lastLines.Contains("InvalidPassword", StringComparison.OrdinalIgnoreCase))
                return new LoginResult(false, false, "Invalid username or password.");
            if (lastLines.Contains("RateLimit", StringComparison.OrdinalIgnoreCase))
                return new LoginResult(false, false, "Rate limited. Wait before retrying.");
            if (lastLines.Contains("SteamGuard", StringComparison.OrdinalIgnoreCase)
                || lastLines.Contains("incorrect", StringComparison.OrdinalIgnoreCase))
                return new LoginResult(false, true, "Guard code incorrect or expired.");

            return new LoginResult(false, false, "Login timed out. Check network.");
        }
        catch (OperationCanceledException)
        { return new LoginResult(false, false, "Login cancelled."); }
        catch (Exception ex)
        { return new LoginResult(false, false, $"Login error: {ex.Message}"); }
    }
public async Task<DeployResult> DeployAsync(
        DeployTarget target,
        string buildPath,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var logs = new List<LogEntry>();

        try
        {
            var steamCmdPath = SteamCmdValidator.ResolveExecutablePath(_customSteamCmdPath);
            if (steamCmdPath is null)
            {
                return new DeployResult(
                    false, null, DateTime.UtcNow - startTime, -1, logs,
                    "steamcmd not found. Install SteamCMD or configure the path.");
            }

            string? accountId = target.SteamAccountId;
            string? username = null;
            string? password = null;

            if (!string.IsNullOrWhiteSpace(accountId))
            {
                var account = await _accountStore.GetByIdAsync(accountId, ct);
                if (account is not null)
                {
                    username = account.Username;
                    password = await _credentialStore.GetAsync(accountId, ct);
                }
            }

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Info,
                "Generating VDF deployment scripts...", "SteamCMD"));

            var vdfFiles = _vdfGenerator.Generate(target, buildPath);

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Debug,
                $"App build VDF: {vdfFiles.AppBuildVdfPath}", "SteamCMD"));

            foreach (var depot in target.Depots)
            {
                var actualRoot = string.IsNullOrWhiteSpace(depot.ContentRoot)
                    ? buildPath
                    : depot.ContentRoot;

                logProgress?.Report(new LogEntry(
                    DateTime.UtcNow, LogLevel.Info,
                    $"Depot {depot.DepotId} ContentRoot: \"{actualRoot}\"", "SteamCMD"));

                if (Directory.Exists(actualRoot))
                {
                    try
                    {
                        var fileCount = Directory.GetFiles(actualRoot, "*", SearchOption.AllDirectories).Length;
                        logProgress?.Report(new LogEntry(
                            DateTime.UtcNow, LogLevel.Info,
                            $"Depot {depot.DepotId}: {fileCount} files found in ContentRoot", "SteamCMD"));
                    }
                    catch
                    {
                        logProgress?.Report(new LogEntry(
                            DateTime.UtcNow, LogLevel.Warning,
                            $"Depot {depot.DepotId}: Unable to enumerate ContentRoot", "SteamCMD"));
                    }
                }
                else
                {
                    logProgress?.Report(new LogEntry(
                        DateTime.UtcNow, LogLevel.Warning,
                        $"Depot {depot.DepotId}: ContentRoot directory does not exist: \"{actualRoot}\"", "SteamCMD"));
                }
            }

            var loginArgs = BuildLoginArgs(username, password);
            var fullArgs = $"{loginArgs} +run_app_build \"{vdfFiles.AppBuildVdfPath}\" +quit";

            if (logProgress is not null)
            {
                try
                {
                    var appVdfContent = File.ReadAllText(vdfFiles.AppBuildVdfPath);
                    logProgress.Report(new LogEntry(
                        DateTime.UtcNow, LogLevel.Debug,
                        $"App VDF content:\n{appVdfContent}", "SteamCMD"));
                }
                catch { }

                foreach (var depotPath in vdfFiles.DepotVdfPaths)
                {
                    try
                    {
                        var depotVdfContent = File.ReadAllText(depotPath);
                        logProgress.Report(new LogEntry(
                            DateTime.UtcNow, LogLevel.Debug,
                            $"Depot VDF ({Path.GetFileName(depotPath)}):\n{depotVdfContent}", "SteamCMD"));
                    }
                    catch { }
                }
            }

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Info,
                $"Uploading build to Steam AppID {target.AppId}, branch '{target.BranchName}'...",
                "SteamCMD"));

            var result = await Cli.Wrap(steamCmdPath)
                .WithWorkingDirectory(Path.GetDirectoryName(steamCmdPath)!)
                .WithArguments(fullArgs)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                {
                    var entry = ParseSteamCmdLine(line);
                    logs.Add(entry);
                    logProgress?.Report(entry);
                }))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    var entry = ParseSteamCmdLine(line);
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
                    $"Deploy to Steam AppID {target.AppId} completed.", "SteamCMD"));
            }

            CleanupVdfFiles(vdfFiles);

            return new DeployResult(
                success,
                null,
                duration,
                result.ExitCode,
                logs,
                success ? null : $"Deploy failed with exit code {result.ExitCode}.");
        }
        catch (OperationCanceledException)
        {
            var duration = DateTime.UtcNow - startTime;
            return new DeployResult(
                false, null, duration, -1, logs, "Deploy was cancelled.");
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            return new DeployResult(
                false, null, duration, -1, logs, ex.Message);
        }
    }

    private static string BuildLoginArgs(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "+login anonymous";

        if (string.IsNullOrWhiteSpace(password))
            return $"+login \"{username}\"";

        return $"+login \"{username}\" \"{password}\"";
    }

    private static void CleanupVdfFiles(GeneratedVdfFiles vdfFiles)
    {
        try
        {
            var dir = Path.GetDirectoryName(vdfFiles.AppBuildVdfPath);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static LogEntry ParseSteamCmdLine(string line)
    {
        var level = LogLevel.Debug;

        if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
            || line.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
            level = LogLevel.Error;
        else if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase))
            level = LogLevel.Warning;
        else if (line.Contains("Success", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("OK", StringComparison.OrdinalIgnoreCase))
            level = LogLevel.Success;

        return new LogEntry(DateTime.UtcNow, level, line, "SteamCMD");
    }
}
