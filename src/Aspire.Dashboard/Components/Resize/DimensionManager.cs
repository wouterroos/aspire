// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

namespace Turbine.Dashboard.Components.Resize;

public class DimensionManager
{
    private ViewportInformation? _viewportInformation;

    public event BrowserDimensionsChangedEventHandler? OnBrowserDimensionsChanged;

    public bool IsResizing { get; set; }
    public ViewportInformation ViewportInformation => _viewportInformation ?? throw new ArgumentNullException(nameof(_viewportInformation));

    internal void InvokeOnBrowserDimensionsChanged(ViewportInformation newViewportInformation)
    {
        _viewportInformation = newViewportInformation;
        OnBrowserDimensionsChanged?.Invoke(this, new BrowserDimensionsChangedEventArgs(newViewportInformation));
    }
}

public delegate void BrowserDimensionsChangedEventHandler(object sender, BrowserDimensionsChangedEventArgs e);

public class BrowserDimensionsChangedEventArgs(ViewportInformation viewportInformation) : EventArgs
{
    public ViewportInformation ViewportInformation { get; } = viewportInformation;
}