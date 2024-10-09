// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Turbine.Dashboard.Authentication.Connection;

internal static class ListenOptionsConnectionTypeExtensions
{
    public static void UseConnectionTypes(this ListenOptions listenOptions, ConnectionType[] connectionTypes)
    {
        listenOptions.Use(next => new ConnectionTypeMiddleware(connectionTypes, next).OnConnectionAsync);
    }
}
