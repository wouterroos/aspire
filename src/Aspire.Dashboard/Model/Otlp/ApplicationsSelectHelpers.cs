// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Aspire;
using Microsoft.Extensions.Logging;
using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Model.Otlp;

public static class ApplicationsSelectHelpers
{
    public static SelectViewModel<ResourceTypeDetails> GetApplication(this List<SelectViewModel<ResourceTypeDetails>> applications, ILogger logger, string? name, bool canSelectGrouping, SelectViewModel<ResourceTypeDetails> fallback)
    {
        if (name is null)
        {
            return fallback;
        }

        List<SelectViewModel<ResourceTypeDetails>>? matches = applications.Where(e => SupportType(e.Id?.Type, canSelectGrouping) && string.Equals(name, e.Name, StringComparisons.ResourceName)).ToList();
        if (matches.Count == 1)
        {
            return matches[0];
        }
        else if (matches.Count == 0)
        {
            return fallback;
        }
        else
        {
            // There are multiple matches. Log as much information as possible about applications.
            logger.LogWarning(
                """
                Multiple matches found when getting application '{Name}'.
                Available applications:
                {AvailableApplications}
                Matched applications:
                {MatchedApplications}
                """, name, string.Join(Environment.NewLine, applications), string.Join(Environment.NewLine, matches));

            // Return first match to not break app. Make the UI resilient to unexpectedly bad data.
            return matches[0];
        }
    }

    public static List<SelectViewModel<ResourceTypeDetails>> CreateApplications(List<OtlpApplication> applications)
    {
        Dictionary<string, List<OtlpApplication>>? replicasByApplicationName = OtlpApplication.GetReplicasByApplicationName(applications);

        List<SelectViewModel<ResourceTypeDetails>>? selectViewModels = new List<SelectViewModel<ResourceTypeDetails>>();

        foreach ((string? applicationName, List<OtlpApplication>? replicas) in replicasByApplicationName)
        {
            if (replicas.Count == 1)
            {
                // not replicated
                OtlpApplication? app = replicas.Single();
                selectViewModels.Add(new SelectViewModel<ResourceTypeDetails>
                {
                    Id = ResourceTypeDetails.CreateSingleton(app.InstanceId, applicationName),
                    Name = applicationName
                });

                continue;
            }

            // add a disabled "Resource" as a header
            selectViewModels.Add(new SelectViewModel<ResourceTypeDetails>
            {
                Id = ResourceTypeDetails.CreateApplicationGrouping(applicationName, isReplicaSet: true),
                Name = applicationName
            });

            // add each individual replica
            selectViewModels.AddRange(replicas.Select(replica =>
                new SelectViewModel<ResourceTypeDetails>
                {
                    Id = ResourceTypeDetails.CreateReplicaInstance(replica.InstanceId, applicationName),
                    Name = OtlpApplication.GetResourceName(replica, applications)
                }));
        }

        List<SelectViewModel<ResourceTypeDetails>>? sortedVMs = selectViewModels.OrderBy(vm => vm.Name, StringComparers.ResourceName).ToList();
        return sortedVMs;
    }

    private static bool SupportType(OtlpApplicationType? type, bool canSelectGrouping)
    {
        if (type is OtlpApplicationType.Instance or OtlpApplicationType.Singleton)
        {
            return true;
        }

        if (canSelectGrouping && type is OtlpApplicationType.ResourceGrouping)
        {
            return true;
        }

        return false;
    }
}