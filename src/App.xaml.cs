﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using DevHome.Activation;
using DevHome.Common.Contracts;
using DevHome.Common.Contracts.Services;
using DevHome.Common.Environments.Services;
using DevHome.Common.Extensions;
using DevHome.Common.Models;
using DevHome.Common.Services;
using DevHome.Contracts.Services;
using DevHome.Customization.Extensions;
using DevHome.Dashboard.Extensions;
using DevHome.ExtensionLibrary.Extensions;
using DevHome.Helpers;
using DevHome.Services;
using DevHome.Settings.Extensions;
using DevHome.SetupFlow.Extensions;
using DevHome.SetupFlow.Services;
using DevHome.Telemetry;
using DevHome.Utilities.Extensions;
using DevHome.ViewModels;
using DevHome.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace DevHome;

public partial class App : Application, IApp
{
    private readonly DispatcherQueue _dispatcherQueue;

    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public T GetService<T>()
        where T : class => Host.GetService<T>();

    public static Window MainWindow { get; } = new MainWindow();

    private static string RemoveComments(string text)
    {
        var start = text.IndexOf("/*", StringComparison.Ordinal);
        while (start >= 0)
        {
            var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                end = text.Length;
            }

            text = text.Remove(start, end - start + 2);
            start = text.IndexOf("/*", start, StringComparison.Ordinal);
        }

        return text;
    }

    internal static NavConfig NavConfig { get; } = System.Text.Json.JsonSerializer.Deserialize(
        RemoveComments(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "navConfig.jsonc"))),
        SourceGenerationContext.Default.NavConfig)!;

    public App()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Set up Logging
        Environment.SetEnvironmentVariable("DEVHOME_LOGS_ROOT", Path.Join(Common.Logging.LogFolderRoot, "DevHome"));
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        UseDefaultServiceProvider((context, options) =>
        {
            options.ValidateOnBuild = true;
        }).
        ConfigureServices((context, services) =>
        {
            // Add Serilog logging for ILogger.
            services.AddLogging(lb => lb.AddSerilog(dispose: true));

            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers
            services.AddTransient<IActivationHandler, ProtocolActivationHandler>();
            services.AddTransient<IActivationHandler, DSCFileActivationHandler>();
            services.AddTransient<IActivationHandler, AppInstallActivationHandler>();

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IExperimentationService, ExperimentationService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IExtensionService, ExtensionService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IAccountsService, AccountsService>();
            services.AddSingleton<IInfoBarService, InfoBarService>();
            services.AddSingleton<IAppInfoService, AppInfoService>();
            services.AddSingleton<ITelemetry>(TelemetryFactory.Get<ITelemetry>());
            services.AddSingleton<IStringResource, StringResource>();
            services.AddSingleton<IAppInstallManagerService, AppInstallManagerService>();
            services.AddSingleton<IPackageDeploymentService, PackageDeploymentService>();
            services.AddSingleton<IScreenReaderService, ScreenReaderService>();
            services.AddSingleton<IComputeSystemService, ComputeSystemService>();
            services.AddSingleton<IComputeSystemManager, ComputeSystemManager>();
            services.AddSingleton<IQuickstartSetupService, QuickstartSetupService>();
            services.AddTransient<AdaptiveCardRenderingService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Main window: Allow access to the main window
            // from anywhere in the application.
            services.AddSingleton(_ => MainWindow);

            // DispatcherQueue: Allow access to the DispatcherQueue for
            // the main window for general purpose UI thread access.
            services.AddSingleton(_ => MainWindow.DispatcherQueue);

            // Views and ViewModels
            services.AddTransient<ShellPage>();
            services.AddTransient<InitializationPage>();
            services.AddTransient<ShellViewModel>();
            services.AddTransient<WhatsNewViewModel>();
            services.AddTransient<InitializationViewModel>();

            // Settings
            services.AddSettings(context);

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));

            // Setup flow
            services.AddSetupFlow(context);

            // Dashboard
            services.AddDashboard(context);

            // ExtensionLibrary
            services.AddExtensionLibrary(context);

            // Environments
            services.AddEnvironments(context);

            // Windows customization
            services.AddWindowsCustomization(context);

            // Utilities
            services.AddUtilities(context);
        }).
        Build();

        UnhandledException += App_UnhandledException;
        AppInstance.GetCurrent().Activated += OnActivated;
    }

    public void ShowMainWindow()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
            if (PInvoke.IsIconic(new HWND(hWnd)))
            {
                PInvoke.ShowWindow(new HWND(hWnd), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_RESTORE);
            }
            else
            {
                PInvoke.SetForegroundWindow(new HWND(hWnd));
            }
        });
    }

    private async void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
        // https://github.com/microsoft/devhome/issues/613
        await GetService<IExtensionService>().SignalStopExtensionsAsync();
    }

    protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        await Task.WhenAll(
            GetService<IActivationService>().ActivateAsync(AppInstance.GetCurrent().GetActivatedEventArgs().Data),
            GetService<IAccountsService>().InitializeAsync(),
            GetService<IAppManagementInitializer>().InitializeAsync());
    }

    private async void OnActivated(object? sender, AppActivationArguments args)
    {
        // Note: Keep the reference to 'args.Data' object, as 'args' may be
        // disposed before the async operation completes (RpcCallFailed: 0x800706be)
        var localArgsDataReference = args.Data;

        // Activate the app and ensure the appropriate handlers are called.
        await _dispatcherQueue.EnqueueAsync(async () => await GetService<IActivationService>().ActivateAsync(localArgsDataReference));
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        Log.CloseAndFlush();
    }
}
