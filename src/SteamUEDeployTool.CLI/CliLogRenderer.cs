using Spectre.Console;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.CLI;

public static class CliLogRenderer
{
    public static void RenderEntry(LogEntry entry)
    {
        var timestamp = $"[{entry.Timestamp:HH:mm:ss}]";
        var level = entry.Level switch
        {
            LogLevel.Debug => $"[grey]DEBUG[/]",
            LogLevel.Info => $"[white]INFO [/]",
            LogLevel.Warning => $"[yellow]WARN [/]",
            LogLevel.Error => $"[red]ERROR[/]",
            LogLevel.Success => $"[green]OK   [/]",
            _ => $"[white]INFO [/]"
        };

        var source = entry.Source is not null
            ? $" [blue]{entry.Source}[/]"
            : "";

        var message = entry.Level switch
        {
            LogLevel.Error => $"[red]{Markup.Escape(entry.Message)}[/]",
            LogLevel.Warning => $"[yellow]{Markup.Escape(entry.Message)}[/]",
            _ => $"[grey]{Markup.Escape(entry.Message)}[/]"
        };

        AnsiConsole.MarkupLine($"{timestamp} {level}{source} {message}");
    }
}
