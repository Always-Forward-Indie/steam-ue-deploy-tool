using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Core.Services;

public sealed class PushPipelineService
{
    private readonly BuildOrchestrator _buildOrchestrator;
    private readonly DeployOrchestrator _deployOrchestrator;
    private readonly IProfileRepository _profileRepository;

    public PushPipelineService(
        BuildOrchestrator buildOrchestrator,
        DeployOrchestrator deployOrchestrator,
        IProfileRepository profileRepository)
    {
        _buildOrchestrator = buildOrchestrator;
        _deployOrchestrator = deployOrchestrator;
        _profileRepository = profileRepository;
    }

    public async Task<PushResult> PushAsync(
        PushProfile pushProfile,
        IProgress<LogEntry>? logProgress = null,
        IProgress<PushProgress>? stageProgress = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        var buildProfile = await _profileRepository.GetByIdAsync<BuildProfile>(
            pushProfile.BuildProfileId, ct);

        if (buildProfile is null)
        {
            return new PushResult(false, null, null, TimeSpan.Zero);
        }

        var deployTarget = await _profileRepository.GetByIdAsync<DeployTarget>(
            pushProfile.DeployTargetId, ct);

        if (deployTarget is null)
        {
            return new PushResult(false, null, null, TimeSpan.Zero);
        }

        ReportStage(stageProgress, PushStage.Validating, 0, "Validating profile...");
        logProgress?.Report(new LogEntry(
            DateTime.UtcNow, LogLevel.Info,
            $"Push profile: {pushProfile.Name}"));

        var buildValidation = await _buildOrchestrator.ValidateAsync(buildProfile, ct);
        if (!buildValidation.IsValid)
        {
            ReportStage(stageProgress, PushStage.Failed, 0,
                $"Build validation failed: {string.Join("; ", buildValidation.Errors)}");

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Error,
                $"Build validation failed: {string.Join("; ", buildValidation.Errors)}"));

            return new PushResult(false, null, null, DateTime.UtcNow - startTime);
        }

        var deployValidation = await _deployOrchestrator.ValidateAsync(deployTarget, ct);
        if (!deployValidation.IsValid)
        {
            ReportStage(stageProgress, PushStage.Failed, 0,
                $"Deploy validation failed: {string.Join("; ", deployValidation.Errors)}");

            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Error,
                $"Deploy validation failed: {string.Join("; ", deployValidation.Errors)}"));

            return new PushResult(false, null, null, DateTime.UtcNow - startTime);
        }

        ReportStage(stageProgress, PushStage.Building, 5, "Building project...");

        BuildResult? buildResult = null;

        try
        {
            buildResult = await _buildOrchestrator.BuildAsync(buildProfile, logProgress, ct);

            if (!buildResult.Success)
            {
                ReportStage(stageProgress, PushStage.Failed, 50,
                    $"Build failed: {buildResult.ErrorMessage}");

                return new PushResult(
                    false, buildResult, null, DateTime.UtcNow - startTime);
            }
        }
        catch (OperationCanceledException)
        {
            ReportStage(stageProgress, PushStage.Failed, 50, "Build cancelled.");
            return new PushResult(false, buildResult, null, DateTime.UtcNow - startTime);
        }

        var buildPath = !string.IsNullOrWhiteSpace(buildProfile.OutputPathOverride)
            ? buildProfile.OutputPathOverride
            : buildResult.OutputPath;

        if (string.IsNullOrWhiteSpace(buildPath) || !Directory.Exists(buildPath))
        {
            ReportStage(stageProgress, PushStage.Failed, 50,
                "Build output path not found. Set OutputPathOverride or check the build output.");

            return new PushResult(
                false, buildResult, null, DateTime.UtcNow - startTime);
        }

        ReportStage(stageProgress, PushStage.Deploying, 60, "Deploying to Steam...");

        DeployResult? deployResult = null;

        try
        {
            deployResult = await _deployOrchestrator.DeployAsync(
                deployTarget, buildPath, logProgress, ct);

            if (!deployResult.Success)
            {
                ReportStage(stageProgress, PushStage.Failed, 90,
                    $"Deploy failed: {deployResult.ErrorMessage}");

                return new PushResult(
                    false, buildResult, deployResult, DateTime.UtcNow - startTime);
            }
        }
        catch (OperationCanceledException)
        {
            ReportStage(stageProgress, PushStage.Failed, 90, "Deploy cancelled.");
            return new PushResult(
                false, buildResult, deployResult, DateTime.UtcNow - startTime);
        }

        ReportStage(stageProgress, PushStage.Completed, 100, "Push completed successfully.");

        logProgress?.Report(new LogEntry(
            DateTime.UtcNow, LogLevel.Success,
            $"Push completed in {(DateTime.UtcNow - startTime).TotalMinutes:F1} minutes."));

        return new PushResult(
            true, buildResult, deployResult, DateTime.UtcNow - startTime);
    }

    private static void ReportStage(
        IProgress<PushProgress>? progress,
        PushStage stage,
        double percent,
        string action)
    {
        progress?.Report(new PushProgress
        {
            Stage = stage,
            Percent = percent,
            CurrentAction = action
        });
    }
}
