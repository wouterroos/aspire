// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Turbine.Dashboard.Otlp.Model;

[DebuggerDisplay("TraceId = {TraceId}, SpanId = {SpanId}, SourceTraceId = {SourceTraceId}, SourceSpanId = {SourceSpanId}")]
public class OtlpSpanLink
{
    public required string SourceTraceId { get; init; }
    public required string SourceSpanId { get; init; }
    public required string TraceState { get; init; }
    public required string SpanId { get; init; }
    public required string TraceId { get; init; }
    public required KeyValuePair<string, string>[] Attributes { get; init; }
}