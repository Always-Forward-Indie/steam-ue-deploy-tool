using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Services;

namespace SteamUEDeployTool.CLI;

public static class BuildCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        var profileName = ParseArg(args, "--profile", "-p");
        var projectPath = ParseArg(args, "--project");
        var platform = ParseArg(args, "--platform");
        var configuration = ParseArg(args, "--config");

        var repo = services.GetRequiredService<IProfileRepository>();
        var orchestrator = services.GetRequiredService<BuildOrchestrator>();

        BuildProfile? profile = null;

        if (!string.IsNullOrWhiteSpace(profileName))
        {
            var profiles = await repo.GetAllAsync<BuildProfile>();
            profile = profiles.FirstOrDefault(p =>
                p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));

            if (profile is null)
            {
                AnsiConsole.MarkupLine($"[red]Build profile '{profileName}' not found.[/]");
                return 1;
            }
        }
        else if (!string.IsNullOrWhiteSpace(projectPath))
        {
            profile = new BuildProfile
            {
                Name = Path.GetFileNameWithoutExtension(projectPath),
                UProjectPath = projectPath,
                Platform = ParsePlatform(platform),
                BuildConfiguration = ParseBuildConfig(configuration)
            };
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Specify --profile or --project with --platform and --config.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[bold]Building: {profile.Name}[/]");
        AnsiConsole.MarkupLine($"  Project: [grey]{profile.UProjectPath}[/]");
        AnsiConsole.MarkupLine($"  Platform: [grey]{profile.Platform}[/]");
        AnsiConsole.MarkupLine($"  Config: [grey]{profile.BuildConfiguration}[/]");
        AnsiConsole.WriteLine();

        var logProgress = new Progress<LogEntry>(CliLogRenderer.RenderEntry);
        var result = await orchestrator.BuildAsync(profile, logProgress);

        AnsiConsole.WriteLine();

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]Build completed in {result.Duration.TotalMinutes:F1} minutes.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[red]Build failed: {result.ErrorMessage}[/]");
        return result.ExitCode != 0 ? result.ExitCode : 1;
    }

    private static Core.Models.Enums.Platform ParsePlatform(string? value) => value?.ToLowerInvariant() switch
    {
        "linux" => Core.Models.Enums.Platform.Linux,
        "mac" => Core.Models.Enums.Platform.Mac,
        _ => Core.Models.Enums.Platform.Win64
    };

    private static Core.Models.Enums.BuildConfiguration ParseBuildConfig(string? value) => value?.ToLowerInvariant() switch
    {
        "debug" => Core.Models.Enums.BuildConfiguration.Debug,
        "debuggame" => Core.Models.Enums.BuildConfiguration.DebugGame,
        "shipping" => Core.Models.Enums.BuildConfiguration.Shipping,
        "test" => Core.Models.Enums.BuildConfiguration.Test,
        _ => Core.Models.Enums.BuildConfiguration.Development
    };

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
