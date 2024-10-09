// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Turbine.Dashboard.Model;

public enum KnownResourceState
{
    Finished,
    Exited,
    FailedToStart,
    Starting,
    Running,
    Building,
    Hidden
}
