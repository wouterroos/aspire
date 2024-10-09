// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Turbine.Dashboard.Model.Otlp;

[DebuggerDisplay("{FilterText,nq}")]
public class LogFilter
{
    public const string KnownMessageField = "log.message";
    public const string KnownCategoryField = "log.category";
    public const string KnownApplicationField = "log.application";
    public const string KnownTraceIdField = "log.traceid";
    public const string KnownSpanIdField = "log.spanid";
    public const string KnownOriginalFormatField = "log.originalformat";

    public string Field { get; set; } = default!;
    public FilterCondition Condition { get; set; }
    public string Value { get; set; } = default!;

    public string DebuggerDisplayText => $"{Field} {ConditionToString(Condition, null)} {Value}";

    public string GetDisplayText(IStringLocalizer<Logs> loc) => $"{ResolveFieldName(Field)} {ConditionToString(Condition, loc)} {Value}";

    public static string ResolveFieldName(string name)
    {
        return name switch
        {
            KnownMessageField => "Message",
            KnownApplicationField => "Application",
            KnownTraceIdField => "TraceId",
            KnownSpanIdField => "SpanId",
            KnownOriginalFormatField => "OriginalFormat",
            KnownCategoryField => "Category",
            _ => name
        };
    }

    public static string ConditionToString(FilterCondition c, IStringLocalizer<Logs>? loc) =>
        c switch
        {
            FilterCondition.Equals => "==",
            FilterCondition.Contains => loc?[nameof(Logs.LogContains)] ?? "contains",
            FilterCondition.GreaterThan => ">",
            FilterCondition.LessThan => "<",
            FilterCondition.GreaterThanOrEqual => ">=",
            FilterCondition.LessThanOrEqual => "<=",
            FilterCondition.NotEqual => "!=",
            FilterCondition.NotContains => loc?[nameof(Logs.LogNotContains)] ?? "not contains",
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };

    private static Func<string?, string, bool> ConditionToFuncString(FilterCondition c) =>
        c switch
        {
            FilterCondition.Equals => (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
            FilterCondition.Contains => (a, b) => a != null && a.Contains(b, StringComparison.OrdinalIgnoreCase),
            // Condition.GreaterThan => (a, b) => a > b,
            // Condition.LessThan => (a, b) => a < b,
            // Condition.GreaterThanOrEqual => (a, b) => a >= b,
            // Condition.LessThanOrEqual => (a, b) => a <= b,
            FilterCondition.NotEqual => (a, b) => !string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
            FilterCondition.NotContains => (a, b) => a != null && !a.Contains(b, StringComparison.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };

    private static Func<DateTime, DateTime, bool> ConditionToFuncDate(FilterCondition c) =>
        c switch
        {
            FilterCondition.Equals => (a, b) => a == b,
            //Condition.Contains => (a, b) => a.Contains(b),
            FilterCondition.GreaterThan => (a, b) => a > b,
            FilterCondition.LessThan => (a, b) => a < b,
            FilterCondition.GreaterThanOrEqual => (a, b) => a >= b,
            FilterCondition.LessThanOrEqual => (a, b) => a <= b,
            FilterCondition.NotEqual => (a, b) => a != b,
            //Condition.NotContains => (a, b) => !a.Contains(b),
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };

    private static Func<double, double, bool> ConditionToFuncNumber(FilterCondition c) =>
        c switch
        {
            FilterCondition.Equals => (a, b) => a == b,
            //Condition.Contains => (a, b) => a.Contains(b),
            FilterCondition.GreaterThan => (a, b) => a > b,
            FilterCondition.LessThan => (a, b) => a < b,
            FilterCondition.GreaterThanOrEqual => (a, b) => a >= b,
            FilterCondition.LessThanOrEqual => (a, b) => a <= b,
            FilterCondition.NotEqual => (a, b) => a != b,
            //Condition.NotContains => (a, b) => !a.Contains(b),
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };

    private string? GetFieldValue(OtlpLogEntry x)
    {
        return Field switch
        {
            KnownMessageField => x.Message,
            KnownApplicationField => x.Application.ApplicationName,
            KnownTraceIdField => x.TraceId,
            KnownSpanIdField => x.SpanId,
            KnownOriginalFormatField => x.OriginalFormat,
            KnownCategoryField => x.Scope.ScopeName,
            _ => x.Attributes.GetValue(Field)
        };
    }

    public IEnumerable<OtlpLogEntry> Apply(IEnumerable<OtlpLogEntry> input)
    {
        switch (Field)
        {
            case nameof(OtlpLogEntry.TimeStamp):
                {
                    DateTime date = DateTime.Parse(Value, CultureInfo.InvariantCulture);
                    Func<DateTime, DateTime, bool>? func = ConditionToFuncDate(Condition);
                    return input.Where(x => func(x.TimeStamp, date));
                }
            case nameof(OtlpLogEntry.Severity):
                {
                    if (Enum.TryParse<LogLevel>(Value, ignoreCase: true, out LogLevel value))
                    {
                        Func<double, double, bool>? func = ConditionToFuncNumber(Condition);
                        return input.Where(x => func((int)x.Severity, (double)value));
                    }
                    return input;
                }
            case nameof(OtlpLogEntry.Message):
                {
                    Func<string, string, bool>? func = ConditionToFuncString(Condition);
                    return input.Where(x => func(x.Message, Value));
                }
            default:
                {
                    Func<string?, string, bool> func = ConditionToFuncString(Condition);
                    return input.Where(x => func(GetFieldValue(x), Value));
                }
        }
    }
}