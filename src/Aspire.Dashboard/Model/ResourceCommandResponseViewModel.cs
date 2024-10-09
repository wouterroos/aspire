// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Turbine.Dashboard.Model;

public class ResourceCommandResponseViewModel
{
    public required ResourceCommandResponseKind Kind { get; init; }
    public string? ErrorMessage { get; init; }
}

// Must be kept in sync with ResourceCommandResponseKind in the resource_service.proto file
public enum ResourceCommandResponseKind
{
    Undefined = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3
}
