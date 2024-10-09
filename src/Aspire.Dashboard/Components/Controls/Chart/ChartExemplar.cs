// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Components.Controls.Chart;

[DebuggerDisplay("Start = {Start}, Value = {Value}, TraceId = {TraceId}, SpanId = {SpanId}")]
public class ChartExemplar
{
    public required DateTimeOffset Start { get; init; }
    public required double Value { get; init; }
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public required OtlpSpan? Span { get; init; }
}