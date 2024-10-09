// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Turbine.Dashboard.Model;

public sealed class ModelSubscription(Func<Task> callback, Action<ModelSubscription> onDispose) : IDisposable
{
    private readonly Func<Task> _callback = callback;
    private readonly Action<ModelSubscription> _onDispose = onDispose;

    public void Dispose() => _onDispose(this);

    public Task ExecuteAsync() => _callback();
}