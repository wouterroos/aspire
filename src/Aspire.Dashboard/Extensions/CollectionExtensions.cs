// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Linq;

namespace Turbine.Dashboard.Extensions;

public static class CollectionExtensions
{
    public static bool Equivalent<T>(this T[] array, T[] other)
    {
        if (array.Length != other.Length)
        {
            return false;
        }

        return !array.Where((t, i) => !Equals(t, other[i])).Any();
    }
}