// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Turbine.Dashboard.Components.Controls.Chart;
using Turbine.Dashboard.Components.Resize;
using Turbine.Dashboard.Extensions;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Model.Otlp;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Resources;
using Turbine.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Turbine.Dashboard.Components;

public partial class PlotlyChart : ChartBase
{
    private static int s_nextChartId;

    [Inject]
    public required IJSRuntime JS { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required IDialogService DialogService { get; init; }

    public string ChartDivId { get; } = $"plotly-chart-container-{Interlocked.Increment(ref s_nextChartId)}";

    [CascadingParameter]
    public required ViewportInformation ViewportInformation { get; init; }

    private DotNetObjectReference<ChartInterop>? _chartInteropReference;
    private IJSObjectReference? _jsModule;

    private string FormatTooltip(string title, double yValue, DateTimeOffset xValue)
    {
        string? formattedValue = FormatHelpers.FormatNumberWithOptionalDecimalPlaces(yValue, maxDecimalPlaces: 3, CultureInfo.CurrentCulture);
        if (InstrumentViewModel?.Instrument is { } instrument)
        {
            formattedValue += " " + InstrumentUnitResolver.ResolveDisplayedUnit(instrument, titleCase: false, pluralize: yValue != 1);
        }
        return $"""
                <b>{HttpUtility.HtmlEncode(title)}</b><br />
                {Loc[nameof(ControlsStrings.PlotlyChartValue)]}: {formattedValue}<br />
                {Loc[nameof(ControlsStrings.PlotlyChartTime)]}: {FormatHelpers.FormatTime(TimeProvider, TimeProvider.ToLocal(xValue))}
                """;
    }

    protected override async Task OnChartUpdatedAsync(List<ChartTrace> traces, List<DateTimeOffset> xValues, List<ChartExemplar> exemplars, bool tickUpdate, DateTimeOffset inProgressDataTime, CancellationToken cancellationToken)
    {
        Debug.Assert(_jsModule != null, "The module should be initialized before chart data is sent to control.");

        PlotlyTrace[]? traceDtos = traces.Select(t => new PlotlyTrace
        {
            Name = t.Name,
            Y = t.DiffValues,
            X = xValues,
            Tooltips = t.Tooltips,
            TraceData = new List<object?>()
        }).ToArray();

        PlotlyTrace? exemplarTraceDto = CalculateExemplarsTrace(xValues, exemplars);

        if (!tickUpdate)
        {
            // The chart mostly shows numbers but some localization is needed for displaying time ticks.
            bool is24Hour = DateTimeFormatInfo.CurrentInfo.LongTimePattern.StartsWith("H", StringComparison.Ordinal);
            // Plotly uses d3-time-format https://d3js.org/d3-time-format
            string? time = is24Hour ? "%H:%M:%S" : "%-I:%M:%S %p";
            PlotlyUserLocale? userLocale = new PlotlyUserLocale
            {
                Periods = [DateTimeFormatInfo.CurrentInfo.AMDesignator, DateTimeFormatInfo.CurrentInfo.PMDesignator],
                Time = time
            };

            _chartInteropReference?.Dispose();
            _chartInteropReference = DotNetObjectReference.Create(new ChartInterop(this));

            await _jsModule.InvokeVoidAsync(
                "initializeChart",
                ChartDivId,
                traceDtos,
                exemplarTraceDto,
                TimeProvider.ToLocal(inProgressDataTime),
                TimeProvider.ToLocal(inProgressDataTime - Duration).ToLocalTime(),
                userLocale,
                _chartInteropReference).ConfigureAwait(false);
        }
        else
        {
            await _jsModule.InvokeVoidAsync(
                "updateChart",
                ChartDivId,
                traceDtos,
                exemplarTraceDto,
                TimeProvider.ToLocal(inProgressDataTime),
                TimeProvider.ToLocal(inProgressDataTime - Duration)).ConfigureAwait(false);
        }
    }

    private PlotlyTrace CalculateExemplarsTrace(List<DateTimeOffset> xValues, List<ChartExemplar> exemplars)
    {
        // In local development there is no sampling of traces. There could be a very high number.
        // Too many points on the graph will impact browser performance, and is not useful anyway as they will
        // draw on top of each other and can't be used. Fix both of these problems by enforcing a maximum limit.
        //
        // Displaying up to a maximum number of exemplars per tick will display a continuous number of ticks across the graph.
        const int MaxExemplarsPerTick = 20;

        // Group exemplars into ticks based on xValues.
        Dictionary<ExemplarGroupKey, List<ChartExemplar>>? exemplarGroups = new Dictionary<ExemplarGroupKey, List<ChartExemplar>>();
        for (int i = 0; i <= xValues.Count; i++)
        {
            DateTimeOffset? start = i > 0 ? xValues[i - 1] : (DateTimeOffset?)null;
            DateTimeOffset? end = i < xValues.Count ? xValues[i] : (DateTimeOffset?)null;
            ExemplarGroupKey g = new ExemplarGroupKey(start, end);

            List<ChartExemplar>? groupExemplars = exemplars.Where(e => (e.Start >= g.Start || g.Start == null) && (e.Start < g.End || g.End == null)).ToList();

            // When exemplars exceeds the limit then sample the exemplars to reduce data to the limit.
            if (groupExemplars.Count > MaxExemplarsPerTick)
            {
                double step = (double)groupExemplars.Count / MaxExemplarsPerTick;

                List<ChartExemplar>? sampledList = new List<ChartExemplar>(MaxExemplarsPerTick);
                for (int j = 0; j < MaxExemplarsPerTick; j++)
                {
                    // Calculate the index to take from the original list
                    int index = (int)Math.Floor(j * step);
                    sampledList.Add(groupExemplars[index]);
                }

                groupExemplars = sampledList;
            }

            exemplarGroups.Add(g, groupExemplars);
        }

        PlotlyTrace? exemplarTraceDto = new PlotlyTrace
        {
            Name = Loc[nameof(ControlsStrings.PlotlyChartExemplars)],
            Y = new List<double?>(),
            X = new List<DateTimeOffset>(),
            Tooltips = new List<string?>(),
            TraceData = new List<object?>()
        };

        foreach (ChartExemplar? exemplar in exemplarGroups.SelectMany(g => g.Value))
        {
            string? title = exemplar.Span != null
                ? SpanWaterfallViewModel.GetTitle(exemplar.Span, Applications)
                : $"{Loc[nameof(ControlsStrings.PlotlyChartTrace)]}: {OtlpHelpers.ToShortenedId(exemplar.TraceId)}";
            string? tooltip = FormatTooltip(title, exemplar.Value, exemplar.Start);

            exemplarTraceDto.X.Add(exemplar.Start);
            exemplarTraceDto.Y.Add(exemplar.Value);
            exemplarTraceDto.Tooltips.Add(tooltip);
            exemplarTraceDto.TraceData.Add(new { TraceId = exemplar.TraceId, SpanId = exemplar.SpanId });
        }

        return exemplarTraceDto;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "/js/app-metrics.js");
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    // The first data is used to initialize the chart. The module needs to be ready first.
    protected override bool ReadyForData() => _jsModule != null;

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        await base.DisposeAsync(disposing);

        if (disposing)
        {
            _chartInteropReference?.Dispose();
            await JSInteropHelpers.SafeDisposeAsync(_jsModule);
        }
    }

    /// <summary>
    /// Handle user clicking on a trace point in the browser.
    /// </summary>
    private sealed class ChartInterop
    {
        private readonly PlotlyChart _plotlyChart;

        public ChartInterop(PlotlyChart plotlyChart)
        {
            _plotlyChart = plotlyChart;
        }

        [JSInvokable]
        public async Task ViewSpan(string traceId, string spanId)
        {
            bool available = await MetricsHelpers.WaitForSpanToBeAvailableAsync(
                traceId,
                spanId,
                _plotlyChart.TelemetryRepository.GetSpan,
                _plotlyChart.DialogService,
                _plotlyChart.InvokeAsync,
                _plotlyChart.DialogsLoc,
                _plotlyChart.CancellationToken).ConfigureAwait(false);

            if (available)
            {
                await _plotlyChart.InvokeAsync(() =>
                {
                    _plotlyChart.NavigationManager.NavigateTo(DashboardUrls.TraceDetailUrl(traceId, spanId));
                });
            }
        }
    }

    private readonly record struct ExemplarGroupKey(DateTimeOffset? Start, DateTimeOffset? End);
}