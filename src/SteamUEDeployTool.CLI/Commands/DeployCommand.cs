using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Services;

namespace SteamUEDeployTool.CLI;

public static class DeployCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        var profileName = ParseArg(args, "--profile", "-p");
        var buildPath = ParseArg(args, "--path", "-d");

        var repo = services.GetRequiredService<IProfileRepository>();
        var orchestrator = services.GetRequiredService<DeployOrchestrator>();

        if (string.IsNullOrWhiteSpace(profileName) || string.IsNullOrWhiteSpace(buildPath))
        {
            AnsiConsole.MarkupLine("[red]Specify --profile and --path[/]");
            return 1;
        }

        var targets = await repo.GetAllAsync<DeployTarget>();
        var target = targets.FirstOrDefault(t =>
            t.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            AnsiConsole.MarkupLine($"[red]Deploy target '{profileName}' not found.[/]");
            return 1;
        }

        if (!Directory.Exists(buildPath))
        {
            AnsiConsole.MarkupLine($"[red]Build path not found: '{buildPath}'[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[bold]Deploying: {target.Name}[/]");
        AnsiConsole.MarkupLine($"  AppID: [grey]{target.AppId}[/]");
        AnsiConsole.MarkupLine($"  Branch: [grey]{target.BranchName}[/]");
        AnsiConsole.MarkupLine($"  Path: [grey]{buildPath}[/]");
        AnsiConsole.WriteLine();

        var validation = await orchestrator.ValidateAsync(target);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                AnsiConsole.MarkupLine($"[red]  - {error}[/]");
            return 1;
        }

        var logProgress = new Progress<LogEntry>(CliLogRenderer.RenderEntry);
        var result = await orchestrator.DeployAsync(target, buildPath, logProgress);

        AnsiConsole.WriteLine();

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]Deploy completed in {result.Duration.TotalMinutes:F1} minutes.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[red]Deploy failed: {result.ErrorMessage}[/]");
        return result.ExitCode != 0 ? result.ExitCode : 1;
    }

    private static string? ParseArg(string[] args, string name, string? shortName = null)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == name || (shortName is not null && args[i] == shortName))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    return args[i + 1];
                return string.Empty;
            }
        }
        return null;
    }
}
