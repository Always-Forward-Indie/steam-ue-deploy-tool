using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;
using SteamUEDeployTool.Core.Validation;

namespace SteamUEDeployTool.Core.Services;

public sealed class BuildOrchestrator
{
    private readonly IBuildRunner _buildRunner;
    private readonly IEngineResolver _engineResolver;
    private readonly IVCRedistBundler _vcredistBundler;

    public BuildOrchestrator(
        IBuildRunner buildRunner,
        IEngineResolver engineResolver,
        IVCRedistBundler vcredistBundler)
    {
        _buildRunner = buildRunner;
        _engineResolver = engineResolver;
        _vcredistBundler = vcredistBundler;
    }

    public async Task<ValidationResult> ValidateAsync(
        BuildProfile profile,
        CancellationToken ct = default)
    {
        var uprojectResult = ProjectValidator.ValidateUProjectFile(profile.UProjectPath);
        if (!uprojectResult.IsValid)
            return uprojectResult;

        string? association = null;
        try
        {
            var json = await File.ReadAllTextAsync(profile.UProjectPath, ct);
            var node = System.Text.Json.Nodes.JsonNode.Parse(json);
            association = node?["EngineAssociation"]?.GetValue<string>();
        }
        catch { }

        EngineInfo? engine;
        if (!string.IsNullOrWhiteSpace(profile.CustomEnginePath))
        {
            engine = await _engineResolver.ResolveFromPathAsync(
                profile.CustomEnginePath, ct);
        }
        else
        {
            engine = await _engineResolver.ResolveFromUProjectAsync(
                profile.UProjectPath, ct);
        }

        if (engine is null)
        {
            var detail = association is not null
                ? $"Engine association '{association}' could not be resolved."
                : "The .uproject file has no EngineAssociation field.";

            return ValidationResult.Failure(new[]
            {
                detail,
                "Possible causes:",
                "- Engine is not installed (launcher builds need Epic Games Launcher registry entries)",
                "- EngineAssociation references a custom path that doesn't exist",
                "Fix:",
                "- Set 'Custom Engine Path' to your engine folder (e.g. C:\\Program Files\\Epic Games\\UE_5.4)"
            });
        }

        var runUatPath = ProjectValidator.GetRunUatPath(engine.Path);
        if (!File.Exists(runUatPath))
        {
            return ValidationResult.Failure(
                $"RunUAT not found at '{runUatPath}'. Engine installation may be incomplete.");
        }

        return ValidationResult.Success();
    }

    public async Task<EngineInfo?> ResolveEngineAsync(
        BuildProfile profile,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(profile.CustomEnginePath))
        {
            return await _engineResolver.ResolveFromPathAsync(
                profile.CustomEnginePath, ct);
        }

        return await _engineResolver.ResolveFromUProjectAsync(
            profile.UProjectPath, ct);
    }

    public async Task<BuildResult> BuildAsync(
        BuildProfile profile,
        IProgress<LogEntry>? logProgress = null,
        CancellationToken ct = default)
    {
        var engine = await ResolveEngineAsync(profile, ct);
        if (engine is null)
        {
            return new BuildResult(
                false, null, TimeSpan.Zero, -1, [],
                "Engine not resolved. Validate the profile first.");
        }

        logProgress?.Report(new LogEntry(
            DateTime.UtcNow, Core.Models.Enums.LogLevel.Info,
            $"Engine: {engine.Path} (v{engine.Version}, {engine.Type})"));

        logProgress?.Report(new LogEntry(
            DateTime.UtcNow, Core.Models.Enums.LogLevel.Info,
            $"Building {profile.Platform}/{profile.BuildConfiguration}..."));

        var buildResult = await _buildRunner.RunAsync(profile, engine, logProgress, ct);

        if (buildResult.Success
            && profile.Platform == Platform.Win64
            && profile.BundleVCRedist
            && !string.IsNullOrWhiteSpace(buildResult.OutputPath))
        {
            logProgress?.Report(new LogEntry(
                DateTime.UtcNow, LogLevel.Info,
                "Bundling VC++ Redist...", nameof(BuildOrchestrator)));

            await _vcredistBundler.BundleAsync(engine, buildResult.OutputPath, logProgress, ct);
        }

        return buildResult;
    }
}
