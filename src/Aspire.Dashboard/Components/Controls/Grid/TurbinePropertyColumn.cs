// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Turbine.Dashboard.Components.Controls.Grid;

public class TurbinePropertyColumn<TGridItem, TProp> : PropertyColumn<TGridItem, TProp>, ITurbineColumn
{
    [Parameter]
    public GridColumnManager? ColumnManager { get; set; }

    [Parameter]
    public string? ColumnId { get; set; }

    [Parameter]
    public bool UseCustomHeaderTemplate { get; set; } = true;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (UseCustomHeaderTemplate)
        {
            HeaderCellItemTemplate = TurbineFluentDataGridHeaderCell.RenderHeaderContent(Grid);
        }
    }

    protected override bool ShouldRender()
    {
        if (ColumnManager is not null && ColumnId is not null && !ColumnManager.IsColumnVisible(ColumnId))
        {
            return false;
        }

        return base.ShouldRender();
    }
}
