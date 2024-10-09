// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Turbine.Dashboard.Extensions;
using Turbine.Dashboard.Model;

namespace Turbine.Dashboard.ConsoleLogs;

public static partial class TimestampParser
{
    private static readonly Regex s_rfc3339RegEx = GenerateRfc3339RegEx();

    public static bool TryParseConsoleTimestamp(string text, [NotNullWhen(true)] out TimestampParserResult? result)
    {
        Match? match = s_rfc3339RegEx.Match(text);

        if (match.Success)
        {
            ReadOnlySpan<char> span = text.AsSpan();
            ReadOnlySpan<char> timestamp = span[match.Index..(match.Index + match.Length)];
            ReadOnlySpan<char> theRest = match.Index + match.Length >= span.Length ? "" : span[(match.Index + match.Length)..];

            result = new(theRest.ToString(), DateTimeOffset.Parse(timestamp.ToString(), CultureInfo.InvariantCulture));
            return true;
        }

        result = default;
        return false;
    }

    public static string ConvertTimestampFromUtc(BrowserTimeProvider timeProvider, ReadOnlySpan<char> timestamp)
    {
        if (DateTimeOffset.TryParse(timestamp, out DateTimeOffset dateTimeUtc))
        {
            DateTime dateTimeLocal = timeProvider.ToLocal(dateTimeUtc);
            return dateTimeLocal.ToString(KnownFormats.ConsoleLogsTimestampFormat, CultureInfo.CurrentCulture);
        }

        return timestamp.ToString();
    }

    // Regular Expression for an RFC3339 timestamp, including RFC3339Nano
    //
    // Example timestamps:
    // 2023-10-02T12:56:35.123456789Z
    // 2023-10-02T13:56:35.123456789+10:00
    // 2023-10-02T13:56:35.123456789-10:00
    // 2023-10-02T13:56:35.123456789Z10:00
    // 2023-10-02T13:56:35.123456Z
    // 2023-10-02T13:56:35Z
    //
    // Explanation:
    // ^                                                   - Starts the string
    // (?:\\d{4})                                          - Four digits for the year
    // -                                                   - Separator for the date
    // (?:0[1-9]|1[0-2])                                   - Two digits for the month, restricted to 01-12
    // -                                                   - Separator for the date
    // (?:0[1-9]|[12][0-9]|3[01])                          - Two digits for the day, restricted to 01-31
    // [T ]                                                - Literal, separator between date and time, either a T or a space
    // (?:[01][0-9]|2[0-3])                                - Two digits for the hour, restricted to 00-23
    // :                                                   - Separator for the time
    // (?:[0-5][0-9])                                      - Two digits for the minutes, restricted to 00-59
    // :                                                   - Separator for the time
    // (?:[0-5][0-9])                                      - Two digits for the seconds, restricted to 00-59
    // (?:\\.\\d{1,9})                                     - A period and up to nine digits for the partial seconds
    // Z                                                   - Literal, same as +00:00
    // (?:[Z+-](?:[01][0-9]|2[0-3]):(?:[0-5][0-9]))        - Time Zone offset, in the form ZHH:MM or +HH:MM or -HH:MM
    //
    // Note: (?:) is a non-capturing group, since we don't care about the values, we are just interested in whether or not there is a match
    [GeneratedRegex("^(?:\\d{4})-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12][0-9]|3[01])T(?:[01][0-9]|2[0-3]):(?:[0-5][0-9]):(?:[0-5][0-9])(?:\\.\\d{1,9})?(?:Z|(?:[Z+-](?:[01][0-9]|2[0-3]):(?:[0-5][0-9])))?")]
    private static partial Regex GenerateRfc3339RegEx();

    public readonly record struct TimestampParserResult(string ModifiedText, DateTimeOffset Timestamp);
}