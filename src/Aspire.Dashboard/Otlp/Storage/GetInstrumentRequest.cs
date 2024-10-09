// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

namespace Turbine.Dashboard.Otlp.Storage;

public sealed class GetInstrumentRequest
{
    public required string InstrumentName { get; init; }
    public required ApplicationKey ApplicationKey { get; init; }
    public required string MeterName { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
}