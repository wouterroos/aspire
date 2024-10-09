// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Model;

public interface IInstrumentUnitResolver
{
    string ResolveDisplayedUnit(OtlpInstrumentSummary instrument, bool titleCase, bool pluralize);
}
