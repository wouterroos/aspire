// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aspire;
using Aspire.Dashboard.Otlp.Storage;
using Turbine.Dashboard.Configuration;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Model.MetricValues;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Turbine.Dashboard.Model.Otlp;
using static OpenTelemetry.Proto.Trace.V1.Span.Types;

namespace Turbine.Dashboard.Otlp.Storage;

public sealed class TelemetryRepository
{
    private readonly object _lock = new();
    internal readonly ILogger _logger;
    internal TimeSpan _subscriptionMinExecuteInterval = TimeSpan.FromMilliseconds(100);

    private readonly List<Subscription> _applicationSubscriptions = new();
    private readonly List<Subscription> _logSubscriptions = new();
    private readonly List<Subscription> _metricsSubscriptions = new();
    private readonly List<Subscription> _tracesSubscriptions = new();

    private readonly ConcurrentDictionary<ApplicationKey, OtlpApplication> _applications = new();

    private readonly ReaderWriterLockSlim _logsLock = new();
    private readonly Dictionary<string, OtlpScope> _logScopes = new();
    private readonly CircularBuffer<OtlpLogEntry> _logs;
    private readonly HashSet<(OtlpApplication Application, string PropertyKey)> _logPropertyKeys = new();
    private readonly Dictionary<OtlpApplication, int> _applicationUnviewedErrorLogs = new();

    private readonly ReaderWriterLockSlim _tracesLock = new();
    private readonly Dictionary<string, OtlpScope> _traceScopes = new();
    private readonly CircularBuffer<OtlpTrace> _traces;
    private readonly List<OtlpSpanLink> _spanLinks = new();
    private readonly DashboardOptions _dashboardOptions;

    public bool HasDisplayedMaxLogLimitMessage { get; set; }
    public bool HasDisplayedMaxTraceLimitMessage { get; set; }

    // For testing.
    internal List<OtlpSpanLink> SpanLinks => _spanLinks;

    public TelemetryRepository(ILoggerFactory loggerFactory, IOptions<DashboardOptions> dashboardOptions)
    {
        _logger = loggerFactory.CreateLogger(typeof(TelemetryRepository));
        _dashboardOptions = dashboardOptions.Value;

        _logs = new(_dashboardOptions.TelemetryLimits.MaxLogCount);
        _traces = new(_dashboardOptions.TelemetryLimits.MaxTraceCount);
        _traces.ItemRemovedForCapacity += TracesItemRemovedForCapacity;
    }

    private void TracesItemRemovedForCapacity(OtlpTrace trace)
    {
        // Remove links from central collection when the span is removed.
        foreach (OtlpSpan? span in trace.Spans)
        {
            foreach (OtlpSpanLink? link in span.Links)
            {
                _spanLinks.Remove(link);
            }
        }
    }

    public List<OtlpApplication> GetApplications()
    {
        return GetApplicationsCore(name: null);
    }

    public List<OtlpApplication> GetApplicationsByName(string name)
    {
        return GetApplicationsCore(name);
    }

    private List<OtlpApplication> GetApplicationsCore(string? name)
    {
        IEnumerable<OtlpApplication> results = _applications.Values;
        if (name != null)
        {
            results = results.Where(a => string.Equals(a.ApplicationKey.Name, name, StringComparisons.ResourceName));
        }

        List<OtlpApplication>? applications = results.OrderBy(a => a.ApplicationKey).ToList();
        return applications;
    }

    public OtlpApplication? GetApplicationByCompositeName(string compositeName)
    {
        foreach (KeyValuePair<ApplicationKey, OtlpApplication> kvp in _applications)
        {
            if (kvp.Key.EqualsCompositeName(compositeName))
            {
                return kvp.Value;
            }
        }

        return null;
    }

    public OtlpApplication? GetApplication(ApplicationKey key)
    {
        if (key.InstanceId == null)
        {
            throw new InvalidOperationException($"{nameof(ApplicationKey)} must have an instance ID.");
        }

        _applications.TryGetValue(key, out OtlpApplication? application);
        return application;
    }

    public List<OtlpApplication> GetApplications(ApplicationKey key)
    {
        if (key.InstanceId == null)
        {
            return GetApplicationsByName(key.Name);
        }

        return [GetApplication(key)];
    }

    public Dictionary<OtlpApplication, int> GetApplicationUnviewedErrorLogsCount()
    {
        _logsLock.EnterReadLock();

        try
        {
            return _applicationUnviewedErrorLogs.ToDictionary();
        }
        finally
        {
            _logsLock.ExitReadLock();
        }
    }

    internal void MarkViewedErrorLogs(ApplicationKey? key)
    {
        _logsLock.EnterWriteLock();

        try
        {
            if (key == null)
            {
                // Mark all logs as viewed.
                if (_applicationUnviewedErrorLogs.Count > 0)
                {
                    _applicationUnviewedErrorLogs.Clear();
                    RaiseSubscriptionChanged(_logSubscriptions);
                }
                return;
            }
            List<OtlpApplication>? applications = GetApplications(key.Value);
            foreach (OtlpApplication? application in applications)
            {
                // Mark one application logs as viewed.
                if (_applicationUnviewedErrorLogs.Remove(application))
                {
                    RaiseSubscriptionChanged(_logSubscriptions);
                }
            }
        }
        finally
        {
            _logsLock.ExitWriteLock();
        }
    }

    public OtlpApplication GetOrAddApplication(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        ApplicationKey key = resource.GetApplicationKey();

        // Fast path.
        if (_applications.TryGetValue(key, out OtlpApplication? application))
        {
            return application;
        }

        // Slower get or add path.
        (application, bool isNew) = GetOrAddApplication(key, resource);
        if (isNew)
        {
            RaiseSubscriptionChanged(_applicationSubscriptions);
        }

        return application;

        (OtlpApplication, bool) GetOrAddApplication(ApplicationKey key, Resource resource)
        {
            // This GetOrAdd allocates a closure, so we avoid it if possible.
            bool newApplication = false;
            OtlpApplication? application = _applications.GetOrAdd(key, _ =>
            {
                newApplication = true;
                return new OtlpApplication(key.Name, key.InstanceId!, resource, _logger, _dashboardOptions.TelemetryLimits);
            });
            return (application, newApplication);
        }
    }

    public Subscription OnNewApplications(Func<Task> callback)
    {
        return AddSubscription(nameof(OnNewApplications), null, SubscriptionType.Read, callback, _applicationSubscriptions);
    }

    public Subscription OnNewLogs(ApplicationKey? applicationKey, SubscriptionType subscriptionType, Func<Task> callback)
    {
        return AddSubscription(nameof(OnNewLogs), applicationKey, subscriptionType, callback, _logSubscriptions);
    }

    public Subscription OnNewMetrics(ApplicationKey? applicationKey, SubscriptionType subscriptionType, Func<Task> callback)
    {
        return AddSubscription(nameof(OnNewMetrics), applicationKey, subscriptionType, callback, _metricsSubscriptions);
    }

    public Subscription OnNewTraces(ApplicationKey? applicationKey, SubscriptionType subscriptionType, Func<Task> callback)
    {
        return AddSubscription(nameof(OnNewTraces), applicationKey, subscriptionType, callback, _tracesSubscriptions);
    }

    private Subscription AddSubscription(string name, ApplicationKey? applicationKey, SubscriptionType subscriptionType, Func<Task> callback, List<Subscription> subscriptions)
    {
        Subscription? subscription = null;
        subscription = new Subscription(name, applicationKey, subscriptionType, callback, () =>
        {
            lock (_lock)
            {
                subscriptions.Remove(subscription!);
            }
        }, ExecutionContext.Capture(), this);

        lock (_lock)
        {
            subscriptions.Add(subscription);
        }

        return subscription;
    }

    private void RaiseSubscriptionChanged(List<Subscription> subscriptions)
    {
        lock (_lock)
        {
            foreach (Subscription? subscription in subscriptions)
            {
                subscription.Execute();
            }
        }
    }

    public void AddLogs(AddContext context, RepeatedField<ResourceLogs> resourceLogs)
    {
        foreach (ResourceLogs? rl in resourceLogs)
        {
            OtlpApplication application;
            try
            {
                application = GetOrAddApplication(rl.Resource);
            }
            catch (Exception ex)
            {
                context.FailureCount += rl.ScopeLogs.Count;
                _logger.LogInformation(ex, "Error adding application.");
                continue;
            }

            AddLogsCore(context, application, rl.ScopeLogs);
        }

        RaiseSubscriptionChanged(_logSubscriptions);
    }

    public void AddLogsCore(AddContext context, OtlpApplication application, RepeatedField<ScopeLogs> scopeLogs)
    {
        _logsLock.EnterWriteLock();

        try
        {
            foreach (ScopeLogs? sl in scopeLogs)
            {
                OtlpScope? scope;
                try
                {
                    // The instrumentation scope information for the spans in this message.
                    // Semantically when InstrumentationScope isn't set, it is equivalent with
                    // an empty instrumentation scope name (unknown).
                    string? name = sl.Scope?.Name ?? string.Empty;
                    if (!_logScopes.TryGetValue(name, out scope))
                    {
                        scope = (sl.Scope != null) ? new OtlpScope(sl.Scope, _dashboardOptions.TelemetryLimits) : OtlpScope.Empty;
                        _logScopes.Add(name, scope);
                    }
                }
                catch (Exception ex)
                {
                    context.FailureCount += sl.LogRecords.Count;
                    _logger.LogInformation(ex, "Error adding scope.");
                    continue;
                }

                foreach (LogRecord? record in sl.LogRecords)
                {
                    try
                    {
                        OtlpLogEntry? logEntry = new OtlpLogEntry(record, application, scope, _dashboardOptions.TelemetryLimits);

                        // Insert log entry in the correct position based on timestamp.
                        // Logs can be added out of order by different services.
                        bool added = false;
                        for (int i = _logs.Count - 1; i >= 0; i--)
                        {
                            if (logEntry.TimeStamp > _logs[i].TimeStamp)
                            {
                                _logs.Insert(i + 1, logEntry);
                                added = true;
                                break;
                            }
                        }
                        if (!added)
                        {
                            _logs.Insert(0, logEntry);
                        }

                        // For log entries error and above, increment the unviewed count if there are no read log subscriptions for the application.
                        // We don't increment the count if there are active read subscriptions because the count will be quickly decremented when the subscription callback is run.
                        // Notifying the user there are errors and then immediately clearing the notification is confusing.
                        if (logEntry.Severity >= LogLevel.Error)
                        {
                            if (!_logSubscriptions.Any(s => s.SubscriptionType == SubscriptionType.Read && (s.ApplicationKey == application.ApplicationKey || s.ApplicationKey == null)))
                            {
                                if (_applicationUnviewedErrorLogs.TryGetValue(application, out int count))
                                {
                                    _applicationUnviewedErrorLogs[application] = ++count;
                                }
                                else
                                {
                                    _applicationUnviewedErrorLogs.Add(application, 1);
                                }
                            }
                        }

                        foreach (KeyValuePair<string, string> kvp in logEntry.Attributes)
                        {
                            _logPropertyKeys.Add((application, kvp.Key));
                        }
                    }
                    catch (Exception ex)
                    {
                        context.FailureCount++;
                        _logger.LogInformation(ex, "Error adding log entry.");
                    }
                }
            }
        }
        finally
        {
            _logsLock.ExitWriteLock();
        }
    }

    public PagedResult<OtlpLogEntry> GetLogs(GetLogsContext context)
    {
        List<OtlpApplication>? applications = null;
        if (context.ApplicationKey is { } key)
        {
            applications = GetApplications(key);

            if (applications.Count == 0)
            {
                return PagedResult<OtlpLogEntry>.Empty;
            }
        }

        _logsLock.EnterReadLock();

        try
        {
            IEnumerable<OtlpLogEntry>? results = _logs.AsEnumerable();
            if (applications?.Count > 0)
            {
                results = results.Where(l => MatchApplications(l.Application, applications));
            }

            foreach (LogFilter? filter in context.Filters)
            {
                results = filter.Apply(results);
            }

            return OtlpHelpers.GetItems(results, context.StartIndex, context.Count);
        }
        finally
        {
            _logsLock.ExitReadLock();
        }
    }

    private static bool MatchApplications(OtlpApplication application, List<OtlpApplication> applications)
    {
        for (int i = 0; i < applications.Count; i++)
        {
            if (application == applications[i])
            {
                return true;
            }
        }
        return false;
    }

    public List<string> GetLogPropertyKeys(ApplicationKey? applicationKey)
    {
        List<OtlpApplication>? applications = null;
        if (applicationKey != null)
        {
            applications = GetApplications(applicationKey.Value);
        }

        _logsLock.EnterReadLock();

        try
        {
            IEnumerable<(OtlpApplication Application, string PropertyKey)>? applicationKeys = _logPropertyKeys.AsEnumerable();
            if (applications?.Count > 0)
            {
                applicationKeys = applicationKeys.Where(keys => MatchApplications(keys.Application, applications));
            }

            IEnumerable<string>? keys = applicationKeys.Select(keys => keys.PropertyKey).Distinct();
            return keys.OrderBy(k => k).ToList();
        }
        finally
        {
            _logsLock.ExitReadLock();
        }
    }

    public GetTracesResponse GetTraces(GetTracesRequest context)
    {
        List<OtlpApplication>? applications = null;
        if (context.ApplicationKey is { } key)
        {
            applications = GetApplications(key);

            if (applications.Count == 0)
            {
                return new GetTracesResponse
                {
                    PagedResult = PagedResult<OtlpTrace>.Empty,
                    MaxDuration = TimeSpan.Zero
                };
            }
        }

        _tracesLock.EnterReadLock();

        try
        {
            IEnumerable<OtlpTrace>? results = _traces.AsEnumerable();
            if (applications?.Count > 0)
            {
                results = results.Where(t =>
                {
                    for (int i = 0; i < applications.Count; i++)
                    {
                        if (HasApplication(t, applications[i].ApplicationKey))
                        {
                            return true;
                        }
                    }
                    return false;
                });
            }
            if (!string.IsNullOrWhiteSpace(context.FilterText))
            {
                results = results.Where(t => t.FullName.Contains(context.FilterText, StringComparison.OrdinalIgnoreCase));
            }

            // Traces can be modified as new spans are added. Copy traces before returning results to avoid concurrency issues.
            Func<OtlpTrace, OtlpTrace>? copyFunc = static (OtlpTrace t) => OtlpTrace.Clone(t);

            PagedResult<OtlpTrace>? pagedResults = OtlpHelpers.GetItems(results, context.StartIndex, context.Count, copyFunc);
            TimeSpan maxDuration = pagedResults.TotalItemCount > 0 ? results.Max(r => r.Duration) : default;

            return new GetTracesResponse
            {
                PagedResult = pagedResults,
                MaxDuration = maxDuration
            };
        }
        finally
        {
            _tracesLock.ExitReadLock();
        }
    }

    public OtlpTrace? GetTrace(string traceId)
    {
        _tracesLock.EnterReadLock();

        try
        {
            return GetTraceUnsynchronized(traceId);
        }
        finally
        {
            _tracesLock.ExitReadLock();
        }
    }

    private OtlpTrace? GetTraceUnsynchronized(string traceId)
    {
        Debug.Assert(_tracesLock.IsReadLockHeld || _tracesLock.IsWriteLockHeld, $"Must get lock before calling {nameof(GetTraceUnsynchronized)}.");

        try
        {
            IEnumerable<OtlpTrace>? results = _traces.Where(t => t.TraceId.StartsWith(traceId, StringComparison.Ordinal));
            OtlpTrace? trace = results.SingleOrDefault();
            return trace is not null ? OtlpTrace.Clone(trace) : null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Multiple traces found with trace id '{traceId}'.", ex);
        }
    }

    private OtlpSpan? GetSpanUnsynchronized(string traceId, string spanId)
    {
        Debug.Assert(_tracesLock.IsReadLockHeld || _tracesLock.IsWriteLockHeld, $"Must get lock before calling {nameof(GetSpanUnsynchronized)}.");

        OtlpTrace? trace = GetTraceUnsynchronized(traceId);
        if (trace != null)
        {
            foreach (OtlpSpan? span in trace.Spans)
            {
                if (span.SpanId == spanId)
                {
                    return span;
                }
            }
        }

        return null;
    }

    public OtlpSpan? GetSpan(string traceId, string spanId)
    {
        _tracesLock.EnterReadLock();

        try
        {
            return GetSpanUnsynchronized(traceId, spanId);
        }
        finally
        {
            _tracesLock.ExitReadLock();
        }
    }

    private static bool HasApplication(OtlpTrace t, ApplicationKey applicationKey)
    {
        foreach (OtlpSpan? span in t.Spans)
        {
            if (span.Source.ApplicationKey == applicationKey)
            {
                return true;
            }
        }
        return false;
    }

    public void AddMetrics(AddContext context, RepeatedField<ResourceMetrics> resourceMetrics)
    {
        foreach (ResourceMetrics? rm in resourceMetrics)
        {
            OtlpApplication application;
            try
            {
                application = GetOrAddApplication(rm.Resource);
            }
            catch (Exception ex)
            {
                context.FailureCount += rm.ScopeMetrics.Sum(s => s.Metrics.Count);
                _logger.LogInformation(ex, "Error adding application.");
                continue;
            }

            application.AddMetrics(context, rm.ScopeMetrics);
        }

        RaiseSubscriptionChanged(_metricsSubscriptions);
    }

    public void AddTraces(AddContext context, RepeatedField<ResourceSpans> resourceSpans)
    {
        foreach (ResourceSpans? rs in resourceSpans)
        {
            OtlpApplication application;
            try
            {
                application = GetOrAddApplication(rs.Resource);
            }
            catch (Exception ex)
            {
                context.FailureCount += rs.ScopeSpans.Sum(s => s.Spans.Count);
                _logger.LogInformation(ex, "Error adding application.");
                continue;
            }

            AddTracesCore(context, application, rs.ScopeSpans);
        }

        RaiseSubscriptionChanged(_tracesSubscriptions);
    }

    private static OtlpSpanStatusCode ConvertStatus(Status? status)
    {
        return status?.Code switch
        {
            Status.Types.StatusCode.Ok => OtlpSpanStatusCode.Ok,
            Status.Types.StatusCode.Error => OtlpSpanStatusCode.Error,
            Status.Types.StatusCode.Unset => OtlpSpanStatusCode.Unset,
            _ => OtlpSpanStatusCode.Unset
        };
    }

    internal static OtlpSpanKind ConvertSpanKind(SpanKind? kind)
    {
        return kind switch
        {
            // Unspecified to Internal is intentional.
            // "Implementations MAY assume SpanKind to be INTERNAL when receiving UNSPECIFIED."
            SpanKind.Unspecified => OtlpSpanKind.Internal,
            SpanKind.Internal => OtlpSpanKind.Internal,
            SpanKind.Client => OtlpSpanKind.Client,
            SpanKind.Server => OtlpSpanKind.Server,
            SpanKind.Producer => OtlpSpanKind.Producer,
            SpanKind.Consumer => OtlpSpanKind.Consumer,
            _ => OtlpSpanKind.Unspecified
        };
    }

    internal void AddTracesCore(AddContext context, OtlpApplication application, RepeatedField<ScopeSpans> scopeSpans)
    {
        _tracesLock.EnterWriteLock();

        try
        {
            foreach (ScopeSpans? scopeSpan in scopeSpans)
            {
                OtlpScope? scope;
                try
                {
                    // The instrumentation scope information for the spans in this message.
                    // Semantically when InstrumentationScope isn't set, it is equivalent with
                    // an empty instrumentation scope name (unknown).
                    string? name = scopeSpan.Scope?.Name ?? string.Empty;
                    if (!_traceScopes.TryGetValue(name, out scope))
                    {
                        scope = (scopeSpan.Scope != null) ? new OtlpScope(scopeSpan.Scope, _dashboardOptions.TelemetryLimits) : OtlpScope.Empty;
                        _traceScopes.Add(name, scope);
                    }
                }
                catch (Exception ex)
                {
                    context.FailureCount += scopeSpan.Spans.Count;
                    _logger.LogInformation(ex, "Error adding scope.");
                    continue;
                }

                OtlpTrace? lastTrace = null;

                foreach (Span? span in scopeSpan.Spans)
                {
                    try
                    {
                        OtlpTrace? trace;
                        bool newTrace = false;

                        // Fast path to check if the span is in the same trace as the last span.
                        if (lastTrace != null && span.TraceId.Span.SequenceEqual(lastTrace.Key.Span))
                        {
                            trace = lastTrace;
                        }
                        else if (!TryGetTraceById(_traces, span.TraceId.Memory, out trace))
                        {
                            trace = new OtlpTrace(span.TraceId.Memory);
                            newTrace = true;
                        }

                        OtlpSpan? newSpan = CreateSpan(application, span, trace, scope, _dashboardOptions.TelemetryLimits);
                        trace.AddSpan(newSpan);

                        // The new span might be linked to by an existing span.
                        // Check current links to see if a backlink should be created.
                        foreach (OtlpSpanLink? existingLink in _spanLinks)
                        {
                            if (existingLink.SpanId == newSpan.SpanId && existingLink.TraceId == newSpan.TraceId)
                            {
                                newSpan.BackLinks.Add(existingLink);
                            }
                        }

                        // Add links to central collection. Add backlinks to existing spans.
                        foreach (OtlpSpanLink? link in newSpan.Links)
                        {
                            _spanLinks.Add(link);

                            OtlpSpan? linkedSpan = GetSpanUnsynchronized(link.TraceId, link.SpanId);
                            linkedSpan?.BackLinks.Add(link);
                        }

                        // Traces are sorted by the start time of the first span.
                        // We need to ensure traces are in the correct order if we're:
                        // 1. Adding a new trace.
                        // 2. The first span of the trace has changed.
                        if (newTrace)
                        {
                            bool added = false;
                            for (int i = _traces.Count - 1; i >= 0; i--)
                            {
                                OtlpTrace? currentTrace = _traces[i];
                                if (trace.FirstSpan.StartTime > currentTrace.FirstSpan.StartTime)
                                {
                                    _traces.Insert(i + 1, trace);
                                    added = true;
                                    break;
                                }
                            }
                            if (!added)
                            {
                                _traces.Insert(0, trace);
                            }
                        }
                        else
                        {
                            if (trace.FirstSpan == newSpan)
                            {
                                bool moved = false;
                                int index = _traces.IndexOf(trace);

                                for (int i = index - 1; i >= 0; i--)
                                {
                                    OtlpTrace? currentTrace = _traces[i];
                                    if (trace.FirstSpan.StartTime > currentTrace.FirstSpan.StartTime)
                                    {
                                        int insertPosition = i + 1;
                                        if (index != insertPosition)
                                        {
                                            _traces.RemoveAt(index);
                                            _traces.Insert(insertPosition, trace);
                                        }
                                        moved = true;
                                        break;
                                    }
                                }
                                if (!moved)
                                {
                                    if (index != 0)
                                    {
                                        _traces.RemoveAt(index);
                                        _traces.Insert(0, trace);
                                    }
                                }
                            }
                        }

                        lastTrace = trace;
                    }
                    catch (Exception ex)
                    {
                        context.FailureCount++;
                        _logger.LogInformation(ex, "Error adding span.");
                    }

                    AssertTraceOrder();
                    AssertSpanLinks();
                }
            }
        }
        finally
        {
            _tracesLock.ExitWriteLock();
        }

        static bool TryGetTraceById(CircularBuffer<OtlpTrace> traces, ReadOnlyMemory<byte> traceId, [NotNullWhen(true)] out OtlpTrace? trace)
        {
            ReadOnlySpan<byte> s = traceId.Span;
            for (int i = traces.Count - 1; i >= 0; i--)
            {
                if (traces[i].Key.Span.SequenceEqual(s))
                {
                    trace = traces[i];
                    return true;
                }
            }

            trace = null;
            return false;
        }
    }

    [Conditional("DEBUG")]
    private void AssertTraceOrder()
    {
        DateTime current = default;
        for (int i = 0; i < _traces.Count; i++)
        {
            OtlpTrace? trace = _traces[i];
            if (trace.FirstSpan.StartTime < current)
            {
                throw new InvalidOperationException($"Traces not in order at index {i}.");
            }

            current = trace.FirstSpan.StartTime;
        }
    }

    [Conditional("DEBUG")]
    private void AssertSpanLinks()
    {
        // Create a local copy of span links.
        List<OtlpSpanLink>? currentSpanLinks = _spanLinks.ToList();

        // Remove span links that match span links on spans.
        // Throw an error if an expected span link doesn't exist.
        foreach (OtlpTrace? trace in _traces)
        {
            foreach (OtlpSpan? span in trace.Spans)
            {
                foreach (OtlpSpanLink? link in span.Links)
                {
                    if (!currentSpanLinks.Remove(link))
                    {
                        throw new InvalidOperationException($"Couldn't find expected link from span {span.SpanId} to span {link.SpanId}.");
                    }
                }
            }
        }

        // Throw error if there are orphaned span links.
        if (currentSpanLinks.Count > 0)
        {
            StringBuilder? sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"There are {currentSpanLinks.Count} orphaned span links.");
            foreach (OtlpSpanLink? link in currentSpanLinks)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"\tSource span ID: {link.SourceSpanId}, Target span ID: {link.SpanId}");
            }

            throw new InvalidOperationException(sb.ToString());
        }
    }

    private static OtlpSpan CreateSpan(OtlpApplication application, Span span, OtlpTrace trace, OtlpScope scope, TelemetryLimitOptions options)
    {
        string? id = span.SpanId?.ToHexString();
        if (id is null)
        {
            throw new ArgumentException("Span has no SpanId");
        }

        List<OtlpSpanEvent>? events = new List<OtlpSpanEvent>();
        foreach (Event? e in span.Events.OrderBy(e => e.TimeUnixNano))
        {
            events.Add(new OtlpSpanEvent
            {
                Name = e.Name,
                Time = OtlpHelpers.UnixNanoSecondsToDateTime(e.TimeUnixNano),
                Attributes = e.Attributes.ToKeyValuePairs(options)
            });

            if (events.Count >= options.MaxSpanEventCount)
            {
                break;
            }
        }

        List<OtlpSpanLink>? links = new List<OtlpSpanLink>();
        foreach (Link? e in span.Links)
        {
            links.Add(new OtlpSpanLink
            {
                SourceSpanId = id,
                SourceTraceId = trace.TraceId,
                TraceState = e.TraceState,
                SpanId = e.SpanId.ToHexString(),
                TraceId = e.TraceId.ToHexString(),
                Attributes = e.Attributes.ToKeyValuePairs(options)
            });
        }

        OtlpSpan? newSpan = new OtlpSpan(application, trace, scope)
        {
            SpanId = id,
            ParentSpanId = span.ParentSpanId?.ToHexString(),
            Name = span.Name,
            Kind = ConvertSpanKind(span.Kind),
            StartTime = OtlpHelpers.UnixNanoSecondsToDateTime(span.StartTimeUnixNano),
            EndTime = OtlpHelpers.UnixNanoSecondsToDateTime(span.EndTimeUnixNano),
            Status = ConvertStatus(span.Status),
            StatusMessage = span.Status?.Message,
            Attributes = span.Attributes.ToKeyValuePairs(options),
            State = span.TraceState,
            Events = events,
            Links = links,
            BackLinks = new()
        };
        return newSpan;
    }

    public List<OtlpInstrumentSummary> GetInstrumentsSummaries(ApplicationKey key)
    {
        List<OtlpApplication>? applications = GetApplications(key);
        if (applications.Count == 0)
        {
            return new List<OtlpInstrumentSummary>();
        }
        else if (applications.Count == 1)
        {
            return applications[0].GetInstrumentsSummary();
        }
        else
        {
            List<OtlpInstrumentSummary>? allApplicationSummaries = applications
                .SelectMany(a => a.GetInstrumentsSummary())
                .DistinctBy(s => s.GetKey())
                .ToList();

            return allApplicationSummaries;
        }
    }

    public OtlpInstrumentData? GetInstrument(GetInstrumentRequest request)
    {
        List<OtlpApplication>? applications = GetApplications(request.ApplicationKey);
        List<OtlpInstrument>? instruments = applications
            .Select(a => a.GetInstrument(request.MeterName, request.InstrumentName, request.StartTime, request.EndTime))
            .OfType<OtlpInstrument>()
            .ToList();

        if (instruments.Count == 0)
        {
            return null;
        }
        else if (instruments.Count == 1)
        {
            OtlpInstrument? instrument = instruments[0];
            return new OtlpInstrumentData
            {
                Summary = instrument.Summary,
                KnownAttributeValues = instrument.KnownAttributeValues,
                Dimensions = instrument.Dimensions.Values.ToList()
            };
        }
        else
        {
            List<DimensionScope>? allDimensions = new List<DimensionScope>();
            Dictionary<string, List<string>>? allKnownAttributes = new Dictionary<string, List<string>>();

            foreach (OtlpInstrument? instrument in instruments)
            {
                allDimensions.AddRange(instrument.Dimensions.Values);

                foreach (KeyValuePair<string, List<string>> knownAttributeValues in instrument.KnownAttributeValues)
                {
                    if (allKnownAttributes.TryGetValue(knownAttributeValues.Key, out List<string>? values))
                    {
                        allKnownAttributes[knownAttributeValues.Key] = values.Union(knownAttributeValues.Value).ToList();
                    }
                    else
                    {
                        allKnownAttributes[knownAttributeValues.Key] = knownAttributeValues.Value.ToList();
                    }
                }
            }

            return new OtlpInstrumentData
            {
                Summary = instruments[0].Summary,
                Dimensions = allDimensions,
                KnownAttributeValues = allKnownAttributes
            };
        }
    }
}