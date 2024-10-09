// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Turbine.Dashboard.Model;

namespace Turbine.Dashboard.Extensions;

internal static class ResourceViewModelExtensions
{
    public static bool IsHiddenState(this ResourceViewModel resource)
    {
        return resource.KnownState == KnownResourceState.Hidden;
    }

    public static bool IsRunningState(this ResourceViewModel resource)
    {
        return resource.KnownState == KnownResourceState.Running;
    }

    public static bool IsFinishedState(this ResourceViewModel resource)
    {
        return resource.KnownState is KnownResourceState.Finished;
    }

    public static bool IsStopped(this ResourceViewModel resource)
    {
        return resource.KnownState is KnownResourceState.Exited or KnownResourceState.Finished or KnownResourceState.FailedToStart;
    }

    public static bool IsStartingOrBuilding(this ResourceViewModel resource)
    {
        return resource.KnownState is KnownResourceState.Starting or KnownResourceState.Building;
    }

    public static bool HasNoState(this ResourceViewModel resource) => string.IsNullOrEmpty(resource.State);
}
