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
using Turbine.Dashboard.Extensions;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Model.MetricValues;
using Turbine.Dashboard.Otlp.Storage;
using Turbine.Dashboard.Resources;
using Turbine.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Turbine.Dashboard.Components.Controls.Chart;

public abstract class ChartBase : ComponentBase, IAsyncDisposable
{
    private const int GraphPointCount = 30;

    private readonly CancellationTokenSource _cts = new();
    protected CancellationToken CancellationToken { get; private set; }

    private TimeSpan _tickDuration;
    private DateTimeOffset _lastUpdateTime;
    private DateTimeOffset _currentDataStartTime;
    private List<KeyValuePair<string, string>[]>? _renderedDimensionAttributes;
    private OtlpInstrumentKey? _renderedInstrument;
    private string? _renderedTheme;
    private bool _renderedShowCount;

    [Inject]
    public required IStringLocalizer<ControlsStrings> Loc { get; init; }

    [Inject]
    public required IStringLocalizer<Resources.Dialogs> DialogsLoc { get; init; }

    [Inject]
    public required IInstrumentUnitResolver InstrumentUnitResolver { get; init; }

    [Inject]
    public required BrowserTimeProvider TimeProvider { get; init; }

    [Inject]
    public required TelemetryRepository TelemetryRepository { get; init; }

    [Parameter, EditorRequired]
    public required InstrumentViewModel InstrumentViewModel { get; set; }

    [Parameter, EditorRequired]
    public required TimeSpan Duration { get; set; }

    [Parameter]
    public required List<OtlpApplication> Applications { get; set; }

    // Stores a cache of the last set of spans returned as exemplars.
    // This dictionary is replaced each time the chart is updated.
    private Dictionary<SpanKey, OtlpSpan> _currentCache = new Dictionary<SpanKey, OtlpSpan>();

    private Dictionary<SpanKey, OtlpSpan> _newCache = new Dictionary<SpanKey, OtlpSpan>();

    private readonly record struct SpanKey(string TraceId, string SpanId);

    protected override void OnInitialized()
    {
        // Copy the token so there is no chance it is accessed on CTS after it is disposed.
        CancellationToken = _cts.Token;
        _currentDataStartTime = GetCurrentDataTime();
        InstrumentViewModel.DataUpdateSubscriptions.Add(OnInstrumentDataUpdate);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (CancellationToken.IsCancellationRequested ||
            InstrumentViewModel.Instrument is null ||
            InstrumentViewModel.MatchedDimensions is null ||
            !ReadyForData())
        {
            return;
        }

        DateTimeOffset inProgressDataTime = GetCurrentDataTime();

        while (_currentDataStartTime.Add(_tickDuration) < inProgressDataTime)
        {
            _currentDataStartTime = _currentDataStartTime.Add(_tickDuration);
        }

        List<KeyValuePair<string, string>[]>? dimensionAttributes = InstrumentViewModel.MatchedDimensions.Select(d => d.Attributes).ToList();
        if (_renderedInstrument is null || _renderedInstrument != InstrumentViewModel.Instrument.GetKey() ||
            _renderedDimensionAttributes is null || !_renderedDimensionAttributes.SequenceEqual(dimensionAttributes) ||
            _renderedTheme != InstrumentViewModel.Theme ||
            _renderedShowCount != InstrumentViewModel.ShowCount)
        {
            // Dimensions (or entire chart) has changed. Re-render the entire chart.
            _renderedInstrument = InstrumentViewModel.Instrument.GetKey();
            _renderedDimensionAttributes = dimensionAttributes;
            _renderedTheme = InstrumentViewModel.Theme;
            _renderedShowCount = InstrumentViewModel.ShowCount;
            await UpdateChartAsync(tickUpdate: false, inProgressDataTime).ConfigureAwait(false);
        }
        else if (_lastUpdateTime.Add(TimeSpan.FromSeconds(0.2)) < TimeProvider.GetUtcNow())
        {
            // Throttle how often the chart is updated.
            _lastUpdateTime = TimeProvider.GetUtcNow();
            await UpdateChartAsync(tickUpdate: true, inProgressDataTime).ConfigureAwait(false);
        }
    }

    protected override void OnParametersSet()
    {
        _tickDuration = Duration / GraphPointCount;
    }

    private Task OnInstrumentDataUpdate()
    {
        return InvokeAsync(StateHasChanged);
    }

    private (List<ChartTrace> Y, List<DateTimeOffset> X, List<ChartExemplar> Exemplars) CalculateHistogramValues(List<DimensionScope> dimensions, int pointCount, bool tickUpdate, DateTimeOffset inProgressDataTime, string yLabel)
    {
        TimeSpan pointDuration = Duration / pointCount;
        Dictionary<int, ChartTrace>? traces = new Dictionary<int, ChartTrace>
        {
            [50] = new() { Name = $"P50 {yLabel}", Percentile = 50 },
            [90] = new() { Name = $"P90 {yLabel}", Percentile = 90 },
            [99] = new() { Name = $"P99 {yLabel}", Percentile = 99 }
        };
        List<DateTimeOffset>? xValues = new List<DateTimeOffset>();
        List<ChartExemplar>? exemplars = new List<ChartExemplar>();
        DateTimeOffset startDate = _currentDataStartTime;
        DateTimeOffset? firstPointEndTime = null;
        DateTimeOffset? lastPointStartTime = null;

        // Generate the points in reverse order so that the chart is drawn from right to left.
        // Add a couple of extra points to the end so that the chart is drawn all the way to the right edge.
        for (int pointIndex = 0; pointIndex < (pointCount + 2); pointIndex++)
        {
            DateTimeOffset start = CalcOffset(pointIndex, startDate, pointDuration);
            DateTimeOffset end = CalcOffset(pointIndex - 1, startDate, pointDuration);
            firstPointEndTime ??= end;
            lastPointStartTime = start;

            xValues.Add(TimeProvider.ToLocalDateTimeOffset(end));

            if (!TryCalculateHistogramPoints(dimensions, start, end, traces, exemplars))
            {
                foreach (KeyValuePair<int, ChartTrace> trace in traces)
                {
                    trace.Value.Values.Add(null);
                }
            }
        }

        foreach (KeyValuePair<int, ChartTrace> item in traces)
        {
            item.Value.Values.Reverse();
        }
        xValues.Reverse();

        if (tickUpdate && TryCalculateHistogramPoints(dimensions, firstPointEndTime!.Value, inProgressDataTime, traces, exemplars))
        {
            xValues.Add(TimeProvider.ToLocalDateTimeOffset(inProgressDataTime));
        }

        ChartTrace? previousValues = null;
        foreach (KeyValuePair<int, ChartTrace> trace in traces.OrderBy(kvp => kvp.Key))
        {
            ChartTrace? currentTrace = trace.Value;

            for (int i = 0; i < currentTrace.Values.Count; i++)
            {
                double? diffValue = (previousValues != null)
                    ? currentTrace.Values[i] - previousValues.Values[i] ?? 0
                    : currentTrace.Values[i];

                if (diffValue > 0)
                {
                    currentTrace.Tooltips.Add(FormatTooltip(currentTrace.Name, currentTrace.Values[i].GetValueOrDefault(), xValues[i]));
                }
                else
                {
                    currentTrace.Tooltips.Add(null);
                }

                currentTrace.DiffValues.Add(diffValue);
            }

            previousValues = currentTrace;
        }

        exemplars = exemplars.Where(p => p.Start <= startDate && p.Start >= lastPointStartTime!.Value).OrderBy(p => p.Start).ToList();

        return (traces.Select(kvp => kvp.Value).ToList(), xValues, exemplars);
    }

    private string FormatTooltip(string name, double yValue, DateTimeOffset xValue)
    {
        return $"<b>{HttpUtility.HtmlEncode(InstrumentViewModel.Instrument?.Name)}</b><br />{HttpUtility.HtmlEncode(name)}: {FormatHelpers.FormatNumberWithOptionalDecimalPlaces(yValue, maxDecimalPlaces: 6, CultureInfo.CurrentCulture)}<br />Time: {FormatHelpers.FormatTime(TimeProvider, TimeProvider.ToLocal(xValue))}";
    }

    private static HistogramValue GetHistogramValue(MetricValueBase metric)
    {
        if (metric is HistogramValue histogramValue)
        {
            return histogramValue;
        }

        throw new InvalidOperationException("Unexpected metric type: " + metric.GetType());
    }

    internal bool TryCalculateHistogramPoints(List<DimensionScope> dimensions, DateTimeOffset start, DateTimeOffset end, Dictionary<int, ChartTrace> traces, List<ChartExemplar> exemplars)
    {
        bool hasValue = false;

        ulong[]? currentBucketCounts = null;
        double[]? explicitBounds = null;

        start = start.Subtract(TimeSpan.FromSeconds(1));
        end = end.Add(TimeSpan.FromSeconds(1));

        foreach (DimensionScope? dimension in dimensions)
        {
            IList<MetricValueBase>? dimensionValues = dimension.Values;
            for (int i = dimensionValues.Count - 1; i >= 0; i--)
            {
                MetricValueBase? metric = dimensionValues[i];
                if (metric.Start >= start && metric.Start <= end)
                {
                    HistogramValue? histogramValue = GetHistogramValue(metric);

                    AddExemplars(exemplars, metric);

                    // Only use the first recorded entry if it is the beginning of data.
                    // We can verify the first entry is the beginning of data by checking if the number of buckets equals the total count.
                    if (i == 0 && CountBuckets(histogramValue) != histogramValue.Count)
                    {
                        continue;
                    }

                    explicitBounds ??= histogramValue.ExplicitBounds;

                    ulong[]? previousHistogramValues = i > 0 ? GetHistogramValue(dimensionValues[i - 1]).Values : null;

                    if (currentBucketCounts is null)
                    {
                        currentBucketCounts = new ulong[histogramValue.Values.Length];
                    }
                    else if (currentBucketCounts.Length != histogramValue.Values.Length)
                    {
                        throw new InvalidOperationException("Histogram values changed size");
                    }

                    for (int valuesIndex = 0; valuesIndex < histogramValue.Values.Length; valuesIndex++)
                    {
                        ulong newValue = histogramValue.Values[valuesIndex];

                        if (previousHistogramValues != null)
                        {
                            // Histogram values are cumulative, so subtract the previous value to get the diff.
                            newValue -= previousHistogramValues[valuesIndex];
                        }

                        currentBucketCounts[valuesIndex] += newValue;
                    }

                    hasValue = true;
                }
            }
        }
        if (hasValue)
        {
            foreach (KeyValuePair<int, ChartTrace> percentileValues in traces)
            {
                double? percentileValue = CalculatePercentile(percentileValues.Key, currentBucketCounts!, explicitBounds!);
                percentileValues.Value.Values.Add(percentileValue);
            }
        }
        return hasValue;
    }

    private void AddExemplars(List<ChartExemplar> exemplars, MetricValueBase metric)
    {
        if (metric.HasExemplars)
        {
            foreach (MetricsExemplar? exemplar in metric.Exemplars)
            {
                // TODO: Exemplars are duplicated on metrics in some scenarios.
                // This is a quick fix to ensure a distinct collection of metrics are displayed in the UI.
                // Investigation is needed into why there are duplicates.
                bool exists = false;
                foreach (ChartExemplar? existingExemplar in exemplars)
                {
                    if (exemplar.Start == existingExemplar.Start &&
                        exemplar.Value == existingExemplar.Value &&
                        exemplar.SpanId == existingExemplar.SpanId &&
                        exemplar.TraceId == existingExemplar.TraceId)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    continue;
                }

                // Try to find span the the local cache first.
                // This is done to avoid scanning a potentially large trace collection in repository.
                SpanKey key = new SpanKey(exemplar.TraceId, exemplar.SpanId);
                if (!_currentCache.TryGetValue(key, out OtlpSpan? span))
                {
                    span = TelemetryRepository.GetSpan(exemplar.TraceId, exemplar.SpanId);
                }
                if (span != null)
                {
                    _newCache[key] = span;
                }

                DateTimeOffset exemplarStart = TimeProvider.ToLocalDateTimeOffset(exemplar.Start);
                exemplars.Add(new ChartExemplar
                {
                    Start = exemplarStart,
                    Value = exemplar.Value,
                    TraceId = exemplar.TraceId,
                    SpanId = exemplar.SpanId,
                    Span = span
                });
            }
        }
    }

    private static ulong CountBuckets(HistogramValue histogramValue)
    {
        ulong value = 0ul;
        for (int i = 0; i < histogramValue.Values.Length; i++)
        {
            value += histogramValue.Values[i];
        }
        return value;
    }

    internal static double? CalculatePercentile(int percentile, ulong[] counts, double[] explicitBounds)
    {
        if (percentile < 0 || percentile > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Percentile must be between 0 and 100.");
        }

        ulong totalCount = 0ul;
        foreach (ulong count in counts)
        {
            totalCount += count;
        }

        double targetCount = (percentile / 100.0) * totalCount;
        ulong accumulatedCount = 0ul;

        for (int i = 0; i < explicitBounds.Length; i++)
        {
            accumulatedCount += counts[i];

            if (accumulatedCount >= targetCount)
            {
                return explicitBounds[i];
            }
        }

        // If the percentile is larger than any bucket value, return the last value
        return explicitBounds[explicitBounds.Length - 1];
    }

    private (List<ChartTrace> Y, List<DateTimeOffset> X, List<ChartExemplar> Exemplars) CalculateChartValues(List<DimensionScope> dimensions, int pointCount, bool tickUpdate, DateTimeOffset inProgressDataTime, string yLabel)
    {
        TimeSpan pointDuration = Duration / pointCount;
        List<double?>? yValues = new List<double?>();
        List<DateTimeOffset>? xValues = new List<DateTimeOffset>();
        List<ChartExemplar>? exemplars = new List<ChartExemplar>();
        DateTimeOffset startDate = _currentDataStartTime;
        DateTimeOffset? firstPointEndTime = null;

        // Generate the points in reverse order so that the chart is drawn from right to left.
        // Add a couple of extra points to the end so that the chart is drawn all the way to the right edge.
        for (int pointIndex = 0; pointIndex < (pointCount + 2); pointIndex++)
        {
            DateTimeOffset start = CalcOffset(pointIndex, startDate, pointDuration);
            DateTimeOffset end = CalcOffset(pointIndex - 1, startDate, pointDuration);
            firstPointEndTime ??= end;

            xValues.Add(TimeProvider.ToLocalDateTimeOffset(end));

            if (TryCalculatePoint(dimensions, start, end, exemplars, out double tickPointValue))
            {
                yValues.Add(tickPointValue);
            }
            else
            {
                yValues.Add(null);
            }
        }

        yValues.Reverse();
        xValues.Reverse();

        if (tickUpdate && TryCalculatePoint(dimensions, firstPointEndTime!.Value, inProgressDataTime, exemplars, out double inProgressPointValue))
        {
            yValues.Add(inProgressPointValue);
            xValues.Add(TimeProvider.ToLocalDateTimeOffset(inProgressDataTime));
        }

        ChartTrace? trace = new ChartTrace
        {
            Name = HttpUtility.HtmlEncode(yLabel)
        };

        for (int i = 0; i < xValues.Count; i++)
        {
            trace.Values.AddRange(yValues);
            trace.DiffValues.AddRange(yValues);
            if (yValues[i] is not null)
            {
                trace.Tooltips.Add(FormatTooltip(yLabel, yValues[i]!.Value, xValues[i]));
            }
            else
            {
                trace.Tooltips.Add(null);
            }
        }

        return ([trace], xValues, exemplars);
    }

    private bool TryCalculatePoint(List<DimensionScope> dimensions, DateTimeOffset start, DateTimeOffset end, List<ChartExemplar> exemplars, out double pointValue)
    {
        bool hasValue = false;
        pointValue = 0d;

        foreach (DimensionScope? dimension in dimensions)
        {
            IList<MetricValueBase>? dimensionValues = dimension.Values;
            double dimensionValue = 0d;
            for (int i = dimensionValues.Count - 1; i >= 0; i--)
            {
                MetricValueBase? metric = dimensionValues[i];
                if ((metric.Start <= end && metric.End >= start) || (metric.Start >= start && metric.End <= end))
                {
                    double value = metric switch
                    {
                        MetricValue<long> longMetric => longMetric.Value,
                        MetricValue<double> doubleMetric => doubleMetric.Value,
                        HistogramValue histogramValue => histogramValue.Count,
                        _ => 0// throw new InvalidOperationException("Unexpected metric type: " + metric.GetType())
                    };

                    dimensionValue = Math.Max(value, dimensionValue);
                    hasValue = true;
                }

                AddExemplars(exemplars, metric);
            }

            pointValue += dimensionValue;
        }

        // JS interop doesn't support serializing NaN values.
        if (double.IsNaN(pointValue))
        {
            pointValue = default;
            return false;
        }

        return hasValue;
    }

    private static DateTimeOffset CalcOffset(int pointIndex, DateTimeOffset now, TimeSpan pointDuration)
    {
        return now.Subtract(pointDuration * pointIndex);
    }

    private async Task UpdateChartAsync(bool tickUpdate, DateTimeOffset inProgressDataTime)
    {
        // Unit comes from the instrument and they're not localized.
        // The hardcoded "Count" label isn't localized for consistency.
        const string CountUnit = "Count";

        Debug.Assert(InstrumentViewModel.MatchedDimensions != null);
        Debug.Assert(InstrumentViewModel.Instrument != null);

        string? unit = !InstrumentViewModel.ShowCount
            ? GetDisplayedUnit(InstrumentViewModel.Instrument)
            : CountUnit;

        List<ChartTrace> traces;
        List<DateTimeOffset> xValues;
        List<ChartExemplar> exemplars;
        if (InstrumentViewModel.Instrument?.Type != OtlpInstrumentType.Histogram || InstrumentViewModel.ShowCount)
        {
            (traces, xValues, exemplars) = CalculateChartValues(InstrumentViewModel.MatchedDimensions, GraphPointCount, tickUpdate, inProgressDataTime, unit);

            // TODO: Exemplars on non-histogram charts doesn't work well. Don't display for now.
            exemplars.Clear();
        }
        else
        {
            (traces, xValues, exemplars) = CalculateHistogramValues(InstrumentViewModel.MatchedDimensions, GraphPointCount, tickUpdate, inProgressDataTime, unit);
        }

        // Replace cache for next update.
        _currentCache = _newCache;
        _newCache = new Dictionary<SpanKey, OtlpSpan>();

        await OnChartUpdatedAsync(traces, xValues, exemplars, tickUpdate, inProgressDataTime, CancellationToken);
    }

    private DateTimeOffset GetCurrentDataTime()
    {
        return TimeProvider.GetUtcNow().Subtract(TimeSpan.FromSeconds(1)); // Compensate for delay in receiving metrics from services.
    }

    private string GetDisplayedUnit(OtlpInstrumentSummary instrument)
    {
        return InstrumentUnitResolver.ResolveDisplayedUnit(instrument, titleCase: true, pluralize: true);
    }

    protected abstract Task OnChartUpdatedAsync(List<ChartTrace> traces, List<DateTimeOffset> xValues, List<ChartExemplar> exemplars, bool tickUpdate, DateTimeOffset inProgressDataTime, CancellationToken cancellationToken);

    protected abstract bool ReadyForData();

    public ValueTask DisposeAsync() => DisposeAsync(disposing: true);

    protected virtual ValueTask DisposeAsync(bool disposing)
    {
        _cts.Cancel();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }
}