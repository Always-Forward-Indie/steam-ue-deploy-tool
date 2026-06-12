using Avalonia.Controls;
using SteamUEDeployTool.Desktop.ViewModels;

namespace SteamUEDeployTool.Desktop.Views;

public partial class PushView : UserControl
{
    public PushView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is PushViewModel vm)
        {
            vm.Logs.CollectionChanged += (_, _) =>
            {
                LogScroller.ScrollToEnd();
            };
        }
    }
}
