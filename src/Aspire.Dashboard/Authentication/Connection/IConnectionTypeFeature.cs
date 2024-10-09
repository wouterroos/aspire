// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Turbine.Dashboard.Authentication.Connection;

/// <summary>
/// This feature's presence on a connection indicates that the connection is for OTLP.
/// </summary>
internal interface IConnectionTypeFeature
{
    List<ConnectionType> ConnectionTypes { get; }
}