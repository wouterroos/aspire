// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Turbine.Dashboard.Otlp.Model;

public record struct OtlpInstrumentKey(string MeterName, string InstrumentName);
