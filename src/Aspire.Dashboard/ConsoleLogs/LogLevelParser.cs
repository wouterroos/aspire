// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Turbine.Dashboard.ConsoleLogs;

public static partial class LogLevelParser
{
    private static readonly Regex s_logLevelRegex = GenerateLogLevelRegex();

    public static bool StartsWithLogLevelHeader(string text) => s_logLevelRegex.IsMatch(text);

    // Regular expression that detects log levels used as indicators
    // of the first line of a log entry, skipping any ANSI control sequences
    // that may come first.
    [GeneratedRegex(
        """
        ^                               # start of string
        (\x1B\[\d{1,2}m)*               # zero or more ANSI control sequences
        (trce|dbug|info|warn|fail|crit) # one of the log level names
        (\x1B\[\d{1,2}m)*               # zero or more ANSI control sequences
        :                               # colon, followed by arbitrary content that is not matched
        """,
        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex GenerateLogLevelRegex();
}
