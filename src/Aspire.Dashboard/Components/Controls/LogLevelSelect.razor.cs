// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Turbine.Dashboard.Components.Controls;

public partial class LogLevelSelect : ComponentBase
{
    private async Task HandleSelectedLogLevelChangedInternalAsync()
    {
        await LogLevelChanged.InvokeAsync(LogLevel);
        await HandleSelectedLogLevelChangedAsync();
    }
}