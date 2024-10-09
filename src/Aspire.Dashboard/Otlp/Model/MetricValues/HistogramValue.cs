// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text;
using Turbine.Dashboard.Extensions;

namespace Turbine.Dashboard.Otlp.Model.MetricValues;

public class HistogramValue : MetricValueBase
{
    public ulong[] Values { get; init; }
    public double Sum { get; init; }
    public double[] ExplicitBounds { get; init; }

    public HistogramValue(ulong[] values, double sum, ulong count, DateTime start, DateTime end, double[] explicitBounds) : base(start, end)
    {
        Values = values;
        Sum = sum;
        Count = count;
        ExplicitBounds = explicitBounds;
    }

    public override string ToString()
    {
        StringBuilder? sb = new StringBuilder();
        bool first = true;
        sb.Append(CultureInfo.InvariantCulture, $"Count:{Count} Sum:{Sum} Values:");
        foreach (ulong v in Values)
        {
            if (!first)
            {
                sb.Append(' ');
            }
            first = false;
            sb.Append(CultureInfo.InvariantCulture, $"{v}");
        }
        return sb.ToString();
    }

    internal override bool TryCompare(MetricValueBase other, out int comparisonResult)
    {
        comparisonResult = default;
        return false;
    }

    protected override MetricValueBase Clone()
    {
        HistogramValue? value = new HistogramValue(Values, Sum, Count, Start, End, ExplicitBounds);
        if (HasExemplars)
        {
            value.Exemplars.AddRange(Exemplars);
        }
        return value;
    }

    public override bool Equals(object? obj)
    {
        return obj is HistogramValue other
            && Values.Equivalent(other.Values)
            && Sum.Equals(other.Sum)
            && Count.Equals(other.Count)
            && Start.Equals(other.Start)
            && ExplicitBounds.Equivalent(other.ExplicitBounds);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, Count, Values, Sum, ExplicitBounds);
    }
}