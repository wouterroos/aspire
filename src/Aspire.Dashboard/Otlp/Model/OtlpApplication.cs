// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Turbine.Dashboard.Configuration;
using Turbine.Dashboard.Otlp.Storage;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Turbine.Dashboard.Otlp.Model;

[DebuggerDisplay("ApplicationName = {ApplicationName}, InstanceId = {InstanceId}")]
public class OtlpApplication
{
    public const string SERVICE_NAME = "service.name";
    public const string SERVICE_INSTANCE_ID = "service.instance.id";
    public const string PROCESS_EXECUTABLE_NAME = "process.executable.name";

    public string ApplicationName { get; }
    public string InstanceId { get; }

    public ApplicationKey ApplicationKey => new ApplicationKey(ApplicationName, InstanceId);

    private readonly ReaderWriterLockSlim _metricsLock = new();
    private readonly Dictionary<string, OtlpMeter> _meters = new();
    private readonly Dictionary<OtlpInstrumentKey, OtlpInstrument> _instruments = new();

    private readonly ILogger _logger;
    private readonly TelemetryLimitOptions _options;

    public KeyValuePair<string, string>[] Properties { get; }

    public OtlpApplication(string name, string instanceId, Resource resource, ILogger logger, TelemetryLimitOptions options)
    {
        List<KeyValuePair<string, string>>? properties = new List<KeyValuePair<string, string>>();
        foreach (KeyValue? attribute in resource.Attributes)
        {
            switch (attribute.Key)
            {
                case SERVICE_NAME:
                case SERVICE_INSTANCE_ID:
                    // Values passed in via ctor and set to members. Don't add to properties collection.
                    break;

                default:
                    properties.Add(new KeyValuePair<string, string>(attribute.Key, attribute.Value.GetString()));
                    break;
            }
        }
        Properties = properties.ToArray();

        ApplicationName = name;
        InstanceId = instanceId;

        _logger = logger;
        _options = options;
    }

    public Dictionary<string, string> AllProperties()
    {
        Dictionary<string, string>? props = new Dictionary<string, string>();
        props.Add(SERVICE_NAME, ApplicationName);
        props.Add(SERVICE_INSTANCE_ID, InstanceId);

        foreach (KeyValuePair<string, string> kv in Properties)
        {
            props.TryAdd(kv.Key, kv.Value);
        }

        return props;
    }

    public void AddMetrics(AddContext context, RepeatedField<ScopeMetrics> scopeMetrics)
    {
        _metricsLock.EnterWriteLock();

        try
        {
            // Temporary attributes array to use when adding metrics to the instruments.
            KeyValuePair<string, string>[]? tempAttributes = null;

            foreach (ScopeMetrics? sm in scopeMetrics)
            {
                foreach (Metric? metric in sm.Metrics)
                {
                    try
                    {
                        OtlpInstrumentKey instrumentKey = new OtlpInstrumentKey(sm.Scope.Name, metric.Name);
                        if (!_instruments.TryGetValue(instrumentKey, out OtlpInstrument? instrument))
                        {
                            _instruments.Add(instrumentKey, instrument = new OtlpInstrument
                            {
                                Summary = new OtlpInstrumentSummary
                                {
                                    Name = metric.Name,
                                    Description = metric.Description,
                                    Unit = metric.Unit,
                                    Type = MapMetricType(metric.DataCase),
                                    Parent = GetMeter(sm.Scope)
                                },
                                Options = _options
                            });
                        }

                        instrument.AddMetrics(metric, ref tempAttributes);
                    }
                    catch (Exception ex)
                    {
                        context.FailureCount++;
                        _logger.LogInformation(ex, "Error adding metric.");
                    }
                }
            }
        }
        finally
        {
            _metricsLock.ExitWriteLock();
        }
    }

    private static OtlpInstrumentType MapMetricType(Metric.DataOneofCase data)
    {
        return data switch
        {
            Metric.DataOneofCase.Gauge => OtlpInstrumentType.Gauge,
            Metric.DataOneofCase.Sum => OtlpInstrumentType.Sum,
            Metric.DataOneofCase.Histogram => OtlpInstrumentType.Histogram,
            _ => OtlpInstrumentType.Unsupported
        };
    }

    private OtlpMeter GetMeter(InstrumentationScope scope)
    {
        if (!_meters.TryGetValue(scope.Name, out OtlpMeter? meter))
        {
            _meters.Add(scope.Name, meter = new OtlpMeter(scope, _options));
        }
        return meter;
    }

    public OtlpInstrument? GetInstrument(string meterName, string instrumentName, DateTime? valuesStart, DateTime? valuesEnd)
    {
        _metricsLock.EnterReadLock();

        try
        {
            if (!_instruments.TryGetValue(new OtlpInstrumentKey(meterName, instrumentName), out OtlpInstrument? instrument))
            {
                return null;
            }

            return OtlpInstrument.Clone(instrument, cloneData: true, valuesStart: valuesStart, valuesEnd: valuesEnd);
        }
        finally
        {
            _metricsLock.ExitReadLock();
        }
    }

    public List<OtlpInstrumentSummary> GetInstrumentsSummary()
    {
        _metricsLock.EnterReadLock();

        try
        {
            List<OtlpInstrumentSummary>? instruments = new List<OtlpInstrumentSummary>(_instruments.Count);
            foreach (KeyValuePair<OtlpInstrumentKey, OtlpInstrument> instrument in _instruments)
            {
                instruments.Add(instrument.Value.Summary);
            }
            return instruments;
        }
        finally
        {
            _metricsLock.ExitReadLock();
        }
    }

    public static Dictionary<string, List<OtlpApplication>> GetReplicasByApplicationName(IEnumerable<OtlpApplication> allApplications)
    {
        return allApplications
            .GroupBy(application => application.ApplicationName, StringComparers.ResourceName)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());
    }

    public static string GetResourceName(OtlpApplication app, List<OtlpApplication> allApplications)
    {
        int count = 0;
        foreach (OtlpApplication? item in allApplications)
        {
            if (string.Equals(item.ApplicationName, app.ApplicationName, StringComparisons.ResourceName))
            {
                count++;
                if (count >= 2)
                {
                    string? instanceId = app.InstanceId;

                    // Convert long GUID into a shorter, more human friendly format.
                    // Before: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
                    // After:  aaaaaaaa
                    if (Guid.TryParse(instanceId, out Guid guid))
                    {
                        Span<char> chars = stackalloc char[32];
                        bool result = guid.TryFormat(chars, charsWritten: out _, format: "N");
                        Debug.Assert(result, "Guid.TryFormat not successful.");

                        instanceId = chars.Slice(0, 8).ToString();
                    }

                    return $"{item.ApplicationName}-{instanceId}";
                }
            }
        }

        return app.ApplicationName;
    }
}