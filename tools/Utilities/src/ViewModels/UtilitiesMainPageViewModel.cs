﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using DevHome.Common.Services;

namespace DevHome.Utilities.ViewModels;

public partial class UtilitiesMainPageViewModel : ObservableObject
{
    public ObservableCollection<UtilityViewModel> Utilities { get; set; }

    public UtilitiesMainPageViewModel()
    {
        var stringResource = new StringResource("DevHome.Utilities.pri", "DevHome.Utilities/Resources");

        Utilities = new ObservableCollection<UtilityViewModel>
        {
            new("DevHome.HostsFileEditorApp.exe")
            {
                Title = stringResource.GetLocalized("HostsFileEditorUtilityTitle"),
                Description = stringResource.GetLocalized("HostsFileEditorUtilityDesc"),
                NavigateUri = "https://go.microsoft.com/fwlink/?Linkid=2271355",
                ImageSource = Path.Combine(AppContext.BaseDirectory, "Assets\\HostsUILib", "Hosts.ico"),
            },
            new("DevHome.RegistryPreviewApp.exe")
            {
                Title = stringResource.GetLocalized("RegistryPreviewUtilityTitle"),
                Description = stringResource.GetLocalized("RegistryPreviewUtilityDesc"),
                NavigateUri = "https://go.microsoft.com/fwlink/?Linkid=2270966",
                ImageSource = Path.Combine(AppContext.BaseDirectory, "Assets\\RegistryPreview", "RegistryPreview.ico"),
            },
            new("DevHome.EnvironmentVariablesApp.exe")
            {
                Title = stringResource.GetLocalized("EnvVariablesEditorUtilityTitle"),
                Description = stringResource.GetLocalized("EnvVariablesEditorUtilityDesc"),
                NavigateUri = "https://go.microsoft.com/fwlink/?Linkid=2270894",
                ImageSource = Path.Combine(AppContext.BaseDirectory, "Assets\\EnvironmentVariables", "EnvironmentVariables.ico"),
            },
        };
    }
}
