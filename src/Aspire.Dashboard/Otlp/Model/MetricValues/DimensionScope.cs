// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Turbine.Dashboard.Configuration;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Metrics.V1;
using Aspire.Dashboard.Otlp.Storage;

namespace Turbine.Dashboard.Otlp.Model.MetricValues;

[DebuggerDisplay("Name = {Name}, Values = {Values.Count}")]
public class DimensionScope
{
    public const string NoDimensions = "no-dimensions";
    public string Name { get; init; }
    public KeyValuePair<string, string>[] Attributes { get; init; }
    public IList<MetricValueBase> Values => _values;

    private readonly CircularBuffer<MetricValueBase> _values;

    // Used to aid in merging values that are the same in a concurrent environment
    private MetricValueBase? _lastValue;

    public int Capacity => _values.Capacity;

    public DimensionScope(int capacity, KeyValuePair<string, string>[] attributes)
    {
        Attributes = attributes;
        string? name = Attributes.ConcatProperties();
        Name = name != null && name.Length > 0 ? name : NoDimensions;
        _values = new(capacity);
    }

    public void AddPointValue(NumberDataPoint d, TelemetryLimitOptions options)
    {
        DateTime start = OtlpHelpers.UnixNanoSecondsToDateTime(d.StartTimeUnixNano);
        DateTime end = OtlpHelpers.UnixNanoSecondsToDateTime(d.TimeUnixNano);

        if (d.ValueCase == NumberDataPoint.ValueOneofCase.AsInt)
        {
            long value = d.AsInt;
            MetricValue<long>? lastLongValue = _lastValue as MetricValue<long>;
            if (lastLongValue is not null && lastLongValue.Value == value)
            {
                lastLongValue.End = end;
                AddExemplars(lastLongValue, d.Exemplars, options);
                Interlocked.Increment(ref lastLongValue.Count);
            }
            else
            {
                if (lastLongValue is not null)
                {
                    start = lastLongValue.End;
                }
                _lastValue = new MetricValue<long>(d.AsInt, start, end);
                AddExemplars(_lastValue, d.Exemplars, options);
                _values.Add(_lastValue);
            }
        }
        else if (d.ValueCase == NumberDataPoint.ValueOneofCase.AsDouble)
        {
            MetricValue<double>? lastDoubleValue = _lastValue as MetricValue<double>;
            if (lastDoubleValue is not null && lastDoubleValue.Value == d.AsDouble)
            {
                lastDoubleValue.End = end;
                AddExemplars(lastDoubleValue, d.Exemplars, options);
                Interlocked.Increment(ref lastDoubleValue.Count);
            }
            else
            {
                if (lastDoubleValue is not null)
                {
                    start = lastDoubleValue.End;
                }
                _lastValue = new MetricValue<double>(d.AsDouble, start, end);
                AddExemplars(_lastValue, d.Exemplars, options);
                _values.Add(_lastValue);
            }
        }
    }

    public void AddHistogramValue(HistogramDataPoint h, TelemetryLimitOptions options)
    {
        DateTime start = OtlpHelpers.UnixNanoSecondsToDateTime(h.StartTimeUnixNano);
        DateTime end = OtlpHelpers.UnixNanoSecondsToDateTime(h.TimeUnixNano);

        HistogramValue? lastHistogramValue = _lastValue as HistogramValue;
        if (lastHistogramValue is not null && lastHistogramValue.Count == h.Count)
        {
            lastHistogramValue.End = end;
            AddExemplars(lastHistogramValue, h.Exemplars, options);
        }
        else
        {
            // If the explicit bounds are the same as the last value, reuse them.
            double[] explicitBounds;
            if (lastHistogramValue is not null)
            {
                start = lastHistogramValue.End;
                explicitBounds = lastHistogramValue.ExplicitBounds.SequenceEqual(h.ExplicitBounds)
                    ? lastHistogramValue.ExplicitBounds
                    : h.ExplicitBounds.ToArray();
            }
            else
            {
                explicitBounds = h.ExplicitBounds.ToArray();
            }
            _lastValue = new HistogramValue(h.BucketCounts.ToArray(), h.Sum, h.Count, start, end, explicitBounds);
            AddExemplars(_lastValue, h.Exemplars, options);
            _values.Add(_lastValue);
        }
    }

    private static void AddExemplars(MetricValueBase value, RepeatedField<Exemplar> exemplars, TelemetryLimitOptions options)
    {
        if (exemplars.Count > 0)
        {
            foreach (Exemplar? exemplar in exemplars)
            {
                // Can't do anything useful with exemplars without a linked trace. Filter them out.
                if (exemplar.TraceId == null || exemplar.SpanId == null)
                {
                    continue;
                }

                DateTime start = OtlpHelpers.UnixNanoSecondsToDateTime(exemplar.TimeUnixNano);
                double exemplarValue = exemplar.HasAsDouble ? exemplar.AsDouble : exemplar.AsInt;

                bool exists = false;
                foreach (MetricsExemplar? existingExemplar in value.Exemplars)
                {
                    if (start == existingExemplar.Start && exemplarValue == existingExemplar.Value)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    continue;
                }

                value.Exemplars.Add(new MetricsExemplar
                {
                    Start = start,
                    Value = exemplarValue,
                    Attributes = exemplar.FilteredAttributes.ToKeyValuePairs(options),
                    SpanId = exemplar.SpanId.ToHexString(),
                    TraceId = exemplar.TraceId.ToHexString()
                });
            }
        }
    }

    internal static DimensionScope Clone(DimensionScope value, DateTime? valuesStart, DateTime? valuesEnd)
    {
        DimensionScope? newDimensionScope = new DimensionScope(value.Capacity, value.Attributes);
        if (valuesStart != null && valuesEnd != null)
        {
            foreach (MetricValueBase? item in value._values)
            {
                if ((item.Start <= valuesEnd.Value && item.End >= valuesStart.Value) ||
                    (item.Start >= valuesStart.Value && item.End <= valuesEnd.Value))
                {
                    newDimensionScope._values.Add(MetricValueBase.Clone(item));
                }
            }
        }

        return newDimensionScope;
    }
}