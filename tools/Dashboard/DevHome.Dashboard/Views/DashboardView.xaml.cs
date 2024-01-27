// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DevHome.Common;
using DevHome.Common.Extensions;
using DevHome.Dashboard.Controls;
using DevHome.Dashboard.Helpers;
using DevHome.Dashboard.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Widgets.Hosts;
using Windows.System;

namespace DevHome.Dashboard.Views;

public partial class DashboardView : ToolPage
{
    public override string ShortName => "Dashboard";

    public DashboardViewModel ViewModel { get; }

    internal DashboardBannerViewModel BannerViewModel { get; }

    private static Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

    private const string DraggedWidget = "DraggedWidget";
    private const string DraggedIndex = "DraggedIndex";

    public DashboardView()
    {
        ViewModel = Application.Current.GetService<DashboardViewModel>();
        BannerViewModel = Application.Current.GetService<DashboardBannerViewModel>();

        this.InitializeComponent();

        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

#if DEBUG
        Loaded += AddResetButton;
#endif
    }

    [RelayCommand]
    public async Task GoToWidgetsInStoreAsync()
    {
        await Launcher.LaunchUriAsync(new ("ms-windows-store://pdp/?productid=9MSSGKG348SP"));
    }

    [RelayCommand]
    public async Task AddWidgetClickAsync()
    {
        var dialog = new AddWidgetDialog(ActualTheme)
        {
            // XamlRoot must be set in the case of a ContentDialog running in a Desktop app.
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme,
        };

        _ = await dialog.ShowAsync();

        var newWidgetDefinition = dialog.AddedWidget;

        if (newWidgetDefinition != null)
        {
            await ViewModel.InsertNewWidgetAsync(newWidgetDefinition);
        }
    }

    private void WidgetGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        Log.Logger()?.ReportDebug("DashboardView", $"Drag starting");

        // When drag starts, save the WidgetViewModel and the original index of the widget being dragged.
        var draggedObject = e.Items.FirstOrDefault();
        var draggedWidgetViewModel = draggedObject as WidgetViewModel;
        e.Data.Properties.Add(DraggedWidget, draggedWidgetViewModel);
        e.Data.Properties.Add(DraggedIndex, ViewModel.PinnedWidgets.IndexOf(draggedWidgetViewModel));
    }

    private void WidgetControl_DragOver(object sender, DragEventArgs e)
    {
        // A widget may be dropped on top of another widget, in which case the dropped widget will take the target widget's place.
        if (e.Data != null)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
        else
        {
            // If the dragged item doesn't have a DataPackage, don't allow it to be dropped.
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
        }
    }

    private async void WidgetControl_Drop(object sender, DragEventArgs e)
    {
        Log.Logger()?.ReportDebug("DashboardView", $"Drop starting");

        // If the the thing we're dragging isn't a widget, it might not have a DataPackage and we shouldn't do anything with it.
        if (e.Data == null)
        {
            return;
        }

        // When drop happens, get the original index of the widget that was dragged and dropped.
        var result = e.Data.Properties.TryGetValue(DraggedIndex, out var draggedIndexObject);
        if (!result || draggedIndexObject == null)
        {
            return;
        }

        var draggedIndex = (int)draggedIndexObject;

        // Get the index of the widget that was dropped onto -- the dragged widget will take the place of this one,
        // and this widget and all subsequent widgets will move over to the right.
        var droppedControl = sender as WidgetControl;
        var droppedIndex = WidgetGridView.Items.IndexOf(droppedControl.WidgetSource);
        Log.Logger()?.ReportInfo("DashboardView", $"Widget dragged from index {draggedIndex} to {droppedIndex}");

        // If the widget is dropped at the position it's already at, there's nothing to do.
        if (draggedIndex == droppedIndex)
        {
            return;
        }

        result = e.Data.Properties.TryGetValue(DraggedWidget, out var draggedObject);
        if (!result || draggedObject == null)
        {
            return;
        }

        var draggedWidgetViewModel = draggedObject as WidgetViewModel;

        // Remove the moved widget then insert it back in the collection at the new location. If the dropped widget was
        // moved from a lower index to a higher one, removing the moved widget before inserting it will ensure that any
        // widgets between the starting and ending indices move up to replace the removed widget. If the widget was
        // moved from a higher index to a lower one, then the order of removal and insertion doesn't matter.
        ViewModel.PinnedWidgets.RemoveAt(draggedIndex);
        var widgetPair = new KeyValuePair<int, Widget>(droppedIndex, draggedWidgetViewModel.Widget);
        await ViewModel.PlaceWidget(widgetPair, droppedIndex);

        // Update the CustomState Position of any widgets that were moved.
        // The widget that has been dropped has already been updated, so don't do it again here.
        var startIndex = draggedIndex < droppedIndex ? draggedIndex : droppedIndex + 1;
        var endIndex = draggedIndex < droppedIndex ? droppedIndex : draggedIndex + 1;
        for (var i = startIndex; i < endIndex; i++)
        {
            var widgetToUpdate = ViewModel.PinnedWidgets.ElementAt(i);
            await WidgetHelpers.SetPositionCustomStateAsync(widgetToUpdate.Widget, i);
        }

        Log.Logger()?.ReportDebug("DashboardView", $"Drop ended");
    }

#if DEBUG
    private void AddResetButton(object sender, RoutedEventArgs e)
    {
        var resetButton = new Button
        {
            Content = new SymbolIcon(Symbol.Refresh),
            HorizontalAlignment = HorizontalAlignment.Right,
            FontSize = 4,
        };
        resetButton.Click += ResetButton_Click;
        AutomationProperties.SetName(resetButton, "ResetBannerButton");
        var parent = AddWidgetButton.Parent as StackPanel;
        var index = parent.Children.IndexOf(AddWidgetButton);
        parent.Children.Insert(index + 1, resetButton);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var roamingProperties = Windows.Storage.ApplicationData.Current.RoamingSettings.Values;
        if (roamingProperties.ContainsKey("HideDashboardBanner"))
        {
            roamingProperties.Remove("HideDashboardBanner");
        }

        BannerViewModel.ResetDashboardBanner();
    }
#endif
}
