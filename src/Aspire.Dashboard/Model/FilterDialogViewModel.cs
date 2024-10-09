// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Turbine.Dashboard.Model.Otlp;

namespace Turbine.Dashboard.Model;

public sealed class FilterDialogViewModel
{
    public required LogFilter? Filter { get; init; }
    public required List<string> LogPropertyKeys { get; init; }
}