// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHome.Common.Services;
using DevHome.Customization.Views;
using Microsoft.UI.Xaml.Controls;

namespace DevHome.Customization.ViewModels.DevDriveInsights;

/// <summary>
/// View model for the card that represents a dev drive optimizer on the dev drive insights page.
/// </summary>
public partial class DevDriveOptimizerCardViewModel : ObservableObject
{
    public OptimizeDevDriveDialogViewModelFactory OptimizeDevDriveDialogViewModelFactory { get; set; }

    public string CacheToBeMoved { get; set; }

    public string DevDriveOptimizationSuggestion { get; set; }

    public string ExistingCacheLocation { get; set; }

    public string ExampleLocationOnDevDrive { get; set; }

    public string EnvironmentVariableToBeSet { get; set; }

    public string OptimizerDevDriveDescription { get; set; }

    public string MakeTheChangeText { get; set; }

    /// <summary>
    /// User wants to optimize a dev drive.
    /// </summary>
    [RelayCommand]
    private async Task OptimizeDevDriveAsync(object sender)
    {
        var settingsCard = sender as Button;
        if (settingsCard != null)
        {
            var optimizeDevDriveViewModel = OptimizeDevDriveDialogViewModelFactory(ExistingCacheLocation, EnvironmentVariableToBeSet);
            var optimizeDevDriveDialog = new OptimizeDevDriveDialog(optimizeDevDriveViewModel);
            optimizeDevDriveDialog.XamlRoot = settingsCard.XamlRoot;
            optimizeDevDriveDialog.RequestedTheme = settingsCard.ActualTheme;
            await optimizeDevDriveDialog.ShowAsync();
        }
    }

    public DevDriveOptimizerCardViewModel(OptimizeDevDriveDialogViewModelFactory optimizeDevDriveDialogViewModelFactory, string cacheToBeMoved, string existingCacheLocation, string exampleLocationOnDevDrive, string environmentVariableToBeSet)
    {
        OptimizeDevDriveDialogViewModelFactory = optimizeDevDriveDialogViewModelFactory;
        CacheToBeMoved = cacheToBeMoved;
        ExistingCacheLocation = existingCacheLocation;
        ExampleLocationOnDevDrive = exampleLocationOnDevDrive;
        EnvironmentVariableToBeSet = environmentVariableToBeSet;
        var stringResource = new StringResource("DevHome.Customization.pri", "DevHome.Customization/Resources");
        OptimizerDevDriveDescription = stringResource.GetLocalized("OptimizerDevDriveDescription", EnvironmentVariableToBeSet, ExistingCacheLocation, ExampleLocationOnDevDrive, EnvironmentVariableToBeSet);
        MakeTheChangeText = stringResource.GetLocalized("MakeTheChangeText");
        DevDriveOptimizationSuggestion = stringResource.GetLocalized("DevDriveOptimizationSuggestion");
    }
}
