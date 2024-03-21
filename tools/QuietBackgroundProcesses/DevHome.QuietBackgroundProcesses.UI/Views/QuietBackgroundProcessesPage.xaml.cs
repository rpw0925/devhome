// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHome.Common;
using DevHome.QuietBackgroundProcesses.UI.ViewModels;

namespace DevHome.QuietBackgroundProcesses.UI.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class QuietBackgroundProcessesPage : ToolPage
{
    public override string DisplayName =>
        new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader(
            "DevHome.QuietBackgroundProcesses.UI.pri",
            "DevHome.QuietBackgroundProcesses.UI/Resources").GetString("NavigationPane/Content");

    public QuietBackgroundProcessesViewModel ViewModel
    {
        get;
    }

    public QuietBackgroundProcessesPage()
    {
        ViewModel = new QuietBackgroundProcessesViewModel();
        InitializeComponent();
    }
}
