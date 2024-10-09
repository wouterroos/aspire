// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Storage;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Turbine.Dashboard.Otlp;

public sealed class OtlpTraceService
{
    private readonly ILogger<OtlpTraceService> _logger;
    private readonly TelemetryRepository _telemetryRepository;

    public OtlpTraceService(ILogger<OtlpTraceService> logger, TelemetryRepository telemetryRepository)
    {
        _logger = logger;
        _telemetryRepository = telemetryRepository;
    }

    public ExportTraceServiceResponse Export(ExportTraceServiceRequest request)
    {
        AddContext? addContext = new AddContext();
        _telemetryRepository.AddTraces(addContext, request.ResourceSpans);

        _logger.LogDebug("Processed trace export. Failure count: {FailureCount}", addContext.FailureCount);

        return new ExportTraceServiceResponse
        {
            PartialSuccess = new ExportTracePartialSuccess
            {
                RejectedSpans = addContext.FailureCount
            }
        };
    }
}