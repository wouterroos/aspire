// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Model;

public sealed class SpanDetailsViewModel
{
    public required OtlpSpan Span { get; init; }
    public required List<SpanPropertyViewModel> Properties { get; init; }
    public required List<SpanLinkViewModel> Links { get; init; }
    public required List<SpanLinkViewModel> Backlinks { get; init; }
    public required string Title { get; init; }
    public required List<OtlpApplication> Applications { get; init; }
}