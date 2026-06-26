using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Services;
using SteamUEDeployTool.Desktop.ViewModels;
using SteamUEDeployTool.Desktop.Views;
using SteamUEDeployTool.Infrastructure.Discovery;
using SteamUEDeployTool.Infrastructure.Runners;
using SteamUEDeployTool.Infrastructure.Storage;
using SteamUEDeployTool.Infrastructure.Vdf;

namespace SteamUEDeployTool.Desktop;

public class App : Application
{
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _host = CreateHostBuilder().Build();
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = _host!.Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = _host!.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = vm;
            desktop.MainWindow = mainWindow;

            await vm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IHostBuilder CreateHostBuilder()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamUEDeployTool",
            "logs");

        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDir, "sdt-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("SteamUE Deploy Tool v{Version} starting", Core.VersionInfo.Version);

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Log.Information("SteamUE Deploy Tool shutting down");
            Log.CloseAndFlush();
        };

        return Host.CreateDefaultBuilder()
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

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<BuildConfigViewModel>();
                services.AddSingleton<DeployConfigViewModel>();
                services.AddSingleton<PushViewModel>();
                services.AddSingleton<AccountManagerViewModel>();

                services.AddSingleton<MainWindow>();
            });
    }
}
