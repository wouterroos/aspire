// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbine.Dashboard.Components.Controls.Chart;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Components.Dialogs;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Resources;
using Turbine.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Turbine.Dashboard.Components.Controls;

public partial class MetricTable : ChartBase
{
    private SortedList<DateTimeOffset, MetricViewBase> _metrics = [];
    private List<ChartExemplar> _exemplars = [];
    private string _unitColumnHeader = string.Empty;
    private IJSObjectReference? _jsModule;

    private OtlpInstrumentSummary? _instrument;
    private bool _showCount;
    private DateTimeOffset? _lastUpdate;

    private IQueryable<MetricViewBase> _metricsView => _metrics.Values.AsEnumerable().Reverse().ToList().AsQueryable();

    [Inject]
    public required IJSRuntime JS { get; init; }

    [Inject]
    public required IDialogService DialogService { get; init; }

    public bool OnlyShowValueChangesInTable { get; set; } = true;

    protected override async Task OnChartUpdatedAsync(List<ChartTrace> traces, List<DateTimeOffset> xValues, List<ChartExemplar> exemplars, bool tickUpdate, DateTimeOffset inProgressDataTime, CancellationToken cancellationToken)
    {
        Debug.Assert(_jsModule != null, "The module should be initialized before chart data is sent to control.");

        // Only update the data grid once per second to avoid additional DOM re-renders.
        if (inProgressDataTime - _lastUpdate < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastUpdate = inProgressDataTime;

        if (!Equals(_instrument?.Name, InstrumentViewModel.Instrument?.Name) || _showCount != InstrumentViewModel.ShowCount)
        {
            _metrics.Clear();
        }

        // Store local values from view model on data update.
        // This keeps the instrument and data consistent while the view model is updated.
        _instrument = InstrumentViewModel.Instrument;
        _showCount = InstrumentViewModel.ShowCount;

        _metrics = UpdateMetrics(out ISet<DateTimeOffset>? xValuesToAnnounce, traces, xValues, exemplars);
        _exemplars = exemplars;

        if (xValuesToAnnounce.Count == 0)
        {
            return;
        }

        await Task.Delay(500, cancellationToken);

        List<MetricViewBase>? metricView = _metricsView.ToList();
        List<int> indices = [];

        for (int i = 0; i < metricView.Count; i++)
        {
            if (xValuesToAnnounce.Contains(metricView[i].DateTime))
            {
                indices.Add(i);
            }
        }

        try
        {
            await _jsModule.InvokeVoidAsync("announceDataGridRows", "metric-table-container", indices);
        }
        catch (ObjectDisposedException)
        {
            // This call happens after a delay. To ensure there is no chance of a race between disposing
            // and using the module, catch and ignore disposed exceptions.
        }
    }

    private async Task OpenExemplarsDialogAsync(MetricViewBase metric)
    {
        ExemplarsDialogViewModel? vm = new ExemplarsDialogViewModel
        {
            Exemplars = metric.Exemplars,
            Applications = Applications,
            Instrument = InstrumentViewModel.Instrument!
        };
        DialogParameters? parameters = new DialogParameters
        {
            Title = DialogsLoc[nameof(Resources.Dialogs.ExemplarsDialogTitle)],
            PrimaryAction = DialogsLoc[nameof(Resources.Dialogs.ExemplarsDialogCloseButtonText)],
            SecondaryAction = string.Empty,
            Width = "800px",
            Height = "auto"
        };
        await DialogService.ShowDialogAsync<ExemplarsDialog>(vm, parameters);
    }

    private SortedList<DateTimeOffset, MetricViewBase> UpdateMetrics(out ISet<DateTimeOffset> addedXValues, List<ChartTrace> traces, List<DateTimeOffset> xValues, List<ChartExemplar> exemplars)
    {
        SortedList<DateTimeOffset, MetricViewBase>? newMetrics = new SortedList<DateTimeOffset, MetricViewBase>();

        _unitColumnHeader = traces.First().Name;

        for (int i = 0; i < xValues.Count; i++)
        {
            DateTimeOffset xValue = xValues[i];
            MetricViewBase? previousMetric = newMetrics.LastOrDefault(dt => dt.Key < xValue).Value;

            if (IsHistogramInstrument() && !_showCount)
            {
                int iTmp = i;
                Dictionary<int, double?>? traceValuesByPercentile = traces.ToDictionary(trace => trace.Percentile!.Value, trace => trace.Values[iTmp]);
                List<double?>? valueDiffs = traceValuesByPercentile.Select(kvp =>
                {
                    (int percentile, double? traceValue) = kvp;
                    if (traceValue is not null
                        && previousMetric is HistogramMetricView histogramMetricView
                        && histogramMetricView.Percentiles[percentile].Value is { } previousPercentileValue)
                    {
                        return traceValue.Value - previousPercentileValue;
                    }

                    return traceValue;
                }).ToList();

                if (traceValuesByPercentile.Values.All(value => value is null))
                {
                    continue;
                }

                if (OnlyShowValueChangesInTable && valueDiffs.All(diff => DoubleEquals(diff, 0)))
                {
                    continue;
                }

                newMetrics.Add(xValue, CreateHistogramMetricView());

                MetricViewBase CreateHistogramMetricView()
                {
                    SortedDictionary<int, (string Name, double? Value, ValueDirectionChange Direction)>? percentiles = new SortedDictionary<int, (string Name, double? Value, ValueDirectionChange Direction)>();
                    for (int traceIndex = 0; traceIndex < traces.Count; traceIndex++)
                    {
                        ChartTrace? trace = traces[traceIndex];
                        percentiles.Add(trace.Percentile!.Value, (trace.Name, trace.Values[i], GetDirectionChange(valueDiffs[traceIndex])));
                    }

                    return new HistogramMetricView
                    {
                        DateTime = xValue,
                        Percentiles = percentiles,
                        Exemplars = []
                    };
                }
            }
            else
            {
                ChartTrace? trace = traces.Single();
                double? yValue = trace.Values[i];
                double? valueDiff = yValue is not null && (previousMetric as MetricValueView)?.Value is { } previousValue ? yValue - previousValue : yValue;

                if (yValue is null)
                {
                    continue;
                }

                if (OnlyShowValueChangesInTable && DoubleEquals(valueDiff, 0d))
                {
                    continue;
                }

                newMetrics.Add(xValue, CreateMetricView());

                MetricViewBase CreateMetricView()
                {
                    return new MetricValueView
                    {
                        DateTime = xValue,
                        Value = yValue,
                        ValueChange = GetDirectionChange(valueDiff),
                        Exemplars = []
                    };
                }
            }
        }

        // Associate exemplars with rows. Need to happen after rows are calculated because they could be skipped (e.g. unchanged data)
        for (int i = newMetrics.Count - 1; i >= 0; i--)
        {
            MetricViewBase? current = newMetrics.GetValueAtIndex(i);
            DateTimeOffset? endTime = (i != newMetrics.Count - 1) ? current.DateTime : (DateTimeOffset?)null;
            DateTimeOffset? startTime = (i > 0) ? newMetrics.GetKeyAtIndex(i - 1) : (DateTimeOffset?)null;

            List<ChartExemplar>? currentExemplars = exemplars.Where(e => (e.Start >= startTime || startTime == null) && (e.Start < endTime || endTime == null)).ToList();
            current.Exemplars.AddRange(currentExemplars);
        }

        Debug.Assert(exemplars.Count == newMetrics.Sum(m => m.Value.Exemplars.Count), $"Expected {exemplars.Count} exemplars but got {newMetrics.Sum(m => m.Value.Exemplars.Count)} exemplars.");

        DateTimeOffset? latestCurrentMetric = _metrics.Keys.OfType<DateTimeOffset?>().LastOrDefault();
        addedXValues = newMetrics.Keys.Where(newKey => newKey > latestCurrentMetric || latestCurrentMetric == null).ToHashSet();
        return newMetrics;
    }

    private static bool DoubleEquals(double? a, double? b)
    {
        if (a is not null && b is not null)
        {
            return Math.Abs(a.Value - b.Value) < 0.00002; // arbitrarily small number
        }

        if ((a is null && b is not null) || (a is not null && b is null))
        {
            return false;
        }

        return true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "/Components/Controls/Chart/MetricTable.razor.js");
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    // The first data is used to initialize the chart. The module needs to be ready first.
    protected override bool ReadyForData() => _jsModule != null;

    private bool IsHistogramInstrument()
    {
        return _instrument?.Type == OtlpInstrumentType.Histogram;
    }

    private bool ShowPercentiles()
    {
        return IsHistogramInstrument() && !_showCount;
    }

    private Task SettingsChangedAsync() => InvokeAsync(StateHasChanged);

    private static ValueDirectionChange GetDirectionChange(double? comparisonResult)
    {
        if (comparisonResult > 0)
        {
            return ValueDirectionChange.Up;
        }

        return comparisonResult < 0 ? ValueDirectionChange.Down : ValueDirectionChange.Constant;
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        await base.DisposeAsync(disposing);

        if (disposing)
        {
            await JSInteropHelpers.SafeDisposeAsync(_jsModule);
        }
    }

    public abstract record MetricViewBase
    {
        public required DateTimeOffset DateTime { get; set; }
        public required List<ChartExemplar> Exemplars { get; set; }
    }

    public record MetricValueView : MetricViewBase
    {
        public required double? Value { get; set; }
        public required ValueDirectionChange? ValueChange { get; init; }
    }

    public record HistogramMetricView : MetricViewBase
    {
        public required SortedDictionary<int, (string Name, double? Value, ValueDirectionChange Direction)> Percentiles { get; init; }
    }

    public enum ValueDirectionChange
    {
        Up,
        Down,
        Constant
    }

    private (Icon Icon, string Title)? GetIconAndTitleForDirection(ValueDirectionChange? directionChange)
    {
        return directionChange switch
        {
            ValueDirectionChange.Up => (new Icons.Filled.Size16.ArrowCircleUp().WithColor(Color.Success), Loc[nameof(ControlsStrings.MetricTableValueIncreased)]),
            ValueDirectionChange.Down => (new Icons.Filled.Size16.ArrowCircleDown().WithColor(Color.Warning), Loc[nameof(ControlsStrings.MetricTableValueDecreased)]),
            ValueDirectionChange.Constant => (new Icons.Filled.Size16.ArrowCircleRight().WithColor(Color.Info), Loc[nameof(ControlsStrings.MetricTableValueNoChange)]),
            _ => null
        };
    }

    private static string FormatMetricValue(double? value)
    {
        return value is null ? string.Empty : value.Value.ToString("F3", CultureInfo.CurrentCulture);
    }
}