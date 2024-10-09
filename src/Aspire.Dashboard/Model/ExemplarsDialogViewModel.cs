// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Turbine.Dashboard.Components.Controls.Chart;
using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Model;

public sealed class ExemplarsDialogViewModel
{
    public required List<ChartExemplar> Exemplars { get; init; }
    public required List<OtlpApplication> Applications { get; init; }
    public required OtlpInstrumentSummary Instrument { get; init; }
}