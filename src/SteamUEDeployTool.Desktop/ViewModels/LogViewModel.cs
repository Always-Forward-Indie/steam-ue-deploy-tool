using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Desktop.ViewModels;

public partial class LogViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<LogEntry> _entries = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showDebug = true;

    [ObservableProperty]
    private bool _showInfo = true;

    [ObservableProperty]
    private bool _showWarning = true;

    [ObservableProperty]
    private bool _showError = true;

    [ObservableProperty]
    private bool _showSuccess = true;

    public void AddEntry(LogEntry entry)
    {
        if (!ShouldShowEntry(entry))
            return;

        Entries.Add(entry);
    }

    public void AddEntries(IEnumerable<LogEntry> entries)
    {
        foreach (var entry in entries)
            AddEntry(entry);
    }

    public void Clear()
    {
        Entries.Clear();
    }

    private bool ShouldShowEntry(LogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(SearchText)
            && !entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        return entry.Level switch
        {
            LogLevel.Debug => ShowDebug,
            LogLevel.Info => ShowInfo,
            LogLevel.Warning => ShowWarning,
            LogLevel.Error => ShowError,
            LogLevel.Success => ShowSuccess,
            _ => true
        };
    }
}
