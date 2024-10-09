// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Utils;
using Google.Protobuf.WellKnownTypes;
using Turbine.Dashboard.Extensions;
using Turbine.Dashboard.Components.Resize;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Storage;
using Turbine.Dashboard.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Turbine.Dashboard.Components.Pages;

public partial class Resources : ComponentBase, IAsyncDisposable
{
    private const string TypeColumn = nameof(TypeColumn);
    private const string NameColumn = nameof(NameColumn);
    private const string StateColumn = nameof(StateColumn);
    private const string StartTimeColumn = nameof(StartTimeColumn);
    private const string SourceColumn = nameof(SourceColumn);
    private const string EndpointsColumn = nameof(EndpointsColumn);
    private const string LogsColumn = nameof(LogsColumn);
    private const string DetailsColumn = nameof(DetailsColumn);
    private const string CommandsColumn = nameof(CommandsColumn);

    private Subscription? _logsSubscription;
    private Dictionary<OtlpApplication, int>? _applicationUnviewedErrorCounts;

    [Inject]
    public required IDashboardClient DashboardClient { get; init; }

    [Inject]
    public required TelemetryRepository TelemetryRepository { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required IDialogService DialogService { get; init; }

    [Inject]
    public required IToastService ToastService { get; init; }

    [Inject]
    public required BrowserTimeProvider TimeProvider { get; init; }

    [Inject]
    public required IJSRuntime JS { get; init; }

    [Inject]
    public required ProtectedSessionStorage SessionStorage { get; init; }

    [Inject]
    public required DimensionManager DimensionManager { get; init; }

    [CascadingParameter]
    public required ViewportInformation ViewportInformation { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? VisibleTypes { get; set; }

    private ResourceViewModel? SelectedResource { get; set; }

    private readonly CancellationTokenSource _watchTaskCancellationTokenSource = new();
    private readonly ConcurrentDictionary<string, ResourceViewModel> _resourceByName = new(StringComparers.ResourceName);
    private readonly ConcurrentDictionary<string, bool> _allResourceTypes = [];
    private readonly ConcurrentDictionary<string, bool> _visibleResourceTypes = new(StringComparers.ResourceName);
    private string _filter = "";
    private bool _isTypeFilterVisible;
    private Task? _resourceSubscriptionTask;
    private bool _isLoading = true;
    private string? _elementIdBeforeDetailsViewOpened;
    private GridColumnManager _manager = null!;

    private bool Filter(ResourceViewModel resource) => _visibleResourceTypes.ContainsKey(resource.ResourceType) && (_filter.Length == 0 || resource.MatchesFilter(_filter)) && !resource.IsHiddenState();

    private Task OnResourceTypeVisibilityChangedAsync(string resourceType, bool isVisible)
    {
        if (isVisible)
        {
            _visibleResourceTypes[resourceType] = true;
        }
        else
        {
            _visibleResourceTypes.TryRemove(resourceType, out _);
        }

        return ClearSelectedResourceAsync();
    }

    private Task HandleSearchFilterChangedAsync()
    {
        return ClearSelectedResourceAsync();
    }

    private bool? AreAllTypesVisible
    {
        get
        {
            static bool SetEqualsKeys(ConcurrentDictionary<string, bool> left, ConcurrentDictionary<string, bool> right)
            {
                // PERF: This is inefficient since Keys locks and copies the keys.
                ICollection<string>? keysLeft = left.Keys;
                ICollection<string>? keysRight = right.Keys;

                return keysLeft.Count == keysRight.Count && keysLeft.OrderBy(key => key, StringComparers.ResourceType).SequenceEqual(keysRight.OrderBy(key => key, StringComparers.ResourceType), StringComparers.ResourceType);
            }

            return SetEqualsKeys(_visibleResourceTypes, _allResourceTypes)
                ? true
                : _visibleResourceTypes.IsEmpty
                    ? false
                    : null;
        }
        set
        {
            static bool UnionWithKeys(ConcurrentDictionary<string, bool> left, ConcurrentDictionary<string, bool> right)
            {
                // .Keys locks and copies the keys so avoid it here.
                foreach ((string? key, bool _) in right)
                {
                    left[key] = true;
                }

                return true;
            }

            if (value is true)
            {
                UnionWithKeys(_visibleResourceTypes, _allResourceTypes);
            }
            else if (value is false)
            {
                _visibleResourceTypes.Clear();
            }

            StateHasChanged();
        }
    }

    private bool HasResourcesWithCommands => _resourceByName.Any(r => r.Value.Commands.Any());

    private IQueryable<ResourceViewModel>? FilteredResources => _resourceByName.Values.Where(Filter).OrderBy(e => e.ResourceType).ThenBy(e => e.Name).AsQueryable();

    private readonly GridSort<ResourceViewModel> _nameSort = GridSort<ResourceViewModel>.ByAscending(p => p.Name);
    private readonly GridSort<ResourceViewModel> _stateSort = GridSort<ResourceViewModel>.ByAscending(p => p.State);
    private readonly GridSort<ResourceViewModel> _startTimeSort = GridSort<ResourceViewModel>.ByDescending(p => p.CreationTimeStamp);

    protected override async Task OnInitializedAsync()
    {
        _manager = new GridColumnManager([
            new GridColumn(Name: TypeColumn, DesktopWidth: "1fr", MobileWidth: "1fr"),
            new GridColumn(Name: NameColumn, DesktopWidth: "1.5fr", MobileWidth: "1.5fr"),
            new GridColumn(Name: StateColumn, DesktopWidth: "1.25fr"),
            new GridColumn(Name: StartTimeColumn, DesktopWidth: "1.5fr"),
            new GridColumn(Name: SourceColumn, DesktopWidth: "2.5fr"),
            new GridColumn(Name: EndpointsColumn, DesktopWidth: "2.5fr", MobileWidth: "2fr"),
            new GridColumn(Name: LogsColumn, DesktopWidth: "1fr"),
            new GridColumn(Name: DetailsColumn, DesktopWidth: "1fr", MobileWidth: "1fr"),
            new GridColumn(Name: CommandsColumn, DesktopWidth: "1fr", IsVisible: () => HasResourcesWithCommands)
        ], DimensionManager);

        _applicationUnviewedErrorCounts = TelemetryRepository.GetApplicationUnviewedErrorLogsCount();

        if (DashboardClient.IsEnabled)
        {
            await SubscribeResourcesAsync();
        }

        _logsSubscription = TelemetryRepository.OnNewLogs(null, SubscriptionType.Other, async () =>
        {
            Dictionary<OtlpApplication, int>? newApplicationUnviewedErrorCounts = TelemetryRepository.GetApplicationUnviewedErrorLogsCount();

            // Only update UI if the error counts have changed.
            if (ApplicationErrorCountsChanged(newApplicationUnviewedErrorCounts))
            {
                _applicationUnviewedErrorCounts = newApplicationUnviewedErrorCounts;
                await InvokeAsync(StateHasChanged);
            }
        });

        _isLoading = false;

        async Task SubscribeResourcesAsync()
        {
            HashSet<string>? preselectedVisibleResourceTypes = VisibleTypes?.Split(',').ToHashSet();

            (ImmutableArray<ResourceViewModel> snapshot, IAsyncEnumerable<IReadOnlyList<ResourceViewModelChange>>? subscription) = await DashboardClient.SubscribeResourcesAsync(_watchTaskCancellationTokenSource.Token);

            // Apply snapshot.
            foreach (ResourceViewModel? resource in snapshot)
            {
                bool added = _resourceByName.TryAdd(resource.Name, resource);

                _allResourceTypes.TryAdd(resource.ResourceType, true);

                if (preselectedVisibleResourceTypes is null || preselectedVisibleResourceTypes.Contains(resource.ResourceType))
                {
                    _visibleResourceTypes.TryAdd(resource.ResourceType, true);
                }

                Debug.Assert(added, "Should not receive duplicate resources in initial snapshot data.");
            }

            // Listen for updates and apply.
            _resourceSubscriptionTask = Task.Run(async () =>
            {
                await foreach (IReadOnlyList<ResourceViewModelChange>? changes in subscription.WithCancellation(_watchTaskCancellationTokenSource.Token).ConfigureAwait(false))
                {
                    foreach ((ResourceViewModelChangeType changeType, ResourceViewModel? resource) in changes)
                    {
                        if (changeType == ResourceViewModelChangeType.Upsert)
                        {
                            _resourceByName[resource.Name] = resource;

                            _allResourceTypes[resource.ResourceType] = true;
                            _visibleResourceTypes[resource.ResourceType] = true;
                        }
                        else if (changeType == ResourceViewModelChangeType.Delete)
                        {
                            bool removed = _resourceByName.TryRemove(resource.Name, out _);
                            Debug.Assert(removed, "Cannot remove unknown resource.");
                        }
                    }

                    await InvokeAsync(StateHasChanged);
                }
            });
        }
    }

    private bool ApplicationErrorCountsChanged(Dictionary<OtlpApplication, int> newApplicationUnviewedErrorCounts)
    {
        if (_applicationUnviewedErrorCounts == null || _applicationUnviewedErrorCounts.Count != newApplicationUnviewedErrorCounts.Count)
        {
            return true;
        }

        foreach ((OtlpApplication? application, int count) in newApplicationUnviewedErrorCounts)
        {
            if (!_applicationUnviewedErrorCounts.TryGetValue(application, out int oldCount) || oldCount != count)
            {
                return true;
            }
        }

        return false;
    }

    private async Task ShowResourceDetailsAsync(ResourceViewModel resource, string? buttonId)
    {
        _elementIdBeforeDetailsViewOpened = buttonId;

        if (SelectedResource == resource)
        {
            await ClearSelectedResourceAsync();
        }
        else
        {
            SelectedResource = resource;
        }
    }

    private async Task ClearSelectedResourceAsync(bool causedByUserAction = false)
    {
        SelectedResource = null;

        await InvokeAsync(StateHasChanged);

        if (_elementIdBeforeDetailsViewOpened is not null && causedByUserAction)
        {
            await JS.InvokeVoidAsync("focusElement", _elementIdBeforeDetailsViewOpened);
        }

        _elementIdBeforeDetailsViewOpened = null;
    }

    private string GetResourceName(ResourceViewModel resource) => ResourceViewModel.GetResourceName(resource, _resourceByName);

    private bool HasMultipleReplicas(ResourceViewModel resource)
    {
        int count = 0;
        foreach ((string _, ResourceViewModel? item) in _resourceByName)
        {
            if (item.IsHiddenState())
            {
                continue;
            }

            if (item.DisplayName == resource.DisplayName)
            {
                count++;
                if (count >= 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string GetRowClass(ResourceViewModel resource)
        => resource == SelectedResource ? "selected-row resource-row" : "resource-row";

    private async Task ExecuteResourceCommandAsync(ResourceViewModel resource, CommandViewModel command)
    {
        if (!string.IsNullOrWhiteSpace(command.ConfirmationMessage))
        {
            IDialogReference? dialogReference = await DialogService.ShowConfirmationAsync(command.ConfirmationMessage);
            DialogResult? result = await dialogReference.Result;
            if (result.Cancelled)
            {
                return;
            }
        }

        ResourceCommandResponseViewModel? response = await DashboardClient.ExecuteResourceCommandAsync(resource.Name, resource.ResourceType, command, CancellationToken.None);

        if (response.Kind == ResourceCommandResponseKind.Succeeded)
        {
            ToastService.ShowSuccess(string.Format(CultureInfo.InvariantCulture, Loc[nameof(Dashboard.Resources.Resources.ResourceCommandSuccess)], command.DisplayName));
        }
        else
        {
            ToastService.ShowCommunicationToast(new ToastParameters<CommunicationToastContent>()
            {
                Intent = ToastIntent.Error,
                Title = string.Format(CultureInfo.InvariantCulture, Loc[nameof(Dashboard.Resources.Resources.ResourceCommandFailed)], command.DisplayName),
                PrimaryAction = Loc[nameof(Dashboard.Resources.Resources.ResourceCommandToastViewLogs)],
                OnPrimaryAction = EventCallback.Factory.Create<ToastResult>(this, () => NavigationManager.NavigateTo(DashboardUrls.ConsoleLogsUrl(resource: resource.Name))),
                Content = new CommunicationToastContent()
                {
                    Details = response.ErrorMessage
                }
            });
        }
    }

    private static (string Value, string? ContentAfterValue, string ValueToCopy, string Tooltip)? GetSourceColumnValueAndTooltip(ResourceViewModel resource)
    {
        // NOTE projects are also executables, so we have to check for projects first
        if (resource.IsProject() && resource.TryGetProjectPath(out string? projectPath))
        {
            return (Value: Path.GetFileName(projectPath), ContentAfterValue: null, ValueToCopy: projectPath, Tooltip: projectPath);
        }

        if (resource.TryGetExecutablePath(out string? executablePath))
        {
            resource.TryGetExecutableArguments(out ImmutableArray<string> arguments);
            string? argumentsString = arguments.IsDefaultOrEmpty ? "" : string.Join(" ", arguments);
            string? fullCommandLine = $"{executablePath} {argumentsString}";

            return (Value: Path.GetFileName(executablePath), ContentAfterValue: argumentsString, ValueToCopy: fullCommandLine, Tooltip: fullCommandLine);
        }

        if (resource.TryGetContainerImage(out string? containerImage))
        {
            return (Value: containerImage, ContentAfterValue: null, ValueToCopy: containerImage, Tooltip: containerImage);
        }

        if (resource.Properties.TryGetValue(KnownProperties.Resource.Source, out Value? value) && value.HasStringValue)
        {
            return (Value: value.StringValue, ContentAfterValue: null, ValueToCopy: value.StringValue, Tooltip: value.StringValue);
        }

        return null;
    }

    private string GetEndpointsTooltip(ResourceViewModel resource)
    {
        List<DisplayedEndpoint>? displayedEndpoints = GetDisplayedEndpoints(resource, out string? additionalMessage);

        if (additionalMessage is not null)
        {
            return additionalMessage;
        }

        if (displayedEndpoints.Count == 1)
        {
            return displayedEndpoints.First().Text;
        }

        int maxShownEndpoints = 3;
        StringBuilder? tooltipBuilder = new StringBuilder(string.Join(", ", displayedEndpoints.Take(maxShownEndpoints).Select(endpoint => endpoint.Text)));

        if (displayedEndpoints.Count > maxShownEndpoints)
        {
            tooltipBuilder.Append(CultureInfo.CurrentCulture, $" + {displayedEndpoints.Count - maxShownEndpoints}");
        }

        return tooltipBuilder.ToString();
    }

    private List<DisplayedEndpoint> GetDisplayedEndpoints(ResourceViewModel resource, out string? additionalMessage)
    {
        if (resource.Urls.Length == 0)
        {
            // If we have no endpoints, and the app isn't running anymore or we're not expecting any, then just say None
            additionalMessage = ColumnsLoc[nameof(Columns.EndpointsColumnDisplayNone)];
            return [];
        }

        additionalMessage = null;

        return ResourceEndpointHelpers.GetEndpoints(resource, includeInternalUrls: false);
    }

    public async ValueTask DisposeAsync()
    {
        _watchTaskCancellationTokenSource.Cancel();
        _watchTaskCancellationTokenSource.Dispose();
        _logsSubscription?.Dispose();

        await TaskHelpers.WaitIgnoreCancelAsync(_resourceSubscriptionTask);
    }
}