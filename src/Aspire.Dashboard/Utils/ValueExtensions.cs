// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Google.Protobuf.WellKnownTypes;

namespace Turbine.Dashboard.Utils;

internal static class ValueExtensions
{
    public static bool TryConvertToInt(this Value value, out int i)
    {
        if (value.HasStringValue && int.TryParse(value.StringValue, CultureInfo.InvariantCulture, out i))
        {
            return true;
        }
        else if (value.HasNumberValue)
        {
            // gRPC doesn't have an integer type, only 'number', which is modelled as 'double'
            // in the .NET API. Here we round to get an integer from that double. If we were just
            // to cast, a number like 3.999 would be converted to 3 rather than 4.
            i = (int)Math.Round(value.NumberValue);
            return true;
        }

        i = 0;
        return false;
    }

    public static bool TryConvertToString(this Value value, [NotNullWhen(returnValue: true)] out string? s)
    {
        if (value.HasStringValue)
        {
            s = value.StringValue;
            return true;
        }

        s = null;
        return false;
    }
}