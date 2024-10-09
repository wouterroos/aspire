// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Turbine.Dashboard.Otlp.Storage;

public sealed class PagedResult<T>
{
    public static readonly PagedResult<T> Empty = new()
    {
        TotalItemCount = 0,
        Items = new List<T>()
    };

    public required int TotalItemCount { get; init; }
    public required List<T> Items { get; init; }
}