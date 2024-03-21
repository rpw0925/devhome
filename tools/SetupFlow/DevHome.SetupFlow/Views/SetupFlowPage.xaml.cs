// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHome.Common;
using DevHome.Common.Extensions;
using DevHome.SetupFlow.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DevHome.SetupFlow.Views;

public partial class SetupFlowPage : ToolPage
{
    public override string DisplayName =>
        new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader(
            "DevHome.SetupFlow.pri",
            "DevHome.SetupFlow/Resources").GetString("NavigationPane/Content");

    public SetupFlowViewModel ViewModel { get; }

    public SetupFlowPage()
    {
        ViewModel = Application.Current.GetService<SetupFlowViewModel>();
        InitializeComponent();
    }
}
