﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;

namespace DevHome.SetupFlow.ViewModels;

public partial class InstallPackageSummaryInformationViewModel : ObservableRecipient, ISummaryInformationViewModel
{
    public bool HasContent => false;
}
