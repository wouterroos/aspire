// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Turbine.Dashboard.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Turbine.Dashboard.Components;

public sealed partial class ApplicationName : ComponentBase, IDisposable
{
    private CancellationTokenSource? _disposalCts;

    [Parameter]
    public string? ResourceName { get; init; }

    [Parameter]
    public IStringLocalizer? Loc { get; init; }

    [Inject]
    public required IDashboardClient DashboardClient { get; init; }

    private string? _applicationName;

    protected override async Task OnInitializedAsync()
    {
        // We won't have an application name until the client has connected to the server.
        if (DashboardClient.IsEnabled && !DashboardClient.WhenConnected.IsCompletedSuccessfully)
        {
            _disposalCts = new CancellationTokenSource();
            await DashboardClient.WhenConnected.WaitAsync(_disposalCts.Token);
        }

        if (ResourceName is not null && Loc is not null)
        {
            _applicationName = string.Format(CultureInfo.InvariantCulture, Loc[ResourceName], DashboardClient.ApplicationName);
        }
        else
        {
            _applicationName = DashboardClient.ApplicationName;
        }
    }

    public void Dispose()
    {
        _disposalCts?.Cancel();
        _disposalCts?.Dispose();
    }
}