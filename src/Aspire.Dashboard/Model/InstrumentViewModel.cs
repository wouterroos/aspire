// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Model.MetricValues;

namespace Turbine.Dashboard.Model;

public class InstrumentViewModel
{
    public OtlpInstrumentSummary? Instrument { get; private set; }
    public List<DimensionScope>? MatchedDimensions { get; private set; }

    public List<Func<Task>> DataUpdateSubscriptions { get; } = [];
    public string? Theme { get; set; }
    public bool ShowCount { get; set; }

    public async Task UpdateDataAsync(OtlpInstrumentSummary instrument, List<DimensionScope> matchedDimensions)
    {
        Instrument = instrument;
        MatchedDimensions = matchedDimensions;

        foreach (Func<Task>? subscription in DataUpdateSubscriptions)
        {
            await subscription().ConfigureAwait(false);
        }
    }
}