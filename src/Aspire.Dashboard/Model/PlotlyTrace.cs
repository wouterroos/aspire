// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Turbine.Dashboard.Model;

public class PlotlyTrace
{
    public required string Name { get; init; }
    public required List<DateTimeOffset> X { get; init; }
    public required List<double?> Y { get; init; }
    public required List<string?> Tooltips { get; init; }
    public required List<object?> TraceData { get; init; }
}

public class PlotlyUserLocale
{
    public required string Time { get; init; }
    public required List<string> Periods { get; init; }
}