using System.ComponentModel;
using Avalonia.Controls;
using SteamUEDeployTool.Desktop.ViewModels;

namespace SteamUEDeployTool.Desktop.Views;

public partial class AccountManagerView : UserControl
{
    public AccountManagerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is AccountManagerViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AccountManagerViewModel.StatusMessage))
                    StatusScroller.ScrollToEnd();
            };
        }
    }
}
