using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;
using SteamUEDeployTool.Core.Services;
using SteamUEDeployTool.Core.Validation;

namespace SteamUEDeployTool.Desktop.ViewModels;

public partial class BuildConfigViewModel : ViewModelBase
{
    private readonly IProfileRepository _profileRepository;
    private readonly BuildOrchestrator _buildOrchestrator;

    [ObservableProperty]
    private ObservableCollection<BuildProfile> _profiles = [];

    [ObservableProperty]
    private BuildProfile? _selectedProfile;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _uProjectPath = string.Empty;

    [ObservableProperty]
    private string? _customEnginePath;

    [ObservableProperty]
    private int _selectedPlatformIndex;

    [ObservableProperty]
    private int _selectedBuildConfigurationIndex;

    [ObservableProperty]
    private bool _cook;

    [ObservableProperty]
    private bool _cleanBuild;

    [ObservableProperty]
    private bool _bundleVCRedist = true;

    [ObservableProperty]
    private string? _extraArgs;

    [ObservableProperty]
    private string? _outputPathOverride;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private ObservableCollection<string> _platforms = new(Enum.GetNames<Platform>());

    [ObservableProperty]
    private ObservableCollection<string> _buildConfigurations = new(Enum.GetNames<BuildConfiguration>());

    public BuildConfigViewModel(
        IProfileRepository profileRepository,
        BuildOrchestrator buildOrchestrator)
    {
        _profileRepository = profileRepository;
        _buildOrchestrator = buildOrchestrator;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var all = await _profileRepository.GetAllAsync<BuildProfile>();
        Profiles = new ObservableCollection<BuildProfile>(all);
    }

    partial void OnSelectedProfileChanged(BuildProfile? value)
    {
        if (value is null) return;

        Name = value.Name;
        UProjectPath = value.UProjectPath;
        CustomEnginePath = value.CustomEnginePath;
        SelectedPlatformIndex = (int)value.Platform;
        SelectedBuildConfigurationIndex = (int)value.BuildConfiguration;
        Cook = value.Cook;
        CleanBuild = value.CleanBuild;
        BundleVCRedist = value.BundleVCRedist;
        ExtraArgs = value.ExtraArgs;
        OutputPathOverride = value.OutputPathOverride;
    }

    [RelayCommand]
    private async Task BrowseUProjectAsync()
    {
        var window = TopLevelHelper.GetTopLevel();
        if (window is null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Unreal Project",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new("Unreal Project") { Patterns = ["*.uproject"] }
                ]
            });

        if (files.Count > 0)
            UProjectPath = files[0].Path.LocalPath;
    }

    [RelayCommand]
    private async Task BrowseEnginePathAsync()
    {
        var window = TopLevelHelper.GetTopLevel();
        if (window is null) return;

        var folders = await window.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Unreal Engine Folder",
                AllowMultiple = false
            });

        if (folders.Count > 0)
            CustomEnginePath = folders[0].Path.LocalPath;
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        var profile = CreateProfileFromForm();
        var result = await _buildOrchestrator.ValidateAsync(profile);

        if (result.IsValid)
        {
            var engine = await _buildOrchestrator.ResolveEngineAsync(profile);
            ValidationMessage = engine is not null
                ? $"Valid. Engine: {engine.Version} ({engine.Type}) at {engine.Path}"
                : "Valid. Engine not resolved (may need CustomEnginePath).";
            IsValid = true;
        }
        else
        {
            ValidationMessage = string.Join("\n", result.Errors);
            IsValid = false;
        }
    }

    [RelayCommand]
    private async Task CopyErrorsAsync()
    {
        if (!string.IsNullOrWhiteSpace(ValidationMessage))
        {
            var clipboard = TopLevelHelper.GetTopLevel()?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(ValidationMessage);
        }
    }

    [RelayCommand]
    private void DeepClean()
    {
        if (string.IsNullOrWhiteSpace(UProjectPath))
        {
            ValidationMessage = "Select a .uproject file first.";
            IsValid = false;
            return;
        }

        var projectDir = Path.GetDirectoryName(UProjectPath);
        if (projectDir is null || !Directory.Exists(projectDir))
        {
            ValidationMessage = "Project directory not found.";
            IsValid = false;
            return;
        }

        var dirsToClean = new[] { "Intermediate", "Binaries", "Saved", "DerivedDataCache" };
        var deleted = new List<string>();
        var failed = new List<string>();

        foreach (var dir in dirsToClean)
        {
            var path = Path.Combine(projectDir, dir);
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                    deleted.Add(dir);
                    Log.Information("Deep clean: deleted {Dir}", path);
                }
                catch (Exception ex)
                {
                    failed.Add($"{dir}: {ex.Message}");
                    Log.Warning("Deep clean: failed to delete {Dir}: {Error}", path, ex.Message);
                }
            }
        }

        IsValid = true;
        ValidationMessage = deleted.Count > 0
            ? $"Cleaned: {string.Join(", ", deleted)}."
            : "Nothing to clean.";

        if (failed.Count > 0)
            ValidationMessage += $"\nSkipped: {string.Join("; ", failed)}";

        Log.Information("Deep clean completed. Deleted: {Deleted}, Failed: {Failed}",
            string.Join(", ", deleted), string.Join(", ", failed));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var profile = CreateProfileFromForm();
        if (Guid.Empty == profile.Id)
            return;

        if (SelectedProfile is not null)
            profile.Id = SelectedProfile.Id;
        else
            profile.CreatedAt = DateTime.UtcNow;

        profile.ModifiedAt = DateTime.UtcNow;

        await _profileRepository.SaveAsync(profile);
        Log.Information("Build profile saved: {Name} ({Platform}/{Config})",
            profile.Name, profile.Platform, profile.BuildConfiguration);
        await LoadAsync();

        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profile.Id);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProfile is null) return;
        await _profileRepository.DeleteAsync<BuildProfile>(SelectedProfile.Id);
        Log.Information("Build profile deleted: {Name}", SelectedProfile.Name);
        ClearForm();
        await LoadAsync();
    }

    [RelayCommand]
    private void ClearForm()
    {
        SelectedProfile = null;
        Name = string.Empty;
        UProjectPath = string.Empty;
        CustomEnginePath = null;
        SelectedPlatformIndex = 0;
        SelectedBuildConfigurationIndex = 0;
        Cook = false;
        CleanBuild = false;
        BundleVCRedist = true;
        ExtraArgs = null;
        OutputPathOverride = null;
        ValidationMessage = string.Empty;
        IsValid = false;
    }

    private BuildProfile CreateProfileFromForm()
    {
        return new BuildProfile
        {
            Id = SelectedProfile?.Id ?? Guid.NewGuid(),
            Name = Name,
            UProjectPath = UProjectPath,
            CustomEnginePath = CustomEnginePath,
            Platform = (Platform)SelectedPlatformIndex,
            BuildConfiguration = (BuildConfiguration)SelectedBuildConfigurationIndex,
            Cook = Cook,
            CleanBuild = CleanBuild,
            BundleVCRedist = BundleVCRedist,
            ExtraArgs = ExtraArgs,
            OutputPathOverride = OutputPathOverride
        };
    }
}

internal static class TopLevelHelper
{
    public static Avalonia.Controls.TopLevel? GetTopLevel()
    {
        return Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
