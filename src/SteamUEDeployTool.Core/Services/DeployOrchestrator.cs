using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Validation;

namespace SteamUEDeployTool.Core.Services;

public sealed class DeployOrchestrator
{
    private readonly ISteamDeployer _steamDeployer;
    private readonly IAccountStore _accountStore;
    private readonly IVdfGenerator _vdfGenerator;

    public DeployOrchestrator(
        ISteamDeployer steamDeployer,
        IAccountStore accountStore,
        IVdfGenerator vdfGenerator)
    {
        _steamDeployer = steamDeployer;
        _accountStore = accountStore;
        _vdfGenerator = vdfGenerator;
    }

    public async Task<ValidationResult> ValidateAsync(
        DeployTarget target,
        CancellationToken ct = default)
    {
        var errors = new List<string>();

        if (target.AppId == 0)
            errors.Add("Steam AppID is required.");

        if (target.Depots.Count == 0)
            errors.Add("At least one depot is required.");

        foreach (var depot in target.Depots)
        {
            if (depot.DepotId == 0)
                errors.Add("Depot ID is required for all depots.");

            if (string.IsNullOrWhiteSpace(depot.ContentRoot))
                errors.Add($"Content root is required for depot {depot.DepotId}.");

            if (!Directory.Exists(depot.ContentRoot))
                errors.Add($"Content root directory does not exist: '{depot.ContentRoot}'");

            if (!string.IsNullOrWhiteSpace(depot.ContentRoot)
                && Directory.Exists(depot.ContentRoot)
                && !Directory.EnumerateFileSystemEntries(depot.ContentRoot).Any())
                errors.Add($"Content root directory is empty: '{depot.ContentRoot}'");
        }

        if (string.IsNullOrWhiteSpace(target.BranchName))
            errors.Add("Branch name is required.");

        if (!string.IsNullOrWhiteSpace(target.SteamAccountId))
        {
            var account = await _accountStore.GetByIdAsync(target.SteamAccountId, ct);
            if (account is null)
                errors.Add($"Steam account '{target.SteamAccountId}' not found.");
            else if (!account.HasSsfn)
                errors.Add($"Steam account '{account.Username}' has no cached login. Go to Accounts → Login first.");
        }

        var steamCmdValidation = SteamCmdValidator.ValidateInstallation();
        if (!steamCmdValidation.IsValid)
            errors.AddRange(steamCmdValidation.Errors);

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    public async Task<DeployResult> DeployAsync(
        DeployTarget target,
        string buildPath,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(buildPath))
        {
            return new DeployResult(
                false, null, TimeSpan.Zero, -1, [],
                $"Build output path not found: '{buildPath}'");
        }

        logProgress?.Report(new LogEntry(
            DateTime.UtcNow, Core.Models.Enums.LogLevel.Info,
            $"Deploying to Steam AppID {target.AppId}, branch '{target.BranchName}'..."));

        return await _steamDeployer.DeployAsync(target, buildPath, logProgress, ct);
    }
}
