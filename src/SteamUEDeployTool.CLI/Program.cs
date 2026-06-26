using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;
using SteamUEDeployTool.Core.Services;
using SteamUEDeployTool.Infrastructure.Discovery;
using SteamUEDeployTool.Infrastructure.Runners;
using SteamUEDeployTool.Infrastructure.Storage;
using SteamUEDeployTool.Infrastructure.Vdf;

namespace SteamUEDeployTool.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        var host = CreateHostBuilder(args).Build();
        var services = host.Services;

        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine($"[bold blue]{Core.VersionInfo.ProductName}[/]");
            AnsiConsole.MarkupLine($"[grey]Version {Core.VersionInfo.Version}[/]");
            AnsiConsole.MarkupLine("[grey]Usage: sdt <command> [options][/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Commands:[/]");
            AnsiConsole.MarkupLine("  [green]build[/]    Run a build");
            AnsiConsole.MarkupLine("  [green]deploy[/]   Deploy to Steam");
            AnsiConsole.MarkupLine("  [green]push[/]     Build and deploy in one go");
            AnsiConsole.MarkupLine("  [green]init[/]     Initialize profiles interactively");
            AnsiConsole.MarkupLine("  [green]profile[/]  Manage profiles");
            AnsiConsole.MarkupLine("  [green]account[/]  Manage Steam accounts");
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var remainingArgs = args[1..];

        if (command is "--version" or "-v")
        {
            AnsiConsole.MarkupLine($"[bold blue]{Core.VersionInfo.ProductName}[/] v{Core.VersionInfo.Version}");
            return 0;
        }

        try
        {
            return command switch
            {
                "build" => await BuildCommand.ExecuteAsync(services, remainingArgs),
                "deploy" => await DeployCommand.ExecuteAsync(services, remainingArgs),
                "push" => await PushCommand.ExecuteAsync(services, remainingArgs),
                "init" => await InitCommand.ExecuteAsync(services, remainingArgs),
                "profile" => await ProfileCommand.ExecuteAsync(services, remainingArgs),
                "account" => await AccountCommand.ExecuteAsync(services, remainingArgs),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IProfileRepository, ProfileRepository>();
                services.AddSingleton<IAccountStore, AccountStore>();
                services.AddSingleton<ISecureCredentialStore, SecureCredentialStore>();
                services.AddSingleton<IEngineResolver, EngineResolver>();
                services.AddSingleton<IProjectDiscoveryService, ProjectDiscoveryService>();
                services.AddSingleton<IVdfGenerator, VdfGenerator>();
                services.AddTransient<IBuildRunner, UATRunner>();

                services.AddSingleton<SteamCmdRunner>();
                services.AddSingleton<ISteamDeployer>(sp => sp.GetRequiredService<SteamCmdRunner>());
                services.AddSingleton<ISteamCmdLoginService>(sp => sp.GetRequiredService<SteamCmdRunner>());

                services.AddSingleton<IVCRedistBundler, VCRedistBundler>();

                services.AddSingleton<BuildOrchestrator>();
                services.AddSingleton<DeployOrchestrator>();
                services.AddSingleton<PushPipelineService>();
            });
    }

    private static int HandleUnknownCommand(string command)
    {
        AnsiConsole.MarkupLine($"[red]Unknown command: {command}[/]");
        AnsiConsole.MarkupLine("[grey]Run 'sdt' without arguments for help.[/]");
        return 1;
    }
}
