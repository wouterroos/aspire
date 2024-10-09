// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

namespace Turbine.Dashboard.Otlp.Storage;

public readonly record struct ApplicationKey(string Name, string? InstanceId) : IComparable<ApplicationKey>
{
    public int CompareTo(ApplicationKey other)
    {
        int c = string.Compare(Name, other.Name, StringComparisons.ResourceName);
        if (c != 0)
        {
            return c;
        }

        return string.Compare(InstanceId, other.InstanceId, StringComparisons.ResourceName);
    }

    public bool EqualsCompositeName(string name)
    {
        if (name == null)
        {
            return false;
        }

        if (InstanceId != null)
        {
            // Composite name has the format "{Name}-{InstanceId}".
            if (name.Length != Name.Length + InstanceId.Length + 1)
            {
                return false;
            }

            if (!name.AsSpan(0, Name.Length).Equals(Name, StringComparisons.ResourceName))
            {
                return false;
            }
            if (name[Name.Length] != '-')
            {
                return false;
            }
            if (!name.AsSpan(Name.Length + 1, InstanceId.Length).Equals(InstanceId, StringComparisons.ResourceName))
            {
                return false;
            }
        }
        else
        {
            // InstanceId is null so just match on name.
            // This is used to match all instances of an app with the matching name.
            return string.Equals(Name, name, StringComparisons.ResourceName);
        }

        return true;
    }
}