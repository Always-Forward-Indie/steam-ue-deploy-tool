using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SteamUEDeployTool.Desktop.ViewModels;

public enum ApplicationView
{
    Dashboard,
    BuildConfig,
    DeployConfig,
    Push,
    AccountManager
}

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ApplicationView _currentView = ApplicationView.Dashboard;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public DashboardViewModel Dashboard { get; }
    public BuildConfigViewModel BuildConfig { get; }
    public DeployConfigViewModel DeployConfig { get; }
    public PushViewModel Push { get; }
    public AccountManagerViewModel AccountManager { get; }

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        BuildConfigViewModel buildConfig,
        DeployConfigViewModel deployConfig,
        PushViewModel push,
        AccountManagerViewModel accountManager)
    {
        Dashboard = dashboard;
        BuildConfig = buildConfig;
        DeployConfig = deployConfig;
        Push = push;
        AccountManager = accountManager;
        CurrentPage = Dashboard;
    }

    public async Task InitializeAsync()
    {
        await Dashboard.LoadAsync();
        await BuildConfig.LoadAsync();
        await DeployConfig.LoadAsync();
        await Push.LoadAsync();
        await AccountManager.LoadAsync();
    }

    [RelayCommand]
    private async Task NavigateToDashboard() => await NavigateAsync(ApplicationView.Dashboard, Dashboard);

    [RelayCommand]
    private async Task NavigateToBuildConfig() => await NavigateAsync(ApplicationView.BuildConfig, BuildConfig);

    [RelayCommand]
    private async Task NavigateToDeployConfig() => await NavigateAsync(ApplicationView.DeployConfig, DeployConfig);

    [RelayCommand]
    private async Task NavigateToPush() => await NavigateAsync(ApplicationView.Push, Push);

    [RelayCommand]
    private async Task NavigateToAccountManager() => await NavigateAsync(ApplicationView.AccountManager, AccountManager);

    private async Task NavigateAsync(ApplicationView view, ViewModelBase page)
    {
        CurrentView = view;
        CurrentPage = page;

        if (page is DashboardViewModel d) await d.LoadAsync();
        else if (page is BuildConfigViewModel bc) await bc.LoadAsync();
        else if (page is DeployConfigViewModel dc) await dc.LoadAsync();
        else if (page is PushViewModel p) await p.LoadAsync();
        else if (page is AccountManagerViewModel a) await a.LoadAsync();
    }
}
