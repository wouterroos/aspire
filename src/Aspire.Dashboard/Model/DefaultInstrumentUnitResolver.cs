// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Globalization;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Resources;
using Humanizer;
using Microsoft.Extensions.Localization;

namespace Turbine.Dashboard.Model;

public sealed class DefaultInstrumentUnitResolver(IStringLocalizer<ControlsStrings> loc) : IInstrumentUnitResolver
{
    public string ResolveDisplayedUnit(OtlpInstrumentSummary instrument, bool titleCase, bool pluralize)
    {
        if (!string.IsNullOrEmpty(instrument.Unit))
        {
            string? unit = OtlpUnits.GetUnit(instrument.Unit.TrimStart('{').TrimEnd('}'));
            if (pluralize)
            {
                unit = unit.Pluralize();
            }
            if (titleCase)
            {
                unit = unit.Titleize();
            }
            return unit;
        }

        // Hard code for instrument names that don't have units
        // but have a descriptive name that lets us infer the unit.
        if (instrument.Name.EndsWith(".count"))
        {
            return UntitleCase(loc[nameof(ControlsStrings.PlotlyChartCount)], titleCase);
        }
        else if (instrument.Name.EndsWith(".length"))
        {
            return UntitleCase(loc[nameof(ControlsStrings.PlotlyChartLength)], titleCase);
        }
        else
        {
            return UntitleCase(loc[nameof(ControlsStrings.PlotlyChartValue)], titleCase);
        }

        static string UntitleCase(string value, bool titleCase)
        {
            if (!titleCase)
            {
                value = value.ToLower(CultureInfo.CurrentCulture);
            }
            return value;
        }
    }
}