// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Otlp.Storage;

namespace Turbine.Dashboard.Model;

[DebuggerDisplay("Type = {Type}, InstanceId = {InstanceId}, ReplicaSetName = {ReplicaSetName}")]
public class ResourceTypeDetails
{
    private ResourceTypeDetails(OtlpApplicationType type, string? instanceId, string? replicaSetName)
    {
        Type = type;
        InstanceId = instanceId;
        ReplicaSetName = replicaSetName;
    }

    public OtlpApplicationType Type { get; }
    public string? InstanceId { get; }
    public string? ReplicaSetName { get; }

    public ApplicationKey GetApplicationKey()
    {
        if (ReplicaSetName == null)
        {
            throw new InvalidOperationException($"Can't get ApplicationKey from resource type details '{ToString()}' because {nameof(ReplicaSetName)} is null.");
        }

        return new ApplicationKey(ReplicaSetName, InstanceId);
    }

    public static ResourceTypeDetails CreateApplicationGrouping(string groupingName, bool isReplicaSet)
    {
        return new ResourceTypeDetails(OtlpApplicationType.ResourceGrouping, instanceId: null, replicaSetName: isReplicaSet ? groupingName : null);
    }

    public static ResourceTypeDetails CreateSingleton(string instanceId, string replicaSetName)
    {
        return new ResourceTypeDetails(OtlpApplicationType.Singleton, instanceId, replicaSetName: replicaSetName);
    }

    public static ResourceTypeDetails CreateReplicaInstance(string instanceId, string replicaSetName)
    {
        return new ResourceTypeDetails(OtlpApplicationType.Instance, instanceId, replicaSetName);
    }

    public override string ToString()
    {
        return $"Type = {Type}, InstanceId = {InstanceId}, ReplicaSetName = {ReplicaSetName}";
    }
}