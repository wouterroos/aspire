// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Turbine.Dashboard.Model;
using Microsoft.AspNetCore.Components;

namespace Turbine.Dashboard.Components;

public partial class ResourceCommands : ComponentBase
{
    [Parameter]
    public required IList<CommandViewModel> Commands { get; set; }

    [Parameter]
    public EventCallback<CommandViewModel> CommandSelected { get; set; }
}