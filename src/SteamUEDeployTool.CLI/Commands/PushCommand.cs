using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Services;

namespace SteamUEDeployTool.CLI;

public static class PushCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        var profileName = ParseArg(args, "--profile", "-p");

        if (string.IsNullOrWhiteSpace(profileName))
        {
            AnsiConsole.MarkupLine("[red]Specify --profile (Push profile name)[/]");
            return 1;
        }

        var repo = services.GetRequiredService<IProfileRepository>();
        var pipeline = services.GetRequiredService<PushPipelineService>();

        var pushProfiles = await repo.GetAllAsync<PushProfile>();
        var pushProfile = pushProfiles.FirstOrDefault(p =>
            p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));

        if (pushProfile is null)
        {
            AnsiConsole.MarkupLine($"[red]Push profile '{profileName}' not found.[/]");
            return 1;
        }

        var buildProfile = await repo.GetByIdAsync<BuildProfile>(pushProfile.BuildProfileId);
        var deployTarget = await repo.GetByIdAsync<DeployTarget>(pushProfile.DeployTargetId);

        AnsiConsole.MarkupLine($"[bold]Push: {pushProfile.Name}[/]");
        AnsiConsole.MarkupLine($"  Build: [grey]{buildProfile?.Name ?? "n/a"}[/]");
        AnsiConsole.MarkupLine($"  Deploy: [grey]{deployTarget?.Name ?? "n/a"} → steam://{deployTarget?.AppId}/{deployTarget?.BranchName}[/]");
        AnsiConsole.WriteLine();

        var logProgress = new Progress<LogEntry>(CliLogRenderer.RenderEntry);

        var stageProgress = new Progress<PushProgress>(p =>
        {
            AnsiConsole.MarkupLine($"[blue]{p.Stage}[/] [grey]{p.CurrentAction}[/]");
        });

        var result = await pipeline.PushAsync(pushProfile, logProgress, stageProgress);

        AnsiConsole.WriteLine();

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]Push completed in {result.Duration.TotalMinutes:F1} minutes.[/]");
            return 0;
        }

        var error = result.DeployResult?.ErrorMessage
            ?? result.BuildResult?.ErrorMessage
            ?? "Unknown error";

        AnsiConsole.MarkupLine($"[red]Push failed: {error}[/]");
        return 1;
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
