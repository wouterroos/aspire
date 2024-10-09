// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Turbine.Dashboard.Components.Controls.Chart;

public sealed class ChartTrace
{
    public int? Percentile { get; init; }
    public required string Name { get; init; }
    public List<double?> Values { get; } = new();
    public List<double?> DiffValues { get; } = new();
    public List<string?> Tooltips { get; } = new();
}