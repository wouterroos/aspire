// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Aspire;
using Aspire.Dashboard.Utils;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Utils;

namespace Turbine.Dashboard.Model;

public sealed class ResourceOutgoingPeerResolver : IOutgoingPeerResolver, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ResourceViewModel> _resourceByName = new(StringComparers.ResourceName);
    private readonly CancellationTokenSource _watchContainersTokenSource = new();
    private readonly List<ModelSubscription> _subscriptions = [];
    private readonly object _lock = new();
    private readonly Task? _watchTask;

    public ResourceOutgoingPeerResolver(IDashboardClient resourceService)
    {
        if (!resourceService.IsEnabled)
        {
            return;
        }

        _watchTask = Task.Run(async () =>
        {
            (ImmutableArray<ResourceViewModel> snapshot, IAsyncEnumerable<IReadOnlyList<ResourceViewModelChange>>? subscription) = await resourceService.SubscribeResourcesAsync(_watchContainersTokenSource.Token).ConfigureAwait(false);

            if (snapshot.Length > 0)
            {
                foreach (ResourceViewModel? resource in snapshot)
                {
                    bool added = _resourceByName.TryAdd(resource.Name, resource);
                    Debug.Assert(added, "Should not receive duplicate resources in initial snapshot data.");
                }

                await RaisePeerChangesAsync().ConfigureAwait(false);
            }

            await foreach (IReadOnlyList<ResourceViewModelChange>? changes in subscription.WithCancellation(_watchContainersTokenSource.Token).ConfigureAwait(false))
            {
                foreach ((ResourceViewModelChangeType changeType, ResourceViewModel? resource) in changes)
                {
                    if (changeType == ResourceViewModelChangeType.Upsert)
                    {
                        _resourceByName[resource.Name] = resource;
                    }
                    else if (changeType == ResourceViewModelChangeType.Delete)
                    {
                        bool removed = _resourceByName.TryRemove(resource.Name, out _);
                        Debug.Assert(removed, "Cannot remove unknown resource.");
                    }
                }

                await RaisePeerChangesAsync().ConfigureAwait(false);
            }
        });
    }

    public bool TryResolvePeerName(KeyValuePair<string, string>[] attributes, [NotNullWhen(true)] out string? name)
    {
        return TryResolvePeerNameCore(_resourceByName, attributes, out name);
    }

    internal static bool TryResolvePeerNameCore(IDictionary<string, ResourceViewModel> resources, KeyValuePair<string, string>[] attributes, out string? name)
    {
        string? address = OtlpHelpers.GetPeerAddress(attributes);
        if (address != null)
        {
            // Match exact value.
            if (TryMatchResourceAddress(address, out name))
            {
                return true;
            }

            // Resource addresses have the format "127.0.0.1:5000". Some libraries modify the peer.service value on the span.
            // If there isn't an exact match then transform the peer.service value and try to match again.
            // Change from transformers are cumulative. e.g. "localhost,5000" -> "localhost:5000" -> "127.0.0.1:5000"
            string? transformedAddress = address;
            foreach (Func<string, string>? transformer in s_addressTransformers)
            {
                transformedAddress = transformer(transformedAddress);
                if (TryMatchResourceAddress(transformedAddress, out name))
                {
                    return true;
                }
            }
        }

        name = null;
        return false;

        bool TryMatchResourceAddress(string value, [NotNullWhen(true)] out string? name)
        {
            foreach ((string? resourceName, ResourceViewModel? resource) in resources)
            {
                foreach (UrlViewModel? service in resource.Urls)
                {
                    string? hostAndPort = service.Url.GetComponents(UriComponents.Host | UriComponents.Port, UriFormat.UriEscaped);

                    if (string.Equals(hostAndPort, value, StringComparison.OrdinalIgnoreCase))
                    {
                        name = ResourceViewModel.GetResourceName(resource, resources);
                        return true;
                    }
                }
            }

            name = null;
            return false;
        }
    }

    private static readonly List<Func<string, string>> s_addressTransformers = [
        s =>
        {
            // SQL Server uses comma instead of colon for port.
            // https://www.connectionstrings.com/sql-server/
            if (s.AsSpan().Count(',') == 1)
            {
                return s.Replace(',', ':');
            }
            return s;
        },
        s =>
        {
            // Some libraries use "127.0.0.1" instead of "localhost".
            return s.Replace("127.0.0.1:", "localhost:");
        }];

    public IDisposable OnPeerChanges(Func<Task> callback)
    {
        lock (_lock)
        {
            ModelSubscription? subscription = new ModelSubscription(callback, RemoveSubscription);
            _subscriptions.Add(subscription);
            return subscription;
        }
    }

    private void RemoveSubscription(ModelSubscription subscription)
    {
        lock (_lock)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private async Task RaisePeerChangesAsync()
    {
        if (_subscriptions.Count == 0 || _watchContainersTokenSource.IsCancellationRequested)
        {
            return;
        }

        ModelSubscription[] subscriptions;
        lock (_lock)
        {
            subscriptions = _subscriptions.ToArray();
        }

        foreach (ModelSubscription? subscription in subscriptions)
        {
            await subscription.ExecuteAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _watchContainersTokenSource.Cancel();
        _watchContainersTokenSource.Dispose();

        await TaskHelpers.WaitIgnoreCancelAsync(_watchTask).ConfigureAwait(false);
    }
}