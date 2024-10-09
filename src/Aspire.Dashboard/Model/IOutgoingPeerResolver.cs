// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Turbine.Dashboard.Model;

public interface IOutgoingPeerResolver
{
    bool TryResolvePeerName(KeyValuePair<string, string>[] attributes, out string? name);

    IDisposable OnPeerChanges(Func<Task> callback);
}