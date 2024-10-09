// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Turbine.Dashboard.Model.Otlp;

[DebuggerDisplay(@"Name = {Name}, Id = \{{Id}\}")]
public class SelectViewModel<T>
{
    public required string Name { get; init; }
    public required T? Id { get; init; }

    public override string ToString()
    {
        return $"Name = {Name}, Id = {{{Id}}}";
    }
}
