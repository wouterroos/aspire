// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Aspire;
using Turbine.Dashboard.Components.Resize;
using Turbine.Dashboard.Model;

namespace Turbine.Dashboard.Components;

public class GridColumnManager
{
    private readonly GridColumn[] _columns;
    private readonly DimensionManager _dimensionManager;

    public GridColumnManager(GridColumn[] columns, DimensionManager dimensionManager)
    {
        if (columns.DistinctBy(c => c.Name, StringComparers.GridColumn).Count() != columns.Length)
        {
            throw new InvalidOperationException("There are duplicate columns");
        }

        _columns = columns;
        _dimensionManager = dimensionManager;
    }

    public bool IsColumnVisible(string columnId)
    {
        return GetColumnWidth(_columns.First(column => column.Name == columnId)) is not null;
    }

    private string? GetColumnWidth(GridColumn column)
    {
        if (column.IsVisible is not null && !column.IsVisible())
        {
            return null;
        }

        if (_dimensionManager.ViewportInformation.IsDesktop)
        {
            return column.DesktopWidth;
        }

        return column.MobileWidth;
    }

    public string GetGridTemplateColumns()
    {
        IEnumerable<string>? visibleColumns = _columns
            .Select(GetColumnWidth)
            .Where(s => s is not null)
            .Select(s => s!);

        return string.Join(" ", visibleColumns);
    }
}