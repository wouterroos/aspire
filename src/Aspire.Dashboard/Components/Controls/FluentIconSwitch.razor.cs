// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;

namespace Turbine.Dashboard.Components.Controls;

public partial class FluentIconSwitch
{
    private async Task OnToggleInternalAsync()
    {
        Value = Value is not true;
        await ValueChanged.InvokeAsync(Value.Value);
        await OnToggle.InvokeAsync();
    }
}