// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Turbine.Dashboard.Otlp.Model;

public static class DurationFormatter
{
    [DebuggerDisplay("Unit = {Unit}, Ticks = {Ticks}, IsDecimal = {IsDecimal}")]
    private sealed class UnitStep
    {
        public required string Unit { get; init; }
        public required long Ticks { get; init; }
        public bool IsDecimal { get; init; }
    }

    private static readonly List<UnitStep> s_unitSteps = new List<UnitStep>
    {
        new UnitStep { Unit = "d", Ticks = TimeSpan.TicksPerDay },
        new UnitStep { Unit = "h", Ticks = TimeSpan.TicksPerHour },
        new UnitStep { Unit = "m", Ticks = TimeSpan.TicksPerMinute },
        new UnitStep { Unit = "s", Ticks = TimeSpan.TicksPerSecond, IsDecimal = true },
        new UnitStep { Unit = "ms", Ticks = TimeSpan.TicksPerMillisecond, IsDecimal = true },
        new UnitStep { Unit = "μs", Ticks = TimeSpan.TicksPerMicrosecond, IsDecimal = true },
    };

    public static string FormatDuration(TimeSpan duration)
    {
        (UnitStep? primaryUnit, UnitStep? secondaryUnit) = ResolveUnits(duration.Ticks);
        long ofPrevious = primaryUnit.Ticks / secondaryUnit.Ticks;
        double ticks = (double)duration.Ticks;

        if (primaryUnit.IsDecimal)
        {
            // If the unit is decimal based, display as a decimal
            return $"{ticks / primaryUnit.Ticks:0.##}{primaryUnit.Unit}";
        }

        double primaryValue = Math.Floor(ticks / primaryUnit.Ticks);
        string? primaryUnitString = $"{primaryValue}{primaryUnit.Unit}";
        double secondaryValue = Math.Round((ticks / secondaryUnit.Ticks) % ofPrevious, MidpointRounding.AwayFromZero);
        string? secondaryUnitString = $"{secondaryValue}{secondaryUnit.Unit}";

        return secondaryValue == 0 ? primaryUnitString : $"{primaryUnitString} {secondaryUnitString}";
    }

    public static string GetUnit(TimeSpan duration)
    {
        (UnitStep? primaryUnit, UnitStep? secondaryUnit) = ResolveUnits(duration.Ticks);
        if (primaryUnit.IsDecimal)
        {
            return primaryUnit.Unit;
        }
        return secondaryUnit.Unit;
    }

    private static (UnitStep, UnitStep) ResolveUnits(long ticks)
    {
        for (int i = 0; i < s_unitSteps.Count; i++)
        {
            UnitStep? step = s_unitSteps[i];
            bool result = i < s_unitSteps.Count - 1 && step.Ticks > ticks;

            if (!result)
            {
                return (step, i < s_unitSteps.Count - 1 ? s_unitSteps[i + 1] : step);
            }
        }

        return (s_unitSteps[^1], s_unitSteps[^1]);
    }
}