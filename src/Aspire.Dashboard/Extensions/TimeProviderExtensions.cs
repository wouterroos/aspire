// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Turbine.Dashboard.Model;

namespace Turbine.Dashboard.Extensions;

internal static class TimeProviderExtensions
{
    public static DateTime ToLocal(this BrowserTimeProvider timeProvider, DateTimeOffset utcDateTimeOffset)
    {
        DateTime dateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTimeOffset.UtcDateTime, timeProvider.LocalTimeZone);
        dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);

        return dateTime;
    }

    public static DateTimeOffset ToLocalDateTimeOffset(this BrowserTimeProvider timeProvider, DateTimeOffset utcDateTimeOffset)
    {
        return TimeZoneInfo.ConvertTime(utcDateTimeOffset, timeProvider.LocalTimeZone);
    }

    public static DateTime ToLocal(this BrowserTimeProvider timeProvider, DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Local)
        {
            return dateTime;
        }
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            throw new InvalidOperationException("Unable to convert unspecified DateTime to local time.");
        }

        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeProvider.LocalTimeZone);
        local = DateTime.SpecifyKind(local, DateTimeKind.Local);

        return local;
    }
}