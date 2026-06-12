using Avalonia.Controls;
using Avalonia.Media;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Desktop.Converters;

namespace SteamUEDeployTool.Desktop.Controls;

public partial class LogEntryControl : UserControl
{
    public LogEntryControl()
    {
        InitializeComponent();
    }

    public void SetEntry(LogEntry entry)
    {
        LogText.Text = $"[{entry.Timestamp:HH:mm:ss}] [{entry.Source}] {entry.Message}";
        LogText.Foreground = (IBrush?)LogLevelToColorConverter.Foreground.Convert(
            entry.Level, typeof(IBrush), null!, null!) ?? Brushes.White;
    }
}
