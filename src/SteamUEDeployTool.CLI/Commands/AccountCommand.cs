using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.CLI;

public static class AccountCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        var subCommand = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
        var store = services.GetRequiredService<IAccountStore>();
        var credentialStore = services.GetRequiredService<ISecureCredentialStore>();

        return subCommand switch
        {
            "list" => await ListAccounts(store),
            "add" => await AddAccount(store, credentialStore),
            "delete" => await DeleteAccount(store, args[1..]),
            "login" => await LoginAccount(services, args[1..]),
            "logout" => await LogoutAccount(store, args[1..]),
            _ => UnknownCommand(subCommand)
        };
    }

    private static async Task<int> ListAccounts(IAccountStore store)
    {
        var accounts = await store.GetAllAsync();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Steam Accounts:[/]");

        var table = new Table();
        table.AddColumn("Username");
        table.AddColumn("SSFN");
        table.AddColumn("Password");
        table.AddColumn("Last Login");

        foreach (var acc in accounts)
        {
            table.AddRow(
                acc.Username,
                acc.HasSsfn ? "[green]Yes[/]" : "[red]No[/]",
                acc.HasCredential ? "[green]Yes[/]" : "[red]No[/]",
                acc.LastLoginAt?.ToString("g") ?? "-");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static async Task<int> AddAccount(IAccountStore store, ISecureCredentialStore credentialStore)
    {
        AnsiConsole.Markup("[yellow]Steam username:[/] ");
        var username = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            AnsiConsole.MarkupLine("[red]Username is required.[/]");
            return 1;
        }

        AnsiConsole.Markup("[yellow]Steam password (leave empty to skip):[/] ");
        var password = Console.ReadLine()?.Trim();

        var account = new SteamAccount { Username = username };

        if (!string.IsNullOrWhiteSpace(password))
        {
            await credentialStore.SaveAsync(account.Id, password);
            account.HasCredential = true;
        }

        await store.SaveAsync(account);

        AnsiConsole.MarkupLine($"[green]Account '{username}' added.[/]");
        return 0;
    }

    private static async Task<int> DeleteAccount(IAccountStore store, string[] args)
    {
        var username = ParseArg(args, "--username", "-u");

        if (string.IsNullOrWhiteSpace(username))
        {
            AnsiConsole.MarkupLine("[red]Specify --username[/]");
            return 1;
        }

        var accounts = await store.GetAllAsync();
        var account = accounts.FirstOrDefault(a =>
            a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            AnsiConsole.MarkupLine($"[red]Account '{username}' not found.[/]");
            return 1;
        }

        await store.DeleteAsync(account.Id);
        AnsiConsole.MarkupLine($"[green]Account '{username}' deleted.[/]");
        return 0;
    }

    private static async Task<int> LoginAccount(IServiceProvider services, string[] args)
    {
        var store = services.GetRequiredService<IAccountStore>();
        var credentialStore = services.GetRequiredService<ISecureCredentialStore>();
        var loginService = services.GetRequiredService<ISteamCmdLoginService>();

        var username = ParseArg(args, "--username", "-u");

        if (string.IsNullOrWhiteSpace(username))
        {
            AnsiConsole.MarkupLine("[red]Specify --username[/]");
            return 1;
        }

        var accounts = await store.GetAllAsync();
        var account = accounts.FirstOrDefault(a =>
            a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            AnsiConsole.MarkupLine($"[red]Account '{username}' not found.[/]");
            return 1;
        }

        var password = await credentialStore.GetAsync(account.Id);
        if (string.IsNullOrWhiteSpace(password))
        {
            AnsiConsole.MarkupLine("[red]No saved password. Delete and re-add account with password.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[bold]Logging in as '{username}' via steamcmd...[/]");

        var result = await loginService.LoginAsync(
            username,
            password,
            async ct =>
            {
                await Task.Yield();
                AnsiConsole.Markup("[yellow]Enter Steam Guard code:[/] ");
                return Console.ReadLine() ?? string.Empty;
            });

        if (result.Success)
        {
            account.LastLoginAt = DateTime.UtcNow;
            account.HasSsfn = true;
            await store.SaveAsync(account);
            AnsiConsole.MarkupLine($"[green]Login successful! SSFN cached.[/]");
        }
        else if (result.RequiresSteamGuard)
            AnsiConsole.MarkupLine("[yellow]Steam Guard code was incorrect. Try again.[/]");
        else
            AnsiConsole.MarkupLine($"[red]Login failed: {result.ErrorMessage}[/]");

        return result.Success ? 0 : 1;
    }

    private static async Task<int> LogoutAccount(IAccountStore store, string[] args)
    {
        var username = ParseArg(args, "--username", "-u");

        if (string.IsNullOrWhiteSpace(username))
        {
            AnsiConsole.MarkupLine("[red]Specify --username[/]");
            return 1;
        }

        var accounts = await store.GetAllAsync();
        var account = accounts.FirstOrDefault(a =>
            a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            AnsiConsole.MarkupLine($"[red]Account '{username}' not found.[/]");
            return 1;
        }

        store.Logout(account.Id);
        AnsiConsole.MarkupLine($"[green]Logged out '{username}'.[/]");
        return 0;
    }

    private static int UnknownCommand(string subCommand)
    {
        AnsiConsole.MarkupLine($"[red]Unknown account command: {subCommand}[/]");
        AnsiConsole.MarkupLine("[grey]Usage: sdt account <list|add|delete|login|logout>[/]");
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
