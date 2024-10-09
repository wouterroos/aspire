// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Otlp.Storage;

public sealed class GetTracesResponse
{
    public required PagedResult<OtlpTrace> PagedResult { get; init; }
    public required TimeSpan MaxDuration { get; init; }
}