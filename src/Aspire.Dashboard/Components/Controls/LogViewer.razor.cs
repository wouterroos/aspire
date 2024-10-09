// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Aspire;
using Turbine.Dashboard.Components.Resize;
using Turbine.Dashboard.Configuration;
using Turbine.Dashboard.ConsoleLogs;
using Turbine.Dashboard.Extensions;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Turbine.Dashboard.Components;

/// <summary>
/// A log viewing UI component that shows a live view of a log, with syntax highlighting and automatic scrolling.
/// </summary>
public sealed partial class LogViewer
{
    private readonly CancellationSeries _cancellationSeries = new();
    private bool _convertTimestampsFromUtc;
    private bool _applicationChanged;

    [Inject]
    public required BrowserTimeProvider TimeProvider { get; init; }

    [Inject]
    public required DimensionManager DimensionManager { get; set; }

    [Inject]
    public required IOptions<DashboardOptions> Options { get; set; }

    public LogEntries LogEntries { get; set; } = null!;

    public string? ResourceName { get; set; }

    protected override void OnInitialized()
    {
        LogEntries = new(Options.Value.Frontend.MaxConsoleLogCount);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_applicationChanged)
        {
            await JS.InvokeVoidAsync("resetContinuousScrollPosition");
            _applicationChanged = false;
        }
        if (firstRender)
        {
            await JS.InvokeVoidAsync("initializeContinuousScroll");
            DimensionManager.OnBrowserDimensionsChanged += OnBrowserResize;
        }
    }

    private void OnBrowserResize(object? o, EventArgs args)
    {
        InvokeAsync(async () =>
        {
            await JS.InvokeVoidAsync("resetContinuousScrollPosition");
            await JS.InvokeVoidAsync("initializeContinuousScroll");
        });
    }

    internal async Task SetLogSourceAsync(string resourceName, IAsyncEnumerable<IReadOnlyList<ResourceLogLine>> batches, bool convertTimestampsFromUtc)
    {
        ResourceName = resourceName;

        System.Diagnostics.Debug.Assert(LogEntries.GetEntries().Count == 0, "Expecting zero log entries");

        _convertTimestampsFromUtc = convertTimestampsFromUtc;

        CancellationToken cancellationToken = await _cancellationSeries.NextAsync();
        LogParser? logParser = new LogParser();

        // This needs to stay on the UI thread since we raise StateHasChanged() in the loop (hence the ConfigureAwait(true)).
        await foreach (IReadOnlyList<ResourceLogLine>? batch in batches.WithCancellation(cancellationToken).ConfigureAwait(true))
        {
            if (batch.Count is 0)
            {
                continue;
            }

            foreach ((int lineNumber, string? content, bool isErrorOutput) in batch)
            {
                LogEntries.InsertSorted(logParser.CreateLogEntry(content, isErrorOutput), lineNumber);
            }

            StateHasChanged();
        }
    }

    private string GetDisplayTimestamp(DateTimeOffset timestamp)
    {
        if (_convertTimestampsFromUtc)
        {
            timestamp = TimeProvider.ToLocal(timestamp);
        }

        return timestamp.ToString(KnownFormats.ConsoleLogsTimestampFormat, CultureInfo.InvariantCulture);
    }

    internal async Task ClearLogsAsync()
    {
        await _cancellationSeries.ClearAsync();

        _applicationChanged = true;
        LogEntries.Clear();
        ResourceName = null;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellationSeries.ClearAsync();
        DimensionManager.OnBrowserDimensionsChanged -= OnBrowserResize;
    }
}