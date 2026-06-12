using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.CLI;

public static class ProfileCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        var subCommand = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
        var repo = services.GetRequiredService<IProfileRepository>();

        switch (subCommand)
        {
            case "list":
                return await ListProfiles(repo);

            case "delete":
                return await DeleteProfile(repo, args[1..]);

            case "create":
                AnsiConsole.MarkupLine("[grey]Use 'sdt init' for interactive profile creation.[/]");
                return 0;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown profile command: {subCommand}[/]");
                return 1;
        }
    }

    private static async Task<int> ListProfiles(IProfileRepository repo)
    {
        var buildProfiles = await repo.GetAllAsync<BuildProfile>();
        var deployTargets = await repo.GetAllAsync<DeployTarget>();
        var pushProfiles = await repo.GetAllAsync<PushProfile>();

        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Build Profiles:[/]");
        var buildTable = new Table();
        buildTable.AddColumn("Name");
        buildTable.AddColumn("Platform");
        buildTable.AddColumn("Config");
        buildTable.AddColumn("Project");

        foreach (var bp in buildProfiles)
        {
            buildTable.AddRow(bp.Name, bp.Platform.ToString(), bp.BuildConfiguration.ToString(),
                Path.GetFileName(bp.UProjectPath));
        }

        AnsiConsole.Write(buildTable);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Deploy Targets:[/]");
        var deployTable = new Table();
        deployTable.AddColumn("Name");
        deployTable.AddColumn("AppID");
        deployTable.AddColumn("Branch");
        deployTable.AddColumn("Depots");

        foreach (var dt in deployTargets)
        {
            deployTable.AddRow(dt.Name, dt.AppId.ToString(), dt.BranchName,
                dt.Depots.Count.ToString());
        }

        AnsiConsole.Write(deployTable);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Push Profiles:[/]");
        var pushTable = new Table();
        pushTable.AddColumn("Name");
        pushTable.AddColumn("Build");
        pushTable.AddColumn("Deploy");

        foreach (var pp in pushProfiles)
        {
            var bp = await repo.GetByIdAsync<BuildProfile>(pp.BuildProfileId);
            var dt = await repo.GetByIdAsync<DeployTarget>(pp.DeployTargetId);
            pushTable.AddRow(pp.Name, bp?.Name ?? "n/a", dt?.Name ?? "n/a");
        }

        AnsiConsole.Write(pushTable);

        return 0;
    }

    private static async Task<int> DeleteProfile(IProfileRepository repo, string[] args)
    {
        var type = ParseArg(args, "--type", "-t")?.ToLowerInvariant();
        var name = ParseArg(args, "--name", "-n");

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
        {
            AnsiConsole.MarkupLine("[red]Specify --type (build/deploy/push) --name[/]");
            return 1;
        }

        switch (type)
        {
            case "build":
                {
                    var profiles = await repo.GetAllAsync<BuildProfile>();
                    var profile = profiles.FirstOrDefault(p =>
                        p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (profile is null)
                    {
                        AnsiConsole.MarkupLine($"[red]Build profile '{name}' not found.[/]");
                        return 1;
                    }
                    await repo.DeleteAsync<BuildProfile>(profile.Id);
                    AnsiConsole.MarkupLine($"[green]Deleted build profile '{name}'.[/]");
                    break;
                }
            case "deploy":
                {
                    var targets = await repo.GetAllAsync<DeployTarget>();
                    var target = targets.FirstOrDefault(t =>
                        t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (target is null)
                    {
                        AnsiConsole.MarkupLine($"[red]Deploy target '{name}' not found.[/]");
                        return 1;
                    }
                    await repo.DeleteAsync<DeployTarget>(target.Id);
                    AnsiConsole.MarkupLine($"[green]Deleted deploy target '{name}'.[/]");
                    break;
                }
            case "push":
                {
                    var profiles = await repo.GetAllAsync<PushProfile>();
                    var profile = profiles.FirstOrDefault(p =>
                        p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (profile is null)
                    {
                        AnsiConsole.MarkupLine($"[red]Push profile '{name}' not found.[/]");
                        return 1;
                    }
                    await repo.DeleteAsync<PushProfile>(profile.Id);
                    AnsiConsole.MarkupLine($"[green]Deleted push profile '{name}'.[/]");
                    break;
                }
            default:
                AnsiConsole.MarkupLine($"[red]Unknown type: {type}[/]");
                return 1;
        }

        return 0;
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
