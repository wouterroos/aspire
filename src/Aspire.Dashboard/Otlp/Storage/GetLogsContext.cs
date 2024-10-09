// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Turbine.Dashboard.Model.Otlp;

namespace Turbine.Dashboard.Otlp.Storage;

public sealed class GetLogsContext
{
    public required ApplicationKey? ApplicationKey { get; init; }
    public required int StartIndex { get; init; }
    public required int? Count { get; init; }
    public required List<LogFilter> Filters { get; init; }
}