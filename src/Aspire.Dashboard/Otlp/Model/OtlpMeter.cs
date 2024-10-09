// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using Turbine.Dashboard.Configuration;
using OpenTelemetry.Proto.Common.V1;

namespace Turbine.Dashboard.Otlp.Model;

[DebuggerDisplay("MeterName = {MeterName}")]
public class OtlpMeter
{
    public string MeterName { get; init; }
    public string Version { get; init; }

    public KeyValuePair<string, string>[] Attributes { get; }

    public OtlpMeter(InstrumentationScope scope, TelemetryLimitOptions options)
    {
        MeterName = scope.Name;
        Version = scope.Version;
        Attributes = scope.Attributes.ToKeyValuePairs(options);
    }
}