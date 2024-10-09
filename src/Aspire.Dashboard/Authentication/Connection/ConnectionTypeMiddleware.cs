// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Turbine.Dashboard.Authentication.Connection;

/// <summary>
/// This connection middleware registers an OTLP feature on the connection.
/// OTLP services check for this feature when authorizing incoming requests to
/// ensure OTLP is only available on specified connections.
/// </summary>
internal sealed class ConnectionTypeMiddleware
{
    private readonly List<ConnectionType> _connectionTypes;
    private readonly ConnectionDelegate _next;

    public ConnectionTypeMiddleware(ConnectionType[] connectionTypes, ConnectionDelegate next)
    {
        _connectionTypes = connectionTypes.ToList();
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task OnConnectionAsync(ConnectionContext context)
    {
        context.Features.Set<IConnectionTypeFeature>(new ConnectionTypeFeature { ConnectionTypes = _connectionTypes });
        await _next(context).ConfigureAwait(false);
    }

    private sealed class ConnectionTypeFeature : IConnectionTypeFeature
    {
        public required List<ConnectionType> ConnectionTypes { get; init; }
    }
}