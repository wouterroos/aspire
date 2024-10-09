// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Turbine.Dashboard.Extensions;

internal static class ComponentExtensions
{
    public static async Task ExecuteOnDefault<T>(this FluentDataGridRow<T> row, Func<T, Task> call)
    {
        // Don't trigger on header rows.
        if (row.RowType == DataGridRowType.Default)
        {
            await call(row.Item!).ConfigureAwait(false);
        }
    }

    public static void ExecuteOnDefault<T>(this FluentDataGridRow<T> row, Action<T> call)
    {
        // Don't trigger on header rows.
        if (row.RowType == DataGridRowType.Default)
        {
            call(row.Item!);
        }
    }
}