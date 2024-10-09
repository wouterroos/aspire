// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Turbine.Dashboard.Otlp.Model;

public class OtlpSpanEvent
{
    public required string Name { get; init; }
    public required DateTime Time { get; init; }
    public required KeyValuePair<string, string>[] Attributes { get; init; }

    public TimeSpan TimeOffset(OtlpSpan span) => (Time - span.StartTime);
}