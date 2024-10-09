// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Turbine.Dashboard.Configuration;
using Turbine.Dashboard.Otlp.Model.MetricValues;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;

namespace Turbine.Dashboard.Otlp.Model;

[DebuggerDisplay("Name = {Name}, Unit = {Unit}, Type = {Type}")]
public class OtlpInstrumentSummary
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Unit { get; init; }
    public required OtlpInstrumentType Type { get; init; }
    public required OtlpMeter Parent { get; init; }

    public OtlpInstrumentKey GetKey() => new(Parent.MeterName, Name);
}

public class OtlpInstrumentData
{
    public required OtlpInstrumentSummary Summary { get; init; }
    public required List<DimensionScope> Dimensions { get; init; }
    public required Dictionary<string, List<string>> KnownAttributeValues { get; init; }
}

[DebuggerDisplay("Name = {Summary.Name}, Unit = {Summary.Unit}, Type = {Summary.Type}")]
public class OtlpInstrument
{
    public required OtlpInstrumentSummary Summary { get; init; }
    public required TelemetryLimitOptions Options { get; init; }

    public Dictionary<ReadOnlyMemory<KeyValuePair<string, string>>, DimensionScope> Dimensions { get; } = new(ScopeAttributesComparer.Instance);
    public Dictionary<string, List<string>> KnownAttributeValues { get; } = new();

    public void AddMetrics(Metric metric, ref KeyValuePair<string, string>[]? tempAttributes)
    {
        switch (metric.DataCase)
        {
            case Metric.DataOneofCase.Gauge:
                foreach (NumberDataPoint? d in metric.Gauge.DataPoints)
                {
                    FindScope(d.Attributes, ref tempAttributes).AddPointValue(d, Options);
                }
                break;

            case Metric.DataOneofCase.Sum:
                foreach (NumberDataPoint? d in metric.Sum.DataPoints)
                {
                    FindScope(d.Attributes, ref tempAttributes).AddPointValue(d, Options);
                }
                break;

            case Metric.DataOneofCase.Histogram:
                foreach (HistogramDataPoint? d in metric.Histogram.DataPoints)
                {
                    FindScope(d.Attributes, ref tempAttributes).AddHistogramValue(d, Options);
                }
                break;
        }
    }

    private DimensionScope FindScope(RepeatedField<KeyValue> attributes, ref KeyValuePair<string, string>[]? tempAttributes)
    {
        // We want to find the dimension scope that matches the attributes, but we don't want to allocate.
        // Copy values to a temporary reusable array.
        //
        // A meter can have attributes. Merge these with the data point attributes when creating a dimension.
        OtlpHelpers.CopyKeyValuePairs(attributes, Summary.Parent.Attributes, Options, out int copyCount, ref tempAttributes);
        Array.Sort(tempAttributes, 0, copyCount, KeyValuePairComparer.Instance);

        Memory<KeyValuePair<string, string>> comparableAttributes = tempAttributes.AsMemory(0, copyCount);

        if (!Dimensions.TryGetValue(comparableAttributes, out DimensionScope? dimension))
        {
            dimension = AddDimensionScope(comparableAttributes);
        }
        return dimension;
    }

    private DimensionScope AddDimensionScope(Memory<KeyValuePair<string, string>> comparableAttributes)
    {
        bool isFirst = Dimensions.Count == 0;
        KeyValuePair<string, string>[]? durableAttributes = comparableAttributes.ToArray();
        DimensionScope? dimension = new DimensionScope(Options.MaxMetricsCount, durableAttributes);
        Dimensions.Add(durableAttributes, dimension);

        IEnumerable<string>? keys = KnownAttributeValues.Keys.Union(durableAttributes.Select(a => a.Key)).Distinct();
        foreach (string? key in keys)
        {
            if (!KnownAttributeValues.TryGetValue(key, out List<string>? values))
            {
                KnownAttributeValues.Add(key, values = new List<string>());

                // If the key is new and there are already dimensions, add an empty value because there are dimensions without this key.
                if (!isFirst)
                {
                    TryAddValue(values, string.Empty);
                }
            }

            string? currentDimensionValue = OtlpHelpers.GetValue(durableAttributes, key);
            TryAddValue(values, currentDimensionValue ?? string.Empty);
        }

        return dimension;

        static void TryAddValue(List<string> values, string value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }
    }

    public static OtlpInstrument Clone(OtlpInstrument instrument, bool cloneData, DateTime? valuesStart, DateTime? valuesEnd)
    {
        OtlpInstrument? newInstrument = new OtlpInstrument
        {
            Summary = instrument.Summary,
            Options = instrument.Options,
        };

        if (cloneData)
        {
            foreach (KeyValuePair<string, List<string>> item in instrument.KnownAttributeValues)
            {
                newInstrument.KnownAttributeValues.Add(item.Key, item.Value.ToList());
            }
            foreach (KeyValuePair<ReadOnlyMemory<KeyValuePair<string, string>>, DimensionScope> item in instrument.Dimensions)
            {
                newInstrument.Dimensions.Add(item.Key, DimensionScope.Clone(item.Value, valuesStart, valuesEnd));
            }
        }

        return newInstrument;
    }

    private sealed class ScopeAttributesComparer : IEqualityComparer<ReadOnlyMemory<KeyValuePair<string, string>>>
    {
        public static readonly ScopeAttributesComparer Instance = new();

        public bool Equals(ReadOnlyMemory<KeyValuePair<string, string>> x, ReadOnlyMemory<KeyValuePair<string, string>> y)
        {
            return x.Span.SequenceEqual(y.Span);
        }

        public int GetHashCode([DisallowNull] ReadOnlyMemory<KeyValuePair<string, string>> obj)
        {
            HashCode hashcode = new HashCode();
            foreach (KeyValuePair<string, string> pair in obj.Span)
            {
                hashcode.Add(pair.Key);
                hashcode.Add(pair.Value);
            }
            return hashcode.ToHashCode();
        }
    }

    private sealed class KeyValuePairComparer : IComparer<KeyValuePair<string, string>>
    {
        public static readonly KeyValuePairComparer Instance = new();

        public int Compare(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
        {
            return string.Compare(x.Key, y.Key, StringComparison.Ordinal);
        }
    }
}