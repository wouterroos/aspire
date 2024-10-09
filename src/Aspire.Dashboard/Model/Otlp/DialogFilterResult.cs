// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Turbine.Dashboard.Model.Otlp;

public class FilterDialogResult
{
    public LogFilter? Filter { get; set; }
    public bool Delete { get; set; }
    public bool Add { get; set; }
}
