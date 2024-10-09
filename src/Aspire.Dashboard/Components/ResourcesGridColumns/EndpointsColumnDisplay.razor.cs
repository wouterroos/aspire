// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Turbine.Dashboard.Components;

public partial class EndpointsColumnDisplay
{
    [Parameter, EditorRequired]
    public required ResourceViewModel Resource { get; set; }

    [Parameter, EditorRequired]
    public required bool HasMultipleReplicas { get; set; }

    [Parameter, EditorRequired]
    public required IList<DisplayedEndpoint> DisplayedEndpoints { get; set; }

    [Parameter]
    public string? AdditionalMessage { get; set; }

    [Inject]
    public required ILogger<EndpointsColumnDisplay> Logger { get; init; }

    [Inject]
    public required IStringLocalizer<Columns> Loc { get; init; }

    private bool _popoverVisible;
}