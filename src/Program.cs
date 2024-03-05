// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHome.Activation;
using DevHome.Contracts.Services;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.Activation;

namespace DevHome;

public static class Program
{
    private static App? _app;

    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var isRedirect = DecideRedirection().GetAwaiter().GetResult();

        if (!isRedirect)
        {
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                var context = new DispatcherQueueSynchronizationContext(dispatcherQueue);
                SynchronizationContext.SetSynchronizationContext(context);
                _app = new App();
            });
        }
    }

    private static async Task<bool> DecideRedirection()
    {
        var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("main");
        var activatedEventArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();

        var isRedirect = false;
        if (mainInstance.IsCurrent)
        {
            mainInstance.Activated += OnActivated;
        }
        else
        {
            // Redirect the activation (and args) to the "main" instance, and exit.
            await mainInstance.RedirectActivationToAsync(activatedEventArgs);
            isRedirect = true;
        }

        return isRedirect;
    }

    private static async void OnActivated(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments args)
    {
        if (args.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.Protocol)
        {
            if (args.Data as ProtocolActivatedEventArgs is ProtocolActivatedEventArgs eventArgs && _app != null)
            {
                var protocolActivationHandler = _app!.GetService<IActivationHandler>();
                if (protocolActivationHandler != null && protocolActivationHandler.CanHandle(eventArgs))
                {
                    await _app.GetService<IActivationService>().ActivateAsync(eventArgs);
                }
            }
        }

        _app?.ShowMainWindow();
    }
}
