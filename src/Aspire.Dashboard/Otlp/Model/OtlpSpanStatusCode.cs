// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Turbine.Dashboard.Otlp.Model;

public enum OtlpSpanStatusCode
{
    /// <summary>
    /// The default status.
    /// </summary>
    Unset = 0,
    /// <summary>
    /// The Span has been validated by an Application developer or Operator to 
    /// have completed successfully.
    /// </summary>
    Ok = 1,
    /// <summary>
    /// The Span contains an error.
    /// </summary>
    Error = 2,
}
