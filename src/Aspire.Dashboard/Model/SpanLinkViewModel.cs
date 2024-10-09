// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Model;

public sealed class SpanLinkViewModel
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public required KeyValuePair<string, string>[] Attributes { get; init; }
    public required OtlpSpan? Span { get; init; }
}