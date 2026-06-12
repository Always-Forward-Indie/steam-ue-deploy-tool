using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Services;

namespace SteamUEDeployTool.Desktop.ViewModels;

public partial class DeployConfigViewModel : ViewModelBase
{
    private readonly IProfileRepository _profileRepository;
    private readonly IAccountStore _accountStore;
    private readonly DeployOrchestrator _deployOrchestrator;

    [ObservableProperty]
    private ObservableCollection<DeployTarget> _profiles = [];

    [ObservableProperty]
    private DeployTarget? _selectedProfile;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _appIdText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DepotEditorViewModel> _depots = [];

    [ObservableProperty]
    private DepotEditorViewModel? _selectedDepot;

    [ObservableProperty]
    private string _branchName = "default";

    [ObservableProperty]
    private bool _setLiveAfterUpload;

    [ObservableProperty]
    private string _buildDescription = string.Empty;

    [ObservableProperty]
    private SteamAccount? _selectedAccount;

    [ObservableProperty]
    private ObservableCollection<SteamAccount> _accounts = [];

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isValid;

    public DeployConfigViewModel(
        IProfileRepository profileRepository,
        IAccountStore accountStore,
        DeployOrchestrator deployOrchestrator)
    {
        _profileRepository = profileRepository;
        _accountStore = accountStore;
        _deployOrchestrator = deployOrchestrator;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var all = await _profileRepository.GetAllAsync<DeployTarget>();
        Profiles = new ObservableCollection<DeployTarget>(all);

        var accounts = await _accountStore.GetAllAsync();
        Accounts = new ObservableCollection<SteamAccount>(accounts);
    }

    partial void OnSelectedProfileChanged(DeployTarget? value)
    {
        if (value is null) return;

        Name = value.Name;
        AppIdText = value.AppId.ToString();
        BranchName = value.BranchName;
        SetLiveAfterUpload = value.SetLiveAfterUpload;
        BuildDescription = value.BuildDescription;
        SelectedAccount = Accounts.FirstOrDefault(a => a.Id == value.SteamAccountId);

        Depots = new ObservableCollection<DepotEditorViewModel>(
            value.Depots.Select(d => new DepotEditorViewModel
            {
                DepotIdText = d.DepotId.ToString(),
                ContentRoot = d.ContentRoot,
                LocalPath = d.Mappings.FirstOrDefault()?.LocalPath ?? "*",
                DepotPath = d.Mappings.FirstOrDefault()?.DepotPath ?? ".",
                Recursive = d.Mappings.FirstOrDefault()?.Recursive ?? true
            }));
    }

    [RelayCommand]
    private void AddDepot()
    {
        Depots.Add(new DepotEditorViewModel { DepotIdText = string.Empty, ContentRoot = string.Empty });
    }

    [RelayCommand]
    private void RemoveDepot(DepotEditorViewModel? depot)
    {
        if (depot is not null)
            Depots.Remove(depot);
    }

    [RelayCommand]
    private async Task BrowseContentRootAsync(DepotEditorViewModel? depot)
    {
        if (depot is null) return;

        var topLevel = TopLevelHelper.GetTopLevel();
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Content Root Directory",
                AllowMultiple = false
            });

        if (folders.Count > 0)
            depot.ContentRoot = folders[0].Path.LocalPath;
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        var target = CreateTargetFromForm();
        var result = await _deployOrchestrator.ValidateAsync(target);

        if (result.IsValid)
        {
            ValidationMessage = "Valid deployment configuration.";
            IsValid = true;
        }
        else
        {
            ValidationMessage = string.Join("\n", result.Errors);
            IsValid = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var target = CreateTargetFromForm();

        if (SelectedProfile is not null)
            target.Id = SelectedProfile.Id;
        else
            target.CreatedAt = DateTime.UtcNow;

        target.ModifiedAt = DateTime.UtcNow;

        await _profileRepository.SaveAsync(target);
        Log.Information("Deploy target saved: {Name} (AppID={AppId}, Branch={Branch})",
            target.Name, target.AppId, target.BranchName);
        await LoadAsync();

        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == target.Id);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProfile is null) return;
        await _profileRepository.DeleteAsync<DeployTarget>(SelectedProfile.Id);
        Log.Information("Deploy target deleted: {Name}", SelectedProfile.Name);
        ClearForm();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CopyErrorsAsync()
    {
        if (!string.IsNullOrWhiteSpace(ValidationMessage))
        {
            var topLevel = TopLevelHelper.GetTopLevel();
            if (topLevel?.Clipboard is not null)
                await topLevel.Clipboard.SetTextAsync(ValidationMessage);
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        SelectedProfile = null;
        Name = string.Empty;
        AppIdText = string.Empty;
        Depots = [];
        BranchName = "default";
        SetLiveAfterUpload = false;
        BuildDescription = string.Empty;
        SelectedAccount = null;
        ValidationMessage = string.Empty;
        IsValid = false;
    }

    private DeployTarget CreateTargetFromForm()
    {
        uint.TryParse(AppIdText, out var appId);

        return new DeployTarget
        {
            Id = SelectedProfile?.Id ?? Guid.NewGuid(),
            Name = Name,
            AppId = appId,
            Depots = Depots.Select(d =>
            {
                uint.TryParse(d.DepotIdText, out var depotId);
                return new SteamDepot
                {
                    DepotId = depotId,
                    ContentRoot = d.ContentRoot,
                    Mappings =
                    [
                        new FileMapping(d.LocalPath, d.DepotPath, d.Recursive)
                    ]
                };
            }).ToList(),
            BranchName = BranchName,
            SetLiveAfterUpload = SetLiveAfterUpload,
            BuildDescription = BuildDescription,
            SteamAccountId = SelectedAccount?.Id
        };
    }
}

public partial class DepotEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _depotIdText = string.Empty;

    [ObservableProperty]
    private string _contentRoot = string.Empty;

    [ObservableProperty]
    private string _localPath = "*";

    [ObservableProperty]
    private string _depotPath = ".";

    [ObservableProperty]
    private bool _recursive = true;
}
