// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Turbine.Dashboard.Components;

namespace Aspire.Dashboard.Components.Controls.Grid;

internal interface IAspireColumn
{
    public GridColumnManager? ColumnManager { get; set; }

    public string? ColumnId { get; set; }

    public bool UseCustomHeaderTemplate { get; set; }
}