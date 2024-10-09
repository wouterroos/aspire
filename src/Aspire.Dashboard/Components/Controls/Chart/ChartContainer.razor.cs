// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbine.Dashboard.Components.Pages;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Model.MetricValues;
using Turbine.Dashboard.Otlp.Storage;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Turbine.Dashboard.Components;

public partial class ChartContainer : ComponentBase, IAsyncDisposable
{
    private OtlpInstrumentData? _instrument;
    private PeriodicTimer? _tickTimer;
    private Task? _tickTask;
    private IDisposable? _themeChangedSubscription;
    private int _renderedDimensionsCount;
    private readonly InstrumentViewModel _instrumentViewModel = new InstrumentViewModel();

    [Parameter, EditorRequired]
    public required ApplicationKey ApplicationKey { get; set; }

    [Parameter, EditorRequired]
    public required string MeterName { get; set; }

    [Parameter, EditorRequired]
    public required string InstrumentName { get; set; }

    [Parameter, EditorRequired]
    public required TimeSpan Duration { get; set; }

    [Inject]
    public required TelemetryRepository TelemetryRepository { get; init; }

    [Inject]
    public required ILogger<ChartContainer> Logger { get; init; }

    [Inject]
    public required ThemeManager ThemeManager { get; init; }

    public List<DimensionFilterViewModel> DimensionFilters { get; } = [];
    public string? PreviousMeterName { get; set; }
    public string? PreviousInstrumentName { get; set; }

    protected override void OnInitialized()
    {
        // Update the graph every 200ms. This displays the latest data and moves time forward.
        _tickTimer = new PeriodicTimer(TimeSpan.FromSeconds(0.2));
        _tickTask = Task.Run(UpdateDataAsync);
        _themeChangedSubscription = ThemeManager.OnThemeChanged(async () =>
        {
            _instrumentViewModel.Theme = ThemeManager.Theme;
            await InvokeAsync(StateHasChanged);
        });
    }

    public async ValueTask DisposeAsync()
    {
        _themeChangedSubscription?.Dispose();
        _tickTimer?.Dispose();

        // Wait for UpdateData to complete.
        if (_tickTask is { } t)
        {
            await t;
        }
    }

    private async Task UpdateDataAsync()
    {
        PeriodicTimer? timer = _tickTimer;
        while (await timer!.WaitForNextTickAsync())
        {
            _instrument = GetInstrument();
            if (_instrument == null)
            {
                continue;
            }

            if (_instrument.Dimensions.Count > _renderedDimensionsCount)
            {
                // Re-render the entire control if the number of dimensions has changed.
                _renderedDimensionsCount = _instrument.Dimensions.Count;
                await InvokeAsync(StateHasChanged);
            }
            else
            {
                await UpdateInstrumentDataAsync(_instrument);
            }
        }
    }

    public async Task DimensionValuesChangedAsync(DimensionFilterViewModel dimensionViewModel)
    {
        if (_instrument == null)
        {
            return;
        }

        await UpdateInstrumentDataAsync(_instrument);
    }

    private async Task UpdateInstrumentDataAsync(OtlpInstrumentData instrument)
    {
        List<DimensionScope>? matchedDimensions = instrument.Dimensions.Where(MatchDimension).ToList();

        // Only update data in plotly
        await _instrumentViewModel.UpdateDataAsync(instrument.Summary, matchedDimensions);
    }

    private bool MatchDimension(DimensionScope dimension)
    {
        foreach (DimensionFilterViewModel? dimensionFilter in DimensionFilters)
        {
            if (!MatchFilter(dimension.Attributes, dimensionFilter))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MatchFilter(KeyValuePair<string, string>[] attributes, DimensionFilterViewModel filter)
    {
        // No filter selected.
        if (!filter.SelectedValues.Any())
        {
            return false;
        }

        string? value = OtlpHelpers.GetValue(attributes, filter.Name);
        foreach (DimensionValueViewModel? item in filter.SelectedValues)
        {
            if (item.Empty && string.IsNullOrEmpty(value))
            {
                return true;
            }
            if (item.Name == value)
            {
                return true;
            }
        }

        return false;
    }

    protected override async Task OnParametersSetAsync()
    {
        _instrument = GetInstrument();

        if (_instrument == null)
        {
            return;
        }

        bool hasInstrumentChanged = PreviousMeterName != MeterName || PreviousInstrumentName != InstrumentName;
        PreviousMeterName = MeterName;
        PreviousInstrumentName = InstrumentName;

        List<DimensionFilterViewModel>? filters = CreateUpdatedFilters(hasInstrumentChanged);

        DimensionFilters.Clear();
        DimensionFilters.AddRange(filters);

        await UpdateInstrumentDataAsync(_instrument);
    }

    private OtlpInstrumentData? GetInstrument()
    {
        DateTime endDate = DateTime.UtcNow;
        // Get more data than is being displayed. Histogram graph uses some historical data to calculate bucket counts.
        // It's ok to get more data than is needed here. An additional date filter is applied when building chart values.
        DateTime startDate = endDate.Subtract(Duration + TimeSpan.FromSeconds(30));

        OtlpInstrumentData? instrument = TelemetryRepository.GetInstrument(new GetInstrumentRequest
        {
            ApplicationKey = ApplicationKey,
            MeterName = MeterName,
            InstrumentName = InstrumentName,
            StartTime = startDate,
            EndTime = endDate,
        });

        if (instrument == null)
        {
            Logger.LogDebug(
                "Unable to find instrument. ApplicationKey: {ApplicationKey}, MeterName: {MeterName}, InstrumentName: {InstrumentName}",
                ApplicationKey,
                MeterName,
                InstrumentName);
        }

        return instrument;
    }

    private List<DimensionFilterViewModel> CreateUpdatedFilters(bool hasInstrumentChanged)
    {
        List<DimensionFilterViewModel>? filters = new List<DimensionFilterViewModel>();
        if (_instrument != null)
        {
            foreach (KeyValuePair<string, List<string>> item in _instrument.KnownAttributeValues.OrderBy(kvp => kvp.Key))
            {
                DimensionFilterViewModel? dimensionModel = new DimensionFilterViewModel
                {
                    Name = item.Key
                };

                dimensionModel.Values.AddRange(item.Value.OrderBy(v => v).Select(v =>
                {
                    bool empty = string.IsNullOrEmpty(v);
                    return new DimensionValueViewModel
                    {
                        Name = empty ? "(Empty)" : v,
                        Empty = empty
                    };
                }));

                filters.Add(dimensionModel);
            }

            foreach (DimensionFilterViewModel? item in filters)
            {
                item.SelectedValues.Clear();

                if (hasInstrumentChanged)
                {
                    // Select all by default.
                    foreach (DimensionValueViewModel? v in item.Values)
                    {
                        item.SelectedValues.Add(v);
                    }
                }
                else
                {
                    DimensionFilterViewModel? existing = DimensionFilters.SingleOrDefault(m => m.Name == item.Name);
                    if (existing != null)
                    {
                        // Select previously selected.
                        // Automatically select new incoming values if existing values are all selected.
                        IEnumerable<DimensionValueViewModel>? newSelectedValues = (existing.AreAllValuesSelected ?? false)
                            ? item.Values
                            : item.Values.Where(newValue => existing.SelectedValues.Any(existingValue => existingValue.Name == newValue.Name));

                        foreach (DimensionValueViewModel? v in newSelectedValues)
                        {
                            item.SelectedValues.Add(v);
                        }
                    }
                    else
                    {
                        // New filter. Select all by default.
                        foreach (DimensionValueViewModel? v in item.Values)
                        {
                            item.SelectedValues.Add(v);
                        }
                    }
                }
            }
        }

        return filters;
    }

    private Task OnTabChangeAsync(FluentTab newTab)
    {
        string? id = newTab.Id?.Substring("tab-".Length);

        if (id is null
            || !Enum.TryParse(typeof(Metrics.MetricViewKind), id, out object? o)
            || o is not Metrics.MetricViewKind viewKind)
        {
            return Task.CompletedTask;
        }

        return OnViewChangedAsync(viewKind);
    }
}