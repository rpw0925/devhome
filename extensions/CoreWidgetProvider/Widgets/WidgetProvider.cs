﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using Serilog;

namespace CoreWidgetProvider.Widgets;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("F8B2DBB9-3687-4C6E-99B2-B92C82905937")]
internal sealed class WidgetProvider : IWidgetProvider, IWidgetProvider2
{
    private readonly Dictionary<string, IWidgetImplFactory> widgetDefinitionRegistry = new();
    private readonly Dictionary<string, WidgetImpl> runningWidgets = new();

    public WidgetProvider()
    {
        Log.Debug("Provider Constructed");
        widgetDefinitionRegistry.Add("SSH_Wallet", new WidgetImplFactory<SSHWalletWidget>());
        widgetDefinitionRegistry.Add("System_Memory", new WidgetImplFactory<SystemMemoryWidget>());
        widgetDefinitionRegistry.Add("System_NetworkUsage", new WidgetImplFactory<SystemNetworkUsageWidget>());
        widgetDefinitionRegistry.Add("System_GPUUsage", new WidgetImplFactory<SystemGPUUsageWidget>());
        widgetDefinitionRegistry.Add("System_CPUUsage", new WidgetImplFactory<SystemCPUUsageWidget>());
        RecoverRunningWidgets();
    }

    private void InitializeWidget(WidgetContext widgetContext, string state)
    {
        var widgetId = widgetContext.Id;
        var widgetDefinitionId = widgetContext.DefinitionId;
        Log.Debug($"Calling Initialize for Widget Id: {widgetId} - {widgetDefinitionId}");

        if (!widgetDefinitionRegistry.TryGetValue(widgetDefinitionId, out var value))
        {
            Log.Error($"Unknown widget DefinitionId: {widgetDefinitionId}");
            return;
        }

        if (runningWidgets.ContainsKey(widgetId))
        {
            Log.Warning($"Attempted to initialize a widget twice: {widgetDefinitionId} - {widgetId}");
            return;
        }

        var factory = value;
        var widgetImpl = factory.Create(widgetContext, state);
        runningWidgets.Add(widgetId, widgetImpl);
    }

    private void RecoverRunningWidgets()
    {
        WidgetInfo[] recoveredWidgets;
        try
        {
            recoveredWidgets = WidgetManager.GetDefault().GetWidgetInfos();

            if (recoveredWidgets is null)
            {
                Log.Debug("No running widgets to recover.");
                return;
            }
        }
        catch (Exception e)
        {
            Log.Error("Failed retrieving list of running widgets.", e);
            return;
        }

        foreach (var widgetInfo in recoveredWidgets)
        {
            if (!runningWidgets.ContainsKey(widgetInfo.WidgetContext.Id))
            {
                InitializeWidget(widgetInfo.WidgetContext, widgetInfo.CustomState);
            }
        }

        Log.Debug("Finished recovering widgets.");
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        Log.Information($"CreateWidget id: {widgetContext.Id} definitionId: {widgetContext.DefinitionId}");
        InitializeWidget(widgetContext, string.Empty);
    }

    public void Activate(WidgetContext widgetContext)
    {
        Log.Debug($"Activate id: {widgetContext.Id} definitionId: {widgetContext.DefinitionId}");
        var widgetId = widgetContext.Id;
        if (runningWidgets.TryGetValue(widgetId, out var runningWidget))
        {
            runningWidget.Activate(widgetContext);
        }
        else
        {
            // Called to activate a widget that we don't know about, which is unexpected. Try to recover by creating it.
            Log.Warning($"Found WidgetId that was not known: {widgetContext.Id}, attempting to recover by creating it.");
            CreateWidget(widgetContext);
            if (runningWidgets.TryGetValue(widgetId, out var recoveredWidget))
            {
                recoveredWidget.Activate(widgetContext);
            }
        }
    }

    public void Deactivate(string widgetId)
    {
        Log.Debug($"Deactivate id: {widgetId}");
        if (runningWidgets.TryGetValue(widgetId, out var value))
        {
            value.Deactivate(widgetId);
        }
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        Log.Information($"DeleteWidget id: {widgetId}");
        if (runningWidgets.TryGetValue(widgetId, out var value))
        {
            value.DeleteWidget(widgetId, customState);
            runningWidgets.Remove(widgetId);
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        Log.Debug($"OnActionInvoked id: {actionInvokedArgs.WidgetContext.Id} definitionId: {actionInvokedArgs.WidgetContext.DefinitionId}");
        var widgetContext = actionInvokedArgs.WidgetContext;
        var widgetId = widgetContext.Id;
        if (runningWidgets.TryGetValue(widgetId, out var value))
        {
            value.OnActionInvoked(actionInvokedArgs);
        }
    }

    public void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        Log.Debug($"OnCustomizationRequested id: {customizationRequestedArgs.WidgetContext.Id} definitionId: {customizationRequestedArgs.WidgetContext.DefinitionId}");
        var widgetContext = customizationRequestedArgs.WidgetContext;
        var widgetId = widgetContext.Id;
        if (runningWidgets.TryGetValue(widgetId, out var value))
        {
            value.OnCustomizationRequested(customizationRequestedArgs);
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        Log.Debug($"OnWidgetContextChanged id: {contextChangedArgs.WidgetContext.Id} definitionId: {contextChangedArgs.WidgetContext.DefinitionId}");
        var widgetContext = contextChangedArgs.WidgetContext;
        var widgetId = widgetContext.Id;
        if (runningWidgets.TryGetValue(widgetId, out var value))
        {
            value.OnWidgetContextChanged(contextChangedArgs);
        }
    }
}
