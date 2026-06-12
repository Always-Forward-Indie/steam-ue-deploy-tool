using Avalonia.Media;
using Avalonia.Data.Converters;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Desktop.Converters;

public static class LogLevelToColorConverter
{
    public static readonly IValueConverter Foreground = new FuncValueConverter<LogLevel, IBrush>(level =>
    {
        return level switch
        {
            LogLevel.Debug => Brushes.Gray,
            LogLevel.Info => Brushes.White,
            LogLevel.Warning => Brushes.Orange,
            LogLevel.Error => Brushes.Red,
            LogLevel.Success => Brushes.LimeGreen,
            _ => Brushes.White
        };
    });
}
