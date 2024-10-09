// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Aspire.Dashboard.Components;

public partial class AspireMenuButton<TItem> : FluentComponentBase
{
    private static readonly Icon _defaultIcon = new Icons.Regular.Size24.ChevronDown();

    private bool _visible;
    private Icon? _icon;

    private readonly string _buttonId = Identifier.NewId();

    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public Icon? Icon { get; set; }

    [Parameter]
    public required IList<TItem> Items { get; set; }

    [Parameter]
    public required Func<TItem, string> ItemText { get; set; }

    [Parameter]
    public Appearance? ButtonAppearance { get; set; }

    [Parameter]
    public EventCallback<TItem> OnItemClicked { get; set; }

    protected override void OnParametersSet()
    {
        _icon = Icon ?? _defaultIcon;
    }

    private void ToggleMenu()
    {
        _visible = !_visible;
    }

    private async Task HandleItemClicked(TItem item)
    {
        if (item is not null)
        {
            await OnItemClicked.InvokeAsync(item);
        }
        _visible = false;
    }

    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args is not null && args.Key == "Escape")
        {
            _visible = false;
        }
    }
}