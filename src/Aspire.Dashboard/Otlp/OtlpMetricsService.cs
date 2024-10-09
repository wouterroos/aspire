// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Storage;
using OpenTelemetry.Proto.Collector.Metrics.V1;

namespace Turbine.Dashboard.Otlp;

public sealed class OtlpMetricsService
{
    private readonly ILogger<OtlpMetricsService> _logger;
    private readonly TelemetryRepository _telemetryRepository;

    public OtlpMetricsService(ILogger<OtlpMetricsService> logger, TelemetryRepository telemetryRepository)
    {
        _logger = logger;
        _telemetryRepository = telemetryRepository;
    }

    public ExportMetricsServiceResponse Export(ExportMetricsServiceRequest request)
    {
        AddContext? addContext = new AddContext();
        _telemetryRepository.AddMetrics(addContext, request.ResourceMetrics);

        _logger.LogDebug("Processed metrics export. Failure count: {FailureCount}", addContext.FailureCount);

        return new ExportMetricsServiceResponse
        {
            PartialSuccess = new ExportMetricsPartialSuccess
            {
                RejectedDataPoints = addContext.FailureCount
            }
        };
    }
}