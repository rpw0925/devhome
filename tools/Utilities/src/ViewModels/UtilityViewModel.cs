// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHome.Telemetry;
using DevHome.Utilities.TelemetryEvents;
using Microsoft.UI.Xaml;
using Serilog;

namespace DevHome.Utilities.ViewModels;

public partial class UtilityViewModel : ObservableObject
{
    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(UtilityViewModel));

    private readonly string _exeName;

    public string Title { get; set; }

    public string Description { get; set; }

    public string NavigateUri { get; set; }

    public string ImageSource { get; set; }

    public ICommand LaunchCommand { get; set; }

    public Visibility SupportsLaunchAsAdmin { get; set; }

    [ObservableProperty]
    private bool _launchAsAdmin;

    public UtilityViewModel(string exeName)
    {
        this._exeName = exeName;
        LaunchCommand = new RelayCommand(Launch);
        _log.Information("UtilityViewModel created for Title: {Title}, exe: {ExeName}", Title, exeName);
    }

    private void Launch()
    {
        _log.Information($"Launching {_exeName}, as admin: {LaunchAsAdmin}");

        // We need to start the process with ShellExecute to run elevated
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _exeName,
            UseShellExecute = true,

            Verb = LaunchAsAdmin ? "runas" : "open",
        };

        var process = Process.Start(processStartInfo);
        if (process is null)
        {
            _log.Error("Failed to start process {ExeName}", _exeName);
            throw new InvalidOperationException("Failed to start process");
        }

        TelemetryFactory.Get<DevHome.Telemetry.ITelemetry>().Log("Utilities_UtilitiesLaunchEvent", LogLevel.Critical, new UtilitiesLaunchEvent(Title, LaunchAsAdmin), null);
    }
}
