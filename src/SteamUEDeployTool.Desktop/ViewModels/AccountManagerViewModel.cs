using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Desktop.ViewModels;

public partial class AccountManagerViewModel : ViewModelBase
{
    private readonly IAccountStore _accountStore;
    private readonly ISecureCredentialStore _credentialStore;
    private readonly ISteamCmdLoginService _loginService;

    [ObservableProperty]
    private ObservableCollection<SteamAccount> _accounts = [];

    [ObservableProperty]
    private SteamAccount? _selectedAccount;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isWaitingForGuardCode;

    [ObservableProperty]
    private string _steamGuardCode = string.Empty;

    [ObservableProperty]
    private string _awaitingGuardForAccount = string.Empty;

    public AccountManagerViewModel(
        IAccountStore accountStore,
        ISecureCredentialStore credentialStore,
        ISteamCmdLoginService loginService)
    {
        _accountStore = accountStore;
        _credentialStore = credentialStore;
        _loginService = loginService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var all = await _accountStore.GetAllAsync();
        var hasCached = _loginService.HasCachedLogin();

        foreach (var acc in all)
            acc.HasSsfn = hasCached || acc.HasSsfn;

        Accounts = new ObservableCollection<SteamAccount>(all);
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            StatusMessage = "Username is required.";
            return;
        }

        var account = new SteamAccount
        {
            Username = NewUsername
        };

        if (!string.IsNullOrWhiteSpace(NewPassword))
        {
            await _credentialStore.SaveAsync(account.Id, NewPassword);
            account.HasCredential = true;
        }

        await _accountStore.SaveAsync(account);
        NewUsername = string.Empty;
        NewPassword = string.Empty;
        StatusMessage = $"Account '{account.Username}' added.";

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount is null) return;

        await _accountStore.DeleteAsync(SelectedAccount.Id);
        StatusMessage = $"Account '{SelectedAccount.Username}' removed.";

        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (SelectedAccount is null) return;

        var password = await _credentialStore.GetAsync(SelectedAccount.Id);

        if (string.IsNullOrWhiteSpace(password))
        {
            StatusMessage = "No saved password. Delete and re-add account with password.";
            return;
        }

        IsWaitingForGuardCode = false;
        StatusMessage = $"Logging in as '{SelectedAccount.Username}' via steamcmd...";

        var logProgress = new Progress<LogEntry>(entry =>
        {
            StatusMessage += $"\n[{entry.Level}] {entry.Message}";
            Log.Information("[{Source}] {Message}", entry.Source, entry.Message);
        });

        var result = await _loginService.LoginAsync(
            SelectedAccount.Username,
            password,
            async ct =>
            {
                StatusMessage = "Steam Guard code required. Check your email or Steam mobile app.";
                IsWaitingForGuardCode = true;
                AwaitingGuardForAccount = SelectedAccount.Username;

                var tcs = new TaskCompletionSource<string>();
                _currentGuardTcs = tcs;

                ct.Register(() => tcs.TrySetCanceled(ct));

                try
                {
                    return await tcs.Task;
                }
                finally
                {
                    IsWaitingForGuardCode = false;
                    AwaitingGuardForAccount = string.Empty;
                }
            },
            logProgress);

        if (result.Success)
        {
            Log.Information("Login successful for user {Username}", SelectedAccount.Username);
            SelectedAccount.LastLoginAt = DateTime.UtcNow;
            SelectedAccount.HasSsfn = true;
            await _accountStore.SaveAsync(SelectedAccount);

            StatusMessage += "\nLogin successful. SSFN cached for future logins and deploys.";
        }
        else
        {
            Log.Warning("Login failed for {Username}: {Error}", SelectedAccount.Username, result.ErrorMessage);
            StatusMessage = result.RequiresSteamGuard
                ? "Steam Guard code was incorrect or expired. Try again."
                : $"Login failed: {result.ErrorMessage}";
        }

        await LoadAsync();
    }

    private TaskCompletionSource<string>? _currentGuardTcs;

    [RelayCommand]
    private void SubmitGuardCode()
    {
        if (string.IsNullOrWhiteSpace(SteamGuardCode)) return;

        var tcs = _currentGuardTcs;
        _currentGuardTcs = null;

        if (tcs is not null)
        {
            var code = SteamGuardCode;
            SteamGuardCode = string.Empty;
            StatusMessage = "Verifying Steam Guard code...";
            tcs.TrySetResult(code);
        }
    }

    [RelayCommand]
    private void CancelGuardCode()
    {
        var tcs = _currentGuardTcs;
        _currentGuardTcs = null;
        IsWaitingForGuardCode = false;
        SteamGuardCode = string.Empty;
        StatusMessage = "Login cancelled.";
        tcs?.TrySetCanceled();
    }

    [RelayCommand]
    private async Task CopyErrorsAsync()
    {
        if (!string.IsNullOrWhiteSpace(StatusMessage))
        {
            var topLevel = TopLevelHelper.GetTopLevel();
            if (topLevel?.Clipboard is not null)
                await topLevel.Clipboard.SetTextAsync(StatusMessage);
        }
    }

    [RelayCommand]
    private void Logout()
    {
        if (SelectedAccount is null) return;
        _accountStore.Logout(SelectedAccount.Id);
        StatusMessage = $"Logged out '{SelectedAccount.Username}'.";
    }
}
