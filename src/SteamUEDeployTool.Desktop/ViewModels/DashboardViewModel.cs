using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Services;

namespace SteamUEDeployTool.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IProfileRepository _profileRepository;

    [ObservableProperty]
    private ObservableCollection<PushProfile> _pushProfiles = [];

    [ObservableProperty]
    private PushProfile? _selectedPushProfile;

    [ObservableProperty]
    private BuildProfile? _detailsBuildProfile;

    [ObservableProperty]
    private DeployTarget? _detailsDeployTarget;

    [ObservableProperty]
    private string? _detailsAccountId;

    [ObservableProperty]
    private ObservableCollection<BuildProfile> _allBuildProfiles = [];

    [ObservableProperty]
    private ObservableCollection<DeployTarget> _allDeployTargets = [];

    [ObservableProperty]
    private BuildProfile? _newBuildProfile;

    [ObservableProperty]
    private DeployTarget? _newDeployTarget;

    [ObservableProperty]
    private string _newPushName = string.Empty;

    [ObservableProperty]
    private string _dashboardMessage = string.Empty;

    [ObservableProperty]
    private bool _showMessage;

    public DashboardViewModel(IProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var push = await _profileRepository.GetAllAsync<PushProfile>();
        PushProfiles = new ObservableCollection<PushProfile>(push);

        var builds = await _profileRepository.GetAllAsync<BuildProfile>();
        AllBuildProfiles = new ObservableCollection<BuildProfile>(builds);

        var deploys = await _profileRepository.GetAllAsync<DeployTarget>();
        AllDeployTargets = new ObservableCollection<DeployTarget>(deploys);
    }

    partial void OnSelectedPushProfileChanged(PushProfile? value)
    {
        if (value is not null)
            _ = RefreshDetailsAsync();
    }

    private async Task RefreshDetailsAsync()
    {
        if (SelectedPushProfile is null) return;

        DetailsBuildProfile = await _profileRepository.GetByIdAsync<BuildProfile>(
            SelectedPushProfile.BuildProfileId);

        DetailsDeployTarget = await _profileRepository.GetByIdAsync<DeployTarget>(
            SelectedPushProfile.DeployTargetId);

        DetailsAccountId = DetailsDeployTarget?.SteamAccountId;
    }

    [RelayCommand]
    private async Task CreatePushProfileAsync()
    {
        if (NewBuildProfile is null || NewDeployTarget is null)
        {
            DashboardMessage = "Select both a Build Profile and Deploy Target.";
            ShowMessage = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPushName))
        {
            DashboardMessage = "Enter a name for the Push Profile.";
            ShowMessage = true;
            return;
        }

        var profile = new PushProfile
        {
            Name = NewPushName,
            BuildProfileId = NewBuildProfile.Id,
            DeployTargetId = NewDeployTarget.Id
        };

        await _profileRepository.SaveAsync(profile);
        Log.Information("Push profile created: {Name} (Build={BuildId}, Deploy={DeployId})",
            profile.Name, profile.BuildProfileId, profile.DeployTargetId);

        NewPushName = string.Empty;
        NewBuildProfile = null;
        NewDeployTarget = null;
        DashboardMessage = $"Push profile '{profile.Name}' created. You can now use it in the Push tab.";
        ShowMessage = true;

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeletePushProfileAsync()
    {
        if (SelectedPushProfile is null) return;
        await _profileRepository.DeleteAsync<PushProfile>(SelectedPushProfile.Id);
        Log.Information("Push profile deleted: {Name}", SelectedPushProfile.Name);
        await LoadAsync();
    }
}
