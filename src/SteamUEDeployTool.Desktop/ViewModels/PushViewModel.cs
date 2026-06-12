using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;
using SteamUEDeployTool.Core.Services;

namespace SteamUEDeployTool.Desktop.ViewModels;

public partial class PushViewModel : ViewModelBase
{
    private readonly IProfileRepository _profileRepository;
    private readonly PushPipelineService _pipelineService;
    private readonly BuildOrchestrator _buildOrchestrator;
    private readonly DeployOrchestrator _deployOrchestrator;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private ObservableCollection<PushProfile> _pushProfiles = [];

    [ObservableProperty]
    private PushProfile? _selectedPushProfile;

    [ObservableProperty]
    private ObservableCollection<BuildProfile> _buildProfiles = [];

    [ObservableProperty]
    private BuildProfile? _selectedBuildProfile;

    [ObservableProperty]
    private ObservableCollection<DeployTarget> _deployTargets = [];

    [ObservableProperty]
    private DeployTarget? _selectedDeployTarget;

    [ObservableProperty]
    private PushStage _currentStage = PushStage.Idle;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _currentAction = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _resultMessage = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _resultSuccess;

    [ObservableProperty]
    private ObservableCollection<string> _logs = [];

    [ObservableProperty]
    private string _deployBuildDescription = string.Empty;

    public PushViewModel(
        IProfileRepository profileRepository,
        PushPipelineService pipelineService,
        BuildOrchestrator buildOrchestrator,
        DeployOrchestrator deployOrchestrator)
    {
        _profileRepository = profileRepository;
        _pipelineService = pipelineService;
        _buildOrchestrator = buildOrchestrator;
        _deployOrchestrator = deployOrchestrator;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var push = await _profileRepository.GetAllAsync<PushProfile>();
        PushProfiles = new ObservableCollection<PushProfile>(push);

        var builds = await _profileRepository.GetAllAsync<BuildProfile>();
        BuildProfiles = new ObservableCollection<BuildProfile>(builds);

        var deploys = await _profileRepository.GetAllAsync<DeployTarget>();
        DeployTargets = new ObservableCollection<DeployTarget>(deploys);
    }

    [RelayCommand]
    private async Task BuildOnlyAsync()
    {
        if (SelectedBuildProfile is null || IsRunning) return;
        IsRunning = true; HasResult = false; Logs.Clear();
        ResultMessage = string.Empty;
        CurrentStage = PushStage.Building;
        CurrentAction = "Starting build...";
        ProgressPercent = 0;

        _cts = new CancellationTokenSource();
        var logProgress = new Progress<LogEntry>(entry =>
        {
            Logs.Add($"[{entry.Level}] {entry.Message}");
            WriteToLogFile(entry);
            CurrentAction = entry.Message;
            if (ProgressPercent < 90) ProgressPercent += 0.5;
        });

        try
        {
            Log.Information("CookAndPackage started: {Profile} ({Platform}/{Config})",
                SelectedBuildProfile.Name, SelectedBuildProfile.Platform, SelectedBuildProfile.BuildConfiguration);
            var result = await _buildOrchestrator.BuildAsync(SelectedBuildProfile, logProgress, _cts.Token);
            HasResult = true;
            ResultSuccess = result.Success;
            ProgressPercent = 100;
            CurrentStage = result.Success ? PushStage.Completed : PushStage.Failed;
            ResultMessage = result.Success
                ? $"Build completed in {result.Duration.TotalMinutes:F1} min. Output: {result.OutputPath}"
                : $"Build failed: {result.ErrorMessage}";
        }
        catch (OperationCanceledException)
        { HasResult = true; ResultSuccess = false; CurrentStage = PushStage.Failed; ResultMessage = "Build cancelled."; }
        catch (Exception ex)
        { HasResult = true; ResultSuccess = false; CurrentStage = PushStage.Failed; ResultMessage = $"Error: {ex.Message}"; }
        finally
        { IsRunning = false; _cts?.Dispose(); _cts = null; }
    }

    [RelayCommand]
    private async Task DeployOnlyAsync()
    {
        if (SelectedDeployTarget is null || IsRunning) return;
        if (SelectedBuildProfile is null) return;

        IsRunning = true; HasResult = false; Logs.Clear();
        ResultMessage = string.Empty;
        CurrentStage = PushStage.Deploying;
        CurrentAction = "Starting deploy...";
        ProgressPercent = 0;

        _cts = new CancellationTokenSource();
        var logProgress = new Progress<LogEntry>(entry =>
        {
            Logs.Add($"[{entry.Level}] {entry.Message}");
            WriteToLogFile(entry);
            CurrentAction = entry.Message;
            if (ProgressPercent < 90) ProgressPercent += 0.5;
        });

        try
        {
            var buildPath = !string.IsNullOrWhiteSpace(SelectedBuildProfile.OutputPathOverride)
                ? SelectedBuildProfile.OutputPathOverride
                : Path.Combine(Path.GetDirectoryName(SelectedBuildProfile.UProjectPath) ?? ".", "Saved", "StagedBuilds");

            Log.Information("DeployOnly started: {Target} -> Steam AppID {AppId} from {Path}",
                SelectedDeployTarget.Name, SelectedDeployTarget.AppId, buildPath);
            Logs.Add($"[Info] Deploying from: {buildPath}");

            var resolvedTarget = ResolveDepotContentRoots(SelectedDeployTarget, buildPath);

            if (!string.IsNullOrWhiteSpace(DeployBuildDescription))
                resolvedTarget.BuildDescription = DeployBuildDescription;

            var validation = await _deployOrchestrator.ValidateAsync(resolvedTarget);
            if (!validation.IsValid)
            {
                HasResult = true;
                ResultSuccess = false;
                ProgressPercent = 100;
                CurrentStage = PushStage.Failed;
                ResultMessage = string.Join("\n", validation.Errors);
                foreach (var error in validation.Errors)
                    Logs.Add($"[Error] {error}");
                return;
            }

            var result = await _deployOrchestrator.DeployAsync(
                resolvedTarget, buildPath, logProgress, _cts.Token);

            HasResult = true;
            ResultSuccess = result.Success;
            ProgressPercent = 100;
            CurrentStage = result.Success ? PushStage.Completed : PushStage.Failed;
            ResultMessage = result.Success
                ? $"Deploy completed in {result.Duration.TotalMinutes:F1} min."
                : $"Deploy failed: {result.ErrorMessage}";
        }
        catch (OperationCanceledException)
        { HasResult = true; ResultSuccess = false; CurrentStage = PushStage.Failed; ResultMessage = "Deploy cancelled."; }
        catch (Exception ex)
        { HasResult = true; ResultSuccess = false; CurrentStage = PushStage.Failed; ResultMessage = $"Error: {ex.Message}"; }
        finally
        { IsRunning = false; _cts?.Dispose(); _cts = null; }
    }

    [RelayCommand]
    private async Task StartPushAsync()
    {
        if (SelectedPushProfile is null || IsRunning)
            return;

        IsRunning = true;
        HasResult = false;
        ResultMessage = string.Empty;
        Logs.Clear();
        CurrentStage = PushStage.Idle;
        ProgressPercent = 0;

        _cts = new CancellationTokenSource();

        var stageProgress = new Progress<PushProgress>(p =>
        {
            CurrentStage = p.Stage;
            ProgressPercent = p.Percent;
            CurrentAction = p.CurrentAction ?? string.Empty;
            Log.Information("Push stage: {Stage} ({Percent:F0}%) — {Action}",
                p.Stage, p.Percent, p.CurrentAction);
        });

        var logProgress = new Progress<LogEntry>(entry =>
        {
            Logs.Add($"[{entry.Level}] {entry.Message}");
            WriteToLogFile(entry);
        });

        try
        {
            Log.Information("Push started: {Profile}", SelectedPushProfile.Name);
            var result = await _pipelineService.PushAsync(
                SelectedPushProfile, logProgress, stageProgress, _cts.Token);

            HasResult = true;
            ResultSuccess = result.Success;
            ResultMessage = result.Success
                ? $"Push completed in {result.Duration.TotalMinutes:F1} minutes."
                : $"Push failed: {result.DeployResult?.ErrorMessage ?? result.BuildResult?.ErrorMessage ?? "Unknown error"}";
        }
        catch (OperationCanceledException)
        {
            HasResult = true;
            ResultSuccess = false;
            ResultMessage = "Push cancelled by user.";
        }
        catch (Exception ex)
        {
            HasResult = true;
            ResultSuccess = false;
            ResultMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelPush()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private async Task CopyErrorsAsync()
    {
        var msg = HasResult ? ResultMessage : string.Join("\n", Logs);
        if (!string.IsNullOrWhiteSpace(msg))
        {
            var topLevel = TopLevelHelper.GetTopLevel();
            if (topLevel?.Clipboard is not null)
                await topLevel.Clipboard.SetTextAsync(msg);
        }
    }

    [RelayCommand]
    private void OpenBuildFolder()
    {
        if (SelectedBuildProfile is null) return;

        var path = !string.IsNullOrWhiteSpace(SelectedBuildProfile.OutputPathOverride)
            ? SelectedBuildProfile.OutputPathOverride
            : Path.Combine(
                Path.GetDirectoryName(SelectedBuildProfile.UProjectPath) ?? ".",
                "Saved", "StagedBuilds");

        var target = path;
        while (!string.IsNullOrEmpty(target) && !Directory.Exists(target))
            target = Path.GetDirectoryName(target);

        if (!string.IsNullOrEmpty(target))
            Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamUEDeployTool",
            "logs");

        if (Directory.Exists(logDir))
            Process.Start(new ProcessStartInfo("explorer.exe", logDir) { UseShellExecute = true });
    }

    private static DeployTarget ResolveDepotContentRoots(DeployTarget target, string buildPath)
    {
        return new DeployTarget
        {
            Id = target.Id,
            Name = target.Name,
            AppId = target.AppId,
            Depots = target.Depots.ConvertAll(d =>
                new SteamDepot
                {
                    DepotId = d.DepotId,
                    ContentRoot = string.IsNullOrWhiteSpace(d.ContentRoot) ? buildPath : d.ContentRoot,
                    Mappings = d.Mappings.ConvertAll(m => new FileMapping(m.LocalPath, m.DepotPath, m.Recursive))
                }),
            BranchName = target.BranchName,
            SetLiveAfterUpload = target.SetLiveAfterUpload,
            BuildDescription = target.BuildDescription,
            SteamAccountId = target.SteamAccountId,
            CreatedAt = target.CreatedAt,
            ModifiedAt = target.ModifiedAt
        };
    }

    private static void WriteToLogFile(LogEntry entry)
    {
        var msg = $"[{entry.Source}] {entry.Message}";
        switch (entry.Level)
        {
            case LogLevel.Error: Log.Error(msg); break;
            case LogLevel.Warning: Log.Warning(msg); break;
            case LogLevel.Debug: Log.Debug(msg); break;
            case LogLevel.Success: Log.Information(msg); break;
            default: Log.Information(msg); break;
        }
    }
}
