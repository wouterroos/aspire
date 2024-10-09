// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbine.Dashboard.Model;
using Microsoft.JSInterop;

namespace Turbine.Dashboard;

public sealed class ShortcutManager(ILoggerFactory loggerFactory) : IDisposable
{
    private readonly ConcurrentDictionary<IGlobalKeydownListener, IGlobalKeydownListener> _keydownListenerComponents = [];
    private readonly ILogger<ShortcutManager> _logger = loggerFactory.CreateLogger<ShortcutManager>();

    public void AddGlobalKeydownListener(IGlobalKeydownListener listener)
    {
        _keydownListenerComponents[listener] = listener;
    }

    public void RemoveGlobalKeydownListener(IGlobalKeydownListener listener)
    {
        _keydownListenerComponents.Remove(listener, out _);
    }

    [JSInvokable]
    public Task OnGlobalKeyDown(TurbineKeyboardShortcut shortcut)
    {
        _logger.LogDebug($"Received shortcut of type {shortcut}");

        IEnumerable<IGlobalKeydownListener>? componentsSubscribedToShortcut =
            _keydownListenerComponents.Values.Where(component => component.SubscribedShortcuts.Contains(shortcut));

        return Task.WhenAll(componentsSubscribedToShortcut.Select(component => component.OnPageKeyDownAsync(shortcut)));
    }

    public void Dispose()
    {
        _keydownListenerComponents.Clear();
    }
}