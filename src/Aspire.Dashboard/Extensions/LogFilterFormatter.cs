// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Turbine.Dashboard.Model.Otlp;

namespace Turbine.Dashboard.Extensions;

public static class LogFilterFormatter
{
    private static string SerializeLogFilterToString(LogFilter filter)
    {
        string? condition = filter.Condition switch
        {
            FilterCondition.Equals => "equals",
            FilterCondition.Contains => "contains",
            FilterCondition.GreaterThan => "gt",
            FilterCondition.LessThan => "lt",
            FilterCondition.GreaterThanOrEqual => "gte",
            FilterCondition.LessThanOrEqual => "lte",
            FilterCondition.NotEqual => "!equals",
            FilterCondition.NotContains => "!contains",
            _ => null
        };

        return $"{filter.Field}:{condition}:{Uri.EscapeDataString(filter.Value)}";
    }

    public static string SerializeLogFiltersToString(IEnumerable<LogFilter> filters)
    {
        // "%2B" is the escaped form of +
        return string.Join("%2B", filters.Select(SerializeLogFilterToString));
    }

    private static LogFilter? DeserializeLogFilterFromString(string filterString)
    {
        string[]? parts = filterString.Split(':');
        if (parts.Length != 3)
        {
            return null;
        }

        string? field = parts[0];

        FilterCondition? condition = parts[1] switch
        {
            "equals" => FilterCondition.Equals,
            "contains" => FilterCondition.Contains,
            "gt" => FilterCondition.GreaterThan,
            "lt" => FilterCondition.LessThan,
            "gte" => FilterCondition.GreaterThanOrEqual,
            "lte" => FilterCondition.LessThanOrEqual,
            "!equals" => FilterCondition.NotEqual,
            "!contains" => FilterCondition.NotContains,
            _ => null
        };

        if (condition is null)
        {
            return null;
        }

        string? value = Uri.UnescapeDataString(parts[2]);

        return new LogFilter { Condition = condition.Value, Field = field, Value = value };
    }

    public static List<LogFilter> DeserializeLogFiltersFromString(string filtersString)
    {
        return filtersString
            .Split('+') // + turns into space from query parameter (' ')
            .Select(DeserializeLogFilterFromString)
            .Where(filter => filter is not null)
            .Cast<LogFilter>()
            .ToList();
    }
}