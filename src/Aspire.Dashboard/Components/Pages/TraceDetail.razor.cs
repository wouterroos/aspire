// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Turbine.Dashboard.Components.Resize;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Model.Otlp;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Storage;
using Turbine.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Turbine.Dashboard.Components.Pages;

public partial class TraceDetail : ComponentBase
{
    private const string NameColumn = nameof(NameColumn);
    private const string TicksColumn = nameof(TicksColumn);
    private const string DetailsColumn = nameof(DetailsColumn);

    private readonly List<IDisposable> _peerChangesSubscriptions = new();
    private OtlpTrace? _trace;
    private Subscription? _tracesSubscription;
    private List<SpanWaterfallViewModel>? _spanWaterfallViewModels;
    private int _maxDepth;
    private List<OtlpApplication> _applications = default!;
    private readonly List<string> _collapsedSpanIds = [];
    private string? _elementIdBeforeDetailsViewOpened;
    private GridColumnManager _manager = null!;

    [Parameter]
    public required string TraceId { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public required string? SpanId { get; set; }

    [Inject]
    public required TelemetryRepository TelemetryRepository { get; init; }

    [Inject]
    public required IEnumerable<IOutgoingPeerResolver> OutgoingPeerResolvers { get; init; }

    [Inject]
    public required BrowserTimeProvider TimeProvider { get; init; }

    [Inject]
    public required IJSRuntime JS { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required DimensionManager DimensionManager { get; init; }

    [CascadingParameter]
    public required ViewportInformation ViewportInformation { get; set; }

    protected override void OnInitialized()
    {
        _manager = new GridColumnManager([
            new GridColumn(Name: NameColumn, DesktopWidth: "4fr", MobileWidth: "4fr"),
            new GridColumn(Name: TicksColumn, DesktopWidth: "12fr", MobileWidth: "12fr"),
            new GridColumn(Name: DetailsColumn, DesktopWidth: "85px", MobileWidth: null)
        ], DimensionManager);

        foreach (IOutgoingPeerResolver? resolver in OutgoingPeerResolvers)
        {
            _peerChangesSubscriptions.Add(resolver.OnPeerChanges(async () =>
            {
                UpdateDetailViewData();
                await InvokeAsync(StateHasChanged);
            }));
        }
    }

    private ValueTask<GridItemsProviderResult<SpanWaterfallViewModel>> GetData(GridItemsProviderRequest<SpanWaterfallViewModel> request)
    {
        Debug.Assert(_spanWaterfallViewModels != null);

        List<SpanWaterfallViewModel>? visibleSpanWaterfallViewModels = _spanWaterfallViewModels.Where(viewModel => !viewModel.IsHidden).ToList();

        IEnumerable<SpanWaterfallViewModel>? page = visibleSpanWaterfallViewModels.AsEnumerable();
        if (request.StartIndex > 0)
        {
            page = page.Skip(request.StartIndex);
        }
        if (request.Count != null)
        {
            page = page.Take(request.Count.Value);
        }

        return ValueTask.FromResult(new GridItemsProviderResult<SpanWaterfallViewModel>
        {
            Items = page.ToList(),
            TotalItemCount = visibleSpanWaterfallViewModels.Count
        });
    }

    private static Icon GetSpanIcon(OtlpSpan span)
    {
        switch (span.Kind)
        {
            case OtlpSpanKind.Server:
                return new Icons.Filled.Size16.Server();

            case OtlpSpanKind.Consumer:
                if (span.Attributes.HasKey("messaging.system"))
                {
                    return new Icons.Filled.Size16.Mailbox();
                }
                else
                {
                    return new Icons.Filled.Size16.ContentSettings();
                }
            default:
                throw new InvalidOperationException($"Unsupported span kind when resolving icon: {span.Kind}");
        }
    }

    private static List<SpanWaterfallViewModel> CreateSpanWaterfallViewModels(OtlpTrace trace, TraceDetailState state)
    {
        List<SpanWaterfallViewModel>? orderedSpans = new List<SpanWaterfallViewModel>();
        // There should be one root span but just in case, we'll add them all.
        foreach (OtlpSpan? rootSpan in trace.Spans.Where(s => string.IsNullOrEmpty(s.ParentSpanId)).OrderBy(s => s.StartTime))
        {
            AddSelfAndChildren(orderedSpans, rootSpan, depth: 1, hidden: false, state, CreateViewModel);
        }
        // Unparented spans.
        foreach (OtlpSpan? unparentedSpan in trace.Spans.Where(s => !string.IsNullOrEmpty(s.ParentSpanId) && s.GetParentSpan() == null).OrderBy(s => s.StartTime))
        {
            AddSelfAndChildren(orderedSpans, unparentedSpan, depth: 1, hidden: false, state, CreateViewModel);
        }

        return orderedSpans;

        static SpanWaterfallViewModel AddSelfAndChildren(List<SpanWaterfallViewModel> orderedSpans, OtlpSpan span, int depth, bool hidden, TraceDetailState state, Func<OtlpSpan, int, bool, TraceDetailState, SpanWaterfallViewModel> createViewModel)
        {
            SpanWaterfallViewModel? viewModel = createViewModel(span, depth, hidden, state);
            orderedSpans.Add(viewModel);
            depth++;

            foreach (OtlpSpan? child in span.GetChildSpans().OrderBy(s => s.StartTime))
            {
                SpanWaterfallViewModel? childViewModel = AddSelfAndChildren(orderedSpans, child, depth, viewModel.IsHidden || viewModel.IsCollapsed, state, createViewModel);
                viewModel.Children.Add(childViewModel);
            }

            return viewModel;
        }

        static SpanWaterfallViewModel CreateViewModel(OtlpSpan span, int depth, bool hidden, TraceDetailState state)
        {
            DateTime traceStart = span.Trace.FirstSpan.StartTime;
            TimeSpan relativeStart = span.StartTime - traceStart;
            double rootDuration = span.Trace.Duration.TotalMilliseconds;

            double leftOffset = relativeStart.TotalMilliseconds / rootDuration * 100;
            double width = span.Duration.TotalMilliseconds / rootDuration * 100;

            // Figure out if the label is displayed to the left or right of the span.
            // If the label position is based on whether more than half of the span is on the left or right side of the trace.
            bool labelIsRight = (relativeStart + span.Duration / 2) < (span.Trace.Duration / 2);

            // A span may indicate a call to another service but the service isn't instrumented.
            bool hasPeerService = OtlpHelpers.GetPeerAddress(span.Attributes) != null;
            bool isUninstrumentedPeer = hasPeerService && span.Kind is OtlpSpanKind.Client or OtlpSpanKind.Producer && !span.GetChildSpans().Any();
            string? uninstrumentedPeer = isUninstrumentedPeer ? ResolveUninstrumentedPeerName(span, state.OutgoingPeerResolvers) : null;

            SpanWaterfallViewModel? viewModel = new SpanWaterfallViewModel
            {
                Children = [],
                Span = span,
                LeftOffset = leftOffset,
                Width = width,
                Depth = depth,
                LabelIsRight = labelIsRight,
                UninstrumentedPeer = uninstrumentedPeer
            };

            // Restore hidden/collapsed state to new view model.
            if (state.CollapsedSpanIds.Contains(span.SpanId))
            {
                viewModel.IsCollapsed = true;
            }
            if (hidden)
            {
                viewModel.IsHidden = true;
            }

            return viewModel;
        }
    }

    private static string? ResolveUninstrumentedPeerName(OtlpSpan span, IEnumerable<IOutgoingPeerResolver> outgoingPeerResolvers)
    {
        // Attempt to resolve uninstrumented peer to a friendly name from the span.
        foreach (IOutgoingPeerResolver? resolver in outgoingPeerResolvers)
        {
            if (resolver.TryResolvePeerName(span.Attributes, out string? name))
            {
                return name;
            }
        }

        // Fallback to the peer address.
        return OtlpHelpers.GetPeerAddress(span.Attributes);
    }

    protected override async Task OnParametersSetAsync()
    {
        UpdateDetailViewData();

        if (SpanId is not null && _spanWaterfallViewModels is not null)
        {
            SpanWaterfallViewModel? spanVm = _spanWaterfallViewModels.SingleOrDefault(vm => vm.Span.SpanId == SpanId);
            if (spanVm != null)
            {
                await OnShowPropertiesAsync(spanVm, buttonId: null);
            }

            // Navigate to remove ?spanId=xxx in the URL.
            NavigationManager.NavigateTo(DashboardUrls.TraceDetailUrl(TraceId), new NavigationOptions { ReplaceHistoryEntry = true });
        }
    }

    private void UpdateDetailViewData()
    {
        _applications = TelemetryRepository.GetApplications();

        _trace = null;

        if (TraceId is not null)
        {
            _trace = TelemetryRepository.GetTrace(TraceId);
            if (_trace is { } trace)
            {
                _spanWaterfallViewModels = CreateSpanWaterfallViewModels(trace, new TraceDetailState(OutgoingPeerResolvers, _collapsedSpanIds));
                _maxDepth = _spanWaterfallViewModels.Max(s => s.Depth);

                if (_tracesSubscription is null || _tracesSubscription.ApplicationKey != trace.FirstSpan.Source.ApplicationKey)
                {
                    _tracesSubscription?.Dispose();
                    _tracesSubscription = TelemetryRepository.OnNewTraces(trace.FirstSpan.Source.ApplicationKey, SubscriptionType.Read, () => InvokeAsync(() =>
                    {
                        UpdateDetailViewData();
                        StateHasChanged();
                        return Task.CompletedTask;
                    }));
                }
            }
        }
    }

    private string GetRowClass(SpanWaterfallViewModel viewModel)
    {
        // Test with id rather than the object reference because the data and view model objects are recreated on trace updates.
        if (viewModel.Span.SpanId == SelectedSpan?.Span.SpanId)
        {
            return "selected-row";
        }

        return string.Empty;
    }

    public SpanDetailsViewModel? SelectedSpan { get; set; }

    private void OnToggleCollapse(SpanWaterfallViewModel viewModel)
    {
        // View model data is recreated if the trace updates.
        // Persist the collapsed state in a separate list.
        if (viewModel.IsCollapsed)
        {
            viewModel.IsCollapsed = false;
            _collapsedSpanIds.Remove(viewModel.Span.SpanId);
        }
        else
        {
            viewModel.IsCollapsed = true;
            _collapsedSpanIds.Add(viewModel.Span.SpanId);
        }
    }

    private async Task OnShowPropertiesAsync(SpanWaterfallViewModel viewModel, string? buttonId)
    {
        _elementIdBeforeDetailsViewOpened = buttonId;

        if (SelectedSpan?.Span == viewModel.Span)
        {
            await ClearSelectedSpanAsync();
        }
        else
        {
            List<SpanPropertyViewModel>? entryProperties = viewModel.Span.AllProperties()
                .Select(kvp => new SpanPropertyViewModel { Name = kvp.Key, Value = kvp.Value })
                .ToList();

            Dictionary<string, OtlpTrace>? traceCache = new Dictionary<string, OtlpTrace>(StringComparer.Ordinal);

            List<SpanLinkViewModel>? links = viewModel.Span.Links.Select(l => CreateLinkViewModel(l.TraceId, l.SpanId, l.Attributes, traceCache)).ToList();
            List<SpanLinkViewModel>? backlinks = viewModel.Span.BackLinks.Select(l => CreateLinkViewModel(l.SourceTraceId, l.SourceSpanId, l.Attributes, traceCache)).ToList();

            SpanDetailsViewModel? spanDetailsViewModel = new SpanDetailsViewModel
            {
                Span = viewModel.Span,
                Applications = _applications,
                Properties = entryProperties,
                Title = SpanWaterfallViewModel.GetTitle(viewModel.Span, _applications),
                Links = links,
                Backlinks = backlinks,
            };

            SelectedSpan = spanDetailsViewModel;
        }
    }

    private SpanLinkViewModel CreateLinkViewModel(string traceId, string spanId, KeyValuePair<string, string>[] attributes, Dictionary<string, OtlpTrace> traceCache)
    {
        if (!traceCache.TryGetValue(traceId, out OtlpTrace? trace))
        {
            trace = TelemetryRepository.GetTrace(traceId);
            if (trace != null)
            {
                traceCache[traceId] = trace;
            }
        }

        OtlpSpan? linkSpan = trace?.Spans.FirstOrDefault(s => s.SpanId == spanId);

        return new SpanLinkViewModel
        {
            TraceId = traceId,
            SpanId = spanId,
            Attributes = attributes,
            Span = linkSpan,
        };
    }

    private async Task ClearSelectedSpanAsync(bool causedByUserAction = false)
    {
        SelectedSpan = null;

        if (_elementIdBeforeDetailsViewOpened is not null && causedByUserAction)
        {
            await JS.InvokeVoidAsync("focusElement", _elementIdBeforeDetailsViewOpened);
        }

        _elementIdBeforeDetailsViewOpened = null;
    }

    private string GetResourceName(OtlpApplication app) => OtlpApplication.GetResourceName(app, _applications);

    public void Dispose()
    {
        foreach (IDisposable? subscription in _peerChangesSubscriptions)
        {
            subscription.Dispose();
        }
        _tracesSubscription?.Dispose();
    }

    private sealed record TraceDetailState(IEnumerable<IOutgoingPeerResolver> OutgoingPeerResolvers, List<string> CollapsedSpanIds);
}