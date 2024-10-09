// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Storage;
using Turbine.Dashboard.Utils;
using Microsoft.AspNetCore.Components;

namespace Turbine.Dashboard.Components;

public partial class UnreadLogErrorsBadge
{
    private string? _applicationName;
    private int _unviewedCount;

    [Parameter, EditorRequired]
    public required ResourceViewModel Resource { get; set; }

    [Parameter, EditorRequired]
    public required Dictionary<OtlpApplication, int>? UnviewedErrorCounts { get; set; }

    [Inject]
    public required TelemetryRepository TelemetryRepository { get; init; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    protected override void OnParametersSet()
    {
        (_applicationName, _unviewedCount) = GetUnviewedErrorCount(Resource);
    }

    private (string? applicationName, int unviewedErrorCount) GetUnviewedErrorCount(ResourceViewModel resource)
    {
        if (UnviewedErrorCounts is null)
        {
            return (null, 0);
        }

        OtlpApplication? application = TelemetryRepository.GetApplicationByCompositeName(resource.Name);
        if (application is null)
        {
            return (null, 0);
        }

        if (!UnviewedErrorCounts.TryGetValue(application, out int count) || count == 0)
        {
            return (null, 0);
        }

        List<OtlpApplication>? applications = TelemetryRepository.GetApplications();
        string? applicationName = applications.Count(a => a.ApplicationName == application.ApplicationName) > 1
            ? application.InstanceId
            : application.ApplicationName;

        return (applicationName, count);
    }

    private string GetResourceErrorStructuredLogsUrl()
    {
        return DashboardUrls.StructuredLogsUrl(resource: _applicationName, logLevel: "error");
    }
}