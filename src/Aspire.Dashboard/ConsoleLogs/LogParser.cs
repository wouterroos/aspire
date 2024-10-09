// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Net;
using Turbine.Dashboard.Model;

namespace Turbine.Dashboard.ConsoleLogs;

internal sealed class LogParser
{
    private DateTimeOffset? _parentTimestamp;
    private Guid? _parentId;
    private int _lineIndex;
    private AnsiParser.ParserState? _residualState;

    public LogEntry CreateLogEntry(string rawText, bool isErrorOutput)
    {
        // Several steps to do here:
        //
        // 1. Parse the content to look for the timestamp
        // 2. Parse the content to look for info/warn/dbug header
        // 3. HTML Encode the raw text for security purposes
        // 4. Parse the content to look for ANSI Control Sequences and color them if possible
        // 5. Parse the content to look for URLs and make them links if possible
        // 6. Create the LogEntry to get the ID
        // 7. Set the relative properties of the log entry (parent/line index/etc)
        // 8. Return the final result

        string? content = rawText;

        // 1. Parse the content to look for the timestamp
        bool isFirstLine = false;
        DateTimeOffset? timestamp = null;

        if (TimestampParser.TryParseConsoleTimestamp(content, out TimestampParser.TimestampParserResult? timestampParseResult))
        {
            isFirstLine = true;
            content = timestampParseResult.Value.ModifiedText;
            timestamp = timestampParseResult.Value.Timestamp;
        }
        // 2. Parse the content to look for info/warn/dbug header
        // TODO extract log level and use here
        else
        {
            if (LogLevelParser.StartsWithLogLevelHeader(content))
            {
                isFirstLine = true;
            }
        }

        // 3. HTML Encode the raw text for security purposes
        content = WebUtility.HtmlEncode(content);

        // 4. Parse the content to look for ANSI Control Sequences and color them if possible
        AnsiParser.ConversionResult conversionResult = AnsiParser.ConvertToHtml(content, _residualState);
        content = conversionResult.ConvertedText;
        _residualState = conversionResult.ResidualState;

        // 5. Parse the content to look for URLs and make them links if possible
        if (UrlParser.TryParse(content, out string? modifiedText))
        {
            content = modifiedText;
        }

        // 6. Create the LogEntry to get the ID
        LogEntry? logEntry = new LogEntry
        {
            Timestamp = timestamp,
            Content = content,
            Type = isErrorOutput ? LogEntryType.Error : LogEntryType.Default,
            IsFirstLine = isFirstLine
        };

        // 7. Set the relative properties of the log entry (parent/line index/etc)
        if (isFirstLine)
        {
            _parentTimestamp = logEntry.Timestamp;
            _parentId = logEntry.Id;
            _lineIndex = 0;
        }
        else if (_parentId.HasValue)
        {
            logEntry.ParentTimestamp = _parentTimestamp;
            logEntry.ParentId = _parentId;
            logEntry.LineIndex = ++_lineIndex;
        }

        // 8. Return the final result
        return logEntry;
    }
}