using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.CLI;

public static class InitCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        AnsiConsole.MarkupLine("[bold blue]Steam UE Deploy Tool - Interactive Setup[/]");
        AnsiConsole.WriteLine();

        var repo = services.GetRequiredService<IProfileRepository>();

        AnsiConsole.Markup("[yellow]Project name:[/] ");
        var name = Console.ReadLine()?.Trim() ?? "MyProject";

        AnsiConsole.Markup("[yellow]UProject path:[/] ");
        var uprojectPath = Console.ReadLine()?.Trim() ?? string.Empty;

        AnsiConsole.Markup("[yellow]Platform (Win64/Linux/Mac):[/] ");
        var platformStr = Console.ReadLine()?.Trim() ?? "Win64";
        var platform = platformStr.Equals("Linux", StringComparison.OrdinalIgnoreCase)
            ? Platform.Linux
            : platformStr.Equals("Mac", StringComparison.OrdinalIgnoreCase)
                ? Platform.Mac
                : Platform.Win64;

        AnsiConsole.Markup("[yellow]Build configuration (Development/Shipping/Debug):[/] ");
        var configStr = Console.ReadLine()?.Trim() ?? "Development";
        var buildConfig = configStr.Equals("Shipping", StringComparison.OrdinalIgnoreCase)
            ? BuildConfiguration.Shipping
            : configStr.Equals("Debug", StringComparison.OrdinalIgnoreCase)
                ? BuildConfiguration.Debug
                : BuildConfiguration.Development;

        var buildProfile = new BuildProfile
        {
            Name = $"{name} {platform} {buildConfig}",
            UProjectPath = uprojectPath,
            Platform = platform,
            BuildConfiguration = buildConfig,
            Cook = true
        };

        await repo.SaveAsync(buildProfile);
        AnsiConsole.MarkupLine($"[green]Build profile '{buildProfile.Name}' created.[/]");

        AnsiConsole.WriteLine();

        AnsiConsole.Markup("[yellow]Steam App ID:[/] ");
        var appIdStr = Console.ReadLine()?.Trim() ?? "0";
        uint.TryParse(appIdStr, out var appId);

        AnsiConsole.Markup("[yellow]Depot ID:[/] ");
        var depotIdStr = Console.ReadLine()?.Trim() ?? "0";
        uint.TryParse(depotIdStr, out var depotId);

        AnsiConsole.Markup("[yellow]Depot content root path:[/] ");
        var contentRoot = Console.ReadLine()?.Trim() ?? string.Empty;

        AnsiConsole.Markup("[yellow]Branch name:[/] ");
        var branchName = Console.ReadLine()?.Trim() ?? "default";

        var deployTarget = new DeployTarget
        {
            Name = $"{name} {branchName}",
            AppId = appId,
            Depots =
            [
                new SteamDepot
                {
                    DepotId = depotId,
                    ContentRoot = contentRoot,
                    Mappings = [new FileMapping()]
                }
            ],
            BranchName = branchName,
            BuildDescription = $"Build {DateTime.UtcNow:yyyyMMdd-HHmm}"
        };

        await repo.SaveAsync(deployTarget);
        AnsiConsole.MarkupLine($"[green]Deploy target '{deployTarget.Name}' created.[/]");

        var pushProfile = new PushProfile
        {
            Name = $"{name} Push",
            BuildProfileId = buildProfile.Id,
            DeployTargetId = deployTarget.Id
        };

        await repo.SaveAsync(pushProfile);
        AnsiConsole.MarkupLine($"[green]Push profile '{pushProfile.Name}' created.[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Setup complete![/]");
        AnsiConsole.MarkupLine($"[grey]Run: sdt push --profile \"{pushProfile.Name}\"[/]");

        return 0;
    }
}
