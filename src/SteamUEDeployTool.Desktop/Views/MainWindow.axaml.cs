using Avalonia.Controls;
using SteamUEDeployTool.Desktop.ViewModels;

namespace SteamUEDeployTool.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<ApplicationView, Button> _navButtons;

    public MainWindow()
    {
        InitializeComponent();

        _navButtons = new Dictionary<ApplicationView, Button>
        {
            [ApplicationView.Dashboard] = BtnDashboard,
            [ApplicationView.BuildConfig] = BtnBuildConfig,
            [ApplicationView.DeployConfig] = BtnDeployConfig,
            [ApplicationView.Push] = BtnPush,
            [ApplicationView.AccountManager] = BtnAccountManager
        };

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.CurrentView))
                    HighlightActiveButton(vm.CurrentView);
            };

            HighlightActiveButton(vm.CurrentView);
        }
    }

    private void HighlightActiveButton(ApplicationView view)
    {
        foreach (var (v, btn) in _navButtons)
        {
            if (v == view)
                btn.Classes.Add("Active");
            else
                btn.Classes.Remove("Active");
        }
    }
}
