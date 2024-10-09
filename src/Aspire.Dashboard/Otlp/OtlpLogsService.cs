// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Storage;
using OpenTelemetry.Proto.Collector.Logs.V1;

namespace Turbine.Dashboard.Otlp;

public sealed class OtlpLogsService
{
    private readonly ILogger<OtlpLogsService> _logger;
    private readonly TelemetryRepository _telemetryRepository;

    public OtlpLogsService(ILogger<OtlpLogsService> logger, TelemetryRepository telemetryRepository)
    {
        _logger = logger;
        _telemetryRepository = telemetryRepository;
    }

    public ExportLogsServiceResponse Export(ExportLogsServiceRequest request)
    {
        AddContext? addContext = new AddContext();
        _telemetryRepository.AddLogs(addContext, request.ResourceLogs);

        _logger.LogDebug("Processed logs export. Failure count: {FailureCount}", addContext.FailureCount);

        return new ExportLogsServiceResponse
        {
            PartialSuccess = new ExportLogsPartialSuccess
            {
                RejectedLogRecords = addContext.FailureCount
            }
        };
    }
}