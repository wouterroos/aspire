// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbine.Dashboard.Components.Controls.Chart;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Model.Otlp;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Storage;
using Turbine.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Turbine.Dashboard.Components.Dialogs;

public partial class ExemplarsDialog : IDisposable
{
    [CascadingParameter]
    public FluentDialog Dialog { get; set; } = default!;

    [Parameter]
    public ExemplarsDialogViewModel Content { get; set; } = default!;

    [Inject]
    public required BrowserTimeProvider TimeProvider { get; init; }

    [Inject]
    public required IDialogService DialogService { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required TelemetryRepository TelemetryRepository { get; init; }

    public IQueryable<ChartExemplar> MetricView => Content.Exemplars.AsQueryable();

    private readonly CancellationTokenSource _cts = new();

    public async Task OnViewDetailsAsync(ChartExemplar exemplar)
    {
        bool available = await MetricsHelpers.WaitForSpanToBeAvailableAsync(
            traceId: exemplar.TraceId,
            spanId: exemplar.SpanId,
            getSpan: TelemetryRepository.GetSpan,
            DialogService,
            InvokeAsync,
            Loc,
            _cts.Token).ConfigureAwait(false);

        if (available)
        {
            NavigationManager.NavigateTo(DashboardUrls.TraceDetailUrl(exemplar.TraceId, spanId: exemplar.SpanId));
        }
    }

    private string GetTitle(ChartExemplar exemplar)
    {
        return (exemplar.Span != null)
            ? SpanWaterfallViewModel.GetTitle(exemplar.Span, Content.Applications)
            : $"{Loc[nameof(Resources.Dialogs.ExemplarsDialogTrace)]}: {OtlpHelpers.ToShortenedId(exemplar.TraceId)}";
    }

    private string FormatMetricValue(double? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        string? formattedValue = value.Value.ToString("F3", CultureInfo.CurrentCulture);
        if (!string.IsNullOrEmpty(Content.Instrument.Unit))
        {
            formattedValue += Content.Instrument.Unit.TrimStart('{').TrimEnd('}');
        }

        return formattedValue;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}