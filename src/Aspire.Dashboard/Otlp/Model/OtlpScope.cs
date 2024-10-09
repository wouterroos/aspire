// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Turbine.Dashboard.Configuration;
using OpenTelemetry.Proto.Common.V1;

namespace Turbine.Dashboard.Otlp.Model;

/// <summary>
/// The Scope of a TraceSource, maps to the name of the ActivitySource in .NET
/// </summary>
public class OtlpScope
{
    public static readonly OtlpScope Empty = new OtlpScope();

    public string ScopeName { get; }
    public string Version { get; }

    public ReadOnlyMemory<KeyValuePair<string, string>> Attributes { get; }

    private OtlpScope()
    {
        ScopeName = string.Empty;
        Attributes = Array.Empty<KeyValuePair<string, string>>();
        Version = string.Empty;
    }

    public OtlpScope(InstrumentationScope scope, TelemetryLimitOptions options)
    {
        ScopeName = scope.Name;

        Attributes = scope.Attributes.ToKeyValuePairs(options);
        Version = scope.Version;
    }
}