// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Turbine.Dashboard.Extensions;

namespace Turbine.Dashboard.Utils;

public static class VersionHelpers
{
    public static string? DashboardDisplayVersion { get; } = typeof(VersionHelpers).Assembly.GetDisplayVersion();
}
