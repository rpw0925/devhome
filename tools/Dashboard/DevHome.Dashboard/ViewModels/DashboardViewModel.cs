// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHome.Common.Extensions;
using DevHome.Dashboard.Helpers;
using DevHome.Dashboard.Services;
using DevHome.Dashboard.TelemetryEvents;
using DevHome.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Widgets.Hosts;
using WinUIEx;

namespace DevHome.Dashboard.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IWidgetHostingService _widgetHostingService;

    private readonly IWidgetIconService _widgetIconService;

    private readonly WindowEx _windowEx;

    private readonly WidgetViewModelFactory _widgetViewModelFactory;

    public static ObservableCollection<WidgetViewModel> PinnedWidgets { get; set; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasWidgetService;

    [ObservableProperty]
    private bool _showRestartDevHomeMessage;

    [ObservableProperty]
    private bool _showUpdateWidgetsMessage;

    public DashboardViewModel(
        IWidgetHostingService widgetHostingService,
        IWidgetIconService widgetIconService,
        WindowEx windowEx,
        WidgetViewModelFactory widgetViewModelFactory)
    {
        _widgetIconService = widgetIconService;
        _widgetHostingService = widgetHostingService;
        _windowEx = windowEx;
        _widgetViewModelFactory = widgetViewModelFactory;

        PinnedWidgets = new ObservableCollection<WidgetViewModel>();
    }

    public Visibility GetNoWidgetMessageVisibility(int widgetCount, bool isLoading)
    {
        if (widgetCount == 0 && !isLoading && HasWidgetService)
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    [RelayCommand]
    private async Task OnActualThemeChangedAsync()
    {
        // A different host config is used to render widgets (adaptive cards) in light and dark themes.
        await Application.Current.GetService<IAdaptiveCardRenderingService>().UpdateHostConfig();

        // Re-render the widgets with the new theme and renderer.
        foreach (var widgetViewModel in PinnedWidgets)
        {
            await widgetViewModel.RenderAsync();
        }
    }

    [RelayCommand]
    private async Task OnLoadedAsync()
    {
        await InitializeDashboard();
    }

    private async Task InitializeDashboard()
    {
        IsLoading = true;

        if (await _widgetHostingService.EnsureWidgetServiceAsync())
        {
            HasWidgetService = true;
            await SubscribeToWidgetCatalogEventsAsync();

            // Cache the widget icons before we display the widgets, since we include the icons in the widgets.
            await _widgetIconService.CacheAllWidgetIconsAsync();

            await RestorePinnedWidgetsAsync();
        }
        else
        {
            var widgetServiceState = _widgetHostingService.GetWidgetServiceState();
            if (widgetServiceState == WidgetHostingService.WidgetServiceStates.HasStoreWidgetServiceNoOrBadVersion)
            {
                // Show error message that restarting Dev Home may help
                ShowRestartDevHomeMessage = true;
            }
            else if (widgetServiceState == WidgetHostingService.WidgetServiceStates.HasWebExperienceNoOrBadVersion)
            {
                // Show error message that updating may help
                ShowUpdateWidgetsMessage = true;
            }
            else
            {
                Log.Logger()?.ReportError("DashboardView", $"Initialization failed, WidgetServiceState unknown");
                ShowRestartDevHomeMessage = true;
            }
        }

        IsLoading = false;
    }

    private async Task<bool> SubscribeToWidgetCatalogEventsAsync()
    {
        Log.Logger()?.ReportInfo("DashboardView", "SubscribeToWidgetCatalogEvents");

        try
        {
            var widgetCatalog = await _widgetHostingService.GetWidgetCatalogAsync();
            if (widgetCatalog == null)
            {
                return false;
            }

            widgetCatalog!.WidgetProviderDefinitionAdded += WidgetCatalog_WidgetProviderDefinitionAdded;
            widgetCatalog!.WidgetProviderDefinitionDeleted += WidgetCatalog_WidgetProviderDefinitionDeleted;
            widgetCatalog!.WidgetDefinitionAdded += WidgetCatalog_WidgetDefinitionAdded;
            widgetCatalog!.WidgetDefinitionUpdated += WidgetCatalog_WidgetDefinitionUpdated;
            widgetCatalog!.WidgetDefinitionDeleted += WidgetCatalog_WidgetDefinitionDeleted;
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError("DashboardView", "Exception in SubscribeToWidgetCatalogEvents:", ex);
            return false;
        }

        return true;
    }

    private async Task RestorePinnedWidgetsAsync()
    {
        Log.Logger()?.ReportInfo("DashboardView", "Get widgets for current host");
        var widgetHost = await _widgetHostingService.GetWidgetHostAsync();
        var hostWidgets = await Task.Run(() => widgetHost?.GetWidgets());

        if (hostWidgets == null)
        {
            Log.Logger()?.ReportInfo("DashboardView", $"Found 0 widgets for this host");
            return;
        }

        Log.Logger()?.ReportInfo("DashboardView", $"Found {hostWidgets.Length} widgets for this host");
        var restoredWidgetsWithPosition = new SortedDictionary<int, Widget>();
        var restoredWidgetsWithoutPosition = new SortedDictionary<int, Widget>();
        var numUnorderedWidgets = 0;

        // Widgets do not come from the host in a deterministic order, so save their order in each widget's CustomState.
        // Iterate through all the widgets and put them in order. If a widget does not have a position assigned to it,
        // append it at the end. If a position is missing, just show the next widget in order.
        foreach (var widget in hostWidgets)
        {
            try
            {
                var stateStr = await widget.GetCustomStateAsync();
                Log.Logger()?.ReportInfo("DashboardView", $"GetWidgetCustomState: {stateStr}");

                if (string.IsNullOrEmpty(stateStr))
                {
                    // If we have a widget with no state, Dev Home does not consider it a valid widget
                    // and should delete it, rather than letting it run invisibly in the background.
                    await DeleteAbandonedWidgetAsync(widget, widgetHost);
                    continue;
                }

                var stateObj = System.Text.Json.JsonSerializer.Deserialize(stateStr, SourceGenerationContext.Default.WidgetCustomState);
                if (stateObj.Host != WidgetHelpers.DevHomeHostName)
                {
                    // This shouldn't be able to be reached
                    Log.Logger()?.ReportError("DashboardView", $"Widget has custom state but no HostName.");
                    continue;
                }

                var position = stateObj.Position;
                if (position >= 0)
                {
                    if (!restoredWidgetsWithPosition.TryAdd(position, widget))
                    {
                        // If there was an error and a widget with this position is already there,
                        // treat this widget as unordered and put it into the unordered map.
                        restoredWidgetsWithoutPosition.Add(numUnorderedWidgets++, widget);
                    }
                }
                else
                {
                    // Widgets with no position will get the default of -1. Append these at the end.
                    restoredWidgetsWithoutPosition.Add(numUnorderedWidgets++, widget);
                }
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError("DashboardView", $"RestorePinnedWidgets(): ", ex);
            }
        }

        // Now that we've ordered the widgets, put them in their final collection.
        var finalPlace = 0;
        foreach (var orderedWidget in restoredWidgetsWithPosition)
        {
            await PlaceWidget(orderedWidget, finalPlace++);
        }

        foreach (var orderedWidget in restoredWidgetsWithoutPosition)
        {
            await PlaceWidget(orderedWidget, finalPlace++);
        }
    }

    private async Task DeleteAbandonedWidgetAsync(Widget widget, WidgetHost widgetHost)
    {
        var length = await Task.Run(() => widgetHost!.GetWidgets().Length);
        Log.Logger()?.ReportInfo("DashboardView", $"Found abandoned widget, try to delete it...");
        Log.Logger()?.ReportInfo("DashboardView", $"Before delete, {length} widgets for this host");

        await widget.DeleteAsync();

        var newWidgetList = await Task.Run(() => widgetHost.GetWidgets());
        length = (newWidgetList == null) ? 0 : newWidgetList.Length;
        Log.Logger()?.ReportInfo("DashboardView", $"After delete, {length} widgets for this host");
    }

    public async Task PlaceWidget(KeyValuePair<int, Widget> orderedWidget, int finalPlace)
    {
        var widget = orderedWidget.Value;
        await InsertWidgetInPinnedWidgetsAsync(widget, finalPlace);
        await WidgetHelpers.SetPositionCustomStateAsync(widget, finalPlace);
    }

    private async Task InsertWidgetInPinnedWidgetsAsync(Widget widget, int index)
    {
        await Task.Run(async () =>
        {
            var widgetDefinitionId = widget.DefinitionId;
            var widgetId = widget.Id;
            var widgetCatalog = await _widgetHostingService.GetWidgetCatalogAsync();
            var widgetDefinition = await Task.Run(() => widgetCatalog?.GetWidgetDefinition(widgetDefinitionId));

            if (widgetDefinition != null)
            {
                Log.Logger()?.ReportInfo("DashboardView", $"Insert widget in pinned widgets, id = {widgetId}, index = {index}");

                TelemetryFactory.Get<ITelemetry>().Log(
                    "Dashboard_ReportPinnedWidget",
                    LogLevel.Critical,
                    new ReportPinnedWidgetEvent(widgetDefinition.ProviderDefinition.Id, widgetDefinitionId));

                var widgetSize = await widget.GetSizeAsync();
                var wvm = _widgetViewModelFactory(widget, widgetSize, widgetDefinition);
                _windowEx.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        PinnedWidgets.Insert(index, wvm);
                    }
                    catch (Exception ex)
                    {
                        // TODO Support concurrency in dashboard. Today concurrent async execution can cause insertion errors.
                        // https://github.com/microsoft/devhome/issues/1215
                        Log.Logger()?.ReportWarn("DashboardView", $"Couldn't insert pinned widget", ex);
                    }
                });
            }
            else
            {
                // If the widget provider was uninstalled while we weren't running, the catalog won't have the definition so delete the widget.
                Log.Logger()?.ReportInfo("DashboardView", $"No widget definition '{widgetDefinitionId}', delete widget {widgetId} with that definition");
                try
                {
                    await widget.SetCustomStateAsync(string.Empty);
                    await widget.DeleteAsync();
                }
                catch (Exception ex)
                {
                    Log.Logger()?.ReportInfo("DashboardView", $"Error deleting widget", ex);
                }
            }
        });
    }

    public async Task InsertNewWidgetAsync(WidgetDefinition newWidgetDefinition)
    {
        try
        {
            var size = WidgetHelpers.GetDefaultWidgetSize(newWidgetDefinition.GetWidgetCapabilities());
            var widgetHost = await _widgetHostingService.GetWidgetHostAsync();
            var newWidget = await Task.Run(async () => await widgetHost?.CreateWidgetAsync(newWidgetDefinition.Id, size));

            // Set custom state on new widget.
            var position = PinnedWidgets.Count;
            var newCustomState = WidgetHelpers.CreateWidgetCustomState(position);
            Log.Logger()?.ReportDebug("DashboardView", $"SetCustomState: {newCustomState}");
            await newWidget.SetCustomStateAsync(newCustomState);

            // Put new widget on the Dashboard.
            await InsertWidgetInPinnedWidgetsAsync(newWidget, position);
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportWarn("AddWidgetDialog", $"Creating widget failed: ", ex);
        }
    }

    private void WidgetCatalog_WidgetProviderDefinitionAdded(WidgetCatalog sender, WidgetProviderDefinitionAddedEventArgs args)
    {
        Log.Logger()?.ReportInfo("DashboardView", $"WidgetCatalog_WidgetProviderDefinitionAdded {args.ProviderDefinition.Id}");
    }

    private void WidgetCatalog_WidgetProviderDefinitionDeleted(WidgetCatalog sender, WidgetProviderDefinitionDeletedEventArgs args)
    {
        Log.Logger()?.ReportInfo("DashboardView", $"WidgetCatalog_WidgetProviderDefinitionDeleted {args.ProviderDefinitionId}");
    }

    private async void WidgetCatalog_WidgetDefinitionAdded(WidgetCatalog sender, WidgetDefinitionAddedEventArgs args)
    {
        Log.Logger()?.ReportInfo("DashboardView", $"WidgetCatalog_WidgetDefinitionAdded {args.Definition.Id}");
        await _widgetIconService.AddIconsToCacheAsync(args.Definition);
    }

    private async void WidgetCatalog_WidgetDefinitionUpdated(WidgetCatalog sender, WidgetDefinitionUpdatedEventArgs args)
    {
        var updatedDefinitionId = args.Definition.Id;
        Log.Logger()?.ReportInfo("DashboardView", $"WidgetCatalog_WidgetDefinitionUpdated {updatedDefinitionId}");

        foreach (var widgetToUpdate in PinnedWidgets.Where(x => x.Widget.DefinitionId == updatedDefinitionId).ToList())
        {
            // Things in the definition that we need to update to if they have changed:
            // AllowMultiple, DisplayTitle, Capabilities (size), ThemeResource (icons)
            var oldDef = widgetToUpdate.WidgetDefinition;
            var newDef = args.Definition;

            // If we're no longer allowed to have multiple instances of this widget, delete all of them.
            if (newDef.AllowMultiple == false && oldDef.AllowMultiple == true)
            {
                _windowEx.DispatcherQueue.TryEnqueue(async () =>
                {
                    Log.Logger()?.ReportInfo("DashboardView", $"No longer allowed to have multiple of widget {newDef.Id}");
                    Log.Logger()?.ReportInfo("DashboardView", $"Delete widget {widgetToUpdate.Widget.Id}");
                    PinnedWidgets.Remove(widgetToUpdate);
                    await widgetToUpdate.Widget.DeleteAsync();
                    Log.Logger()?.ReportInfo("DashboardView", $"Deleted Widget {widgetToUpdate.Widget.Id}");
                });
            }
            else
            {
                // Changing the definition updates the DisplayTitle.
                widgetToUpdate.WidgetDefinition = newDef;

                // If the size the widget is currently set to is no longer supported by the widget, revert to its default size.
                // TODO: Need to update WidgetControl with now-valid sizes.
                // TODO: Properly compare widget capabilities.
                // https://github.com/microsoft/devhome/issues/641
                if (oldDef.GetWidgetCapabilities() != newDef.GetWidgetCapabilities())
                {
                    // TODO: handle the case where this change is made while Dev Home is not running -- how do we restore?
                    // https://github.com/microsoft/devhome/issues/641
                    if (!newDef.GetWidgetCapabilities().Any(cap => cap.Size == widgetToUpdate.WidgetSize))
                    {
                        var newDefaultSize = WidgetHelpers.GetDefaultWidgetSize(newDef.GetWidgetCapabilities());
                        widgetToUpdate.WidgetSize = newDefaultSize;
                        await widgetToUpdate.Widget.SetSizeAsync(newDefaultSize);
                    }
                }
            }

            // TODO: ThemeResource (icons) changed.
            // https://github.com/microsoft/devhome/issues/641
        }
    }

    // Remove widget(s) from the Dashboard if the provider deletes the widget definition, or the provider is uninstalled.
    private void WidgetCatalog_WidgetDefinitionDeleted(WidgetCatalog sender, WidgetDefinitionDeletedEventArgs args)
    {
        var definitionId = args.DefinitionId;
        _windowEx.DispatcherQueue.TryEnqueue(async () =>
        {
            Log.Logger()?.ReportInfo("DashboardView", $"WidgetDefinitionDeleted {definitionId}");
            foreach (var widgetToRemove in PinnedWidgets.Where(x => x.Widget.DefinitionId == definitionId).ToList())
            {
                Log.Logger()?.ReportInfo("DashboardView", $"Remove widget {widgetToRemove.Widget.Id}");
                PinnedWidgets.Remove(widgetToRemove);

                // The widget definition is gone, so delete widgets with that definition.
                await widgetToRemove.Widget.DeleteAsync();
            }
        });

        _widgetIconService.RemoveIconsFromCache(definitionId);
    }
}
