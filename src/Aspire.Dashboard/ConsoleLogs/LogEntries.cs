// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Aspire.Dashboard.Otlp.Storage;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Otlp.Storage;

namespace Turbine.Dashboard.ConsoleLogs;

public sealed class LogEntries(int maximumEntryCount)
{
    private readonly CircularBuffer<LogEntry> _logEntries = new(maximumEntryCount);

    private int? _baseLineNumber;

    public void Clear()
    {
        _logEntries.Clear();

        _baseLineNumber = null;
    }

    public IList<LogEntry> GetEntries() => _logEntries;

    public void InsertSorted(LogEntry logEntry, int lineNumber)
    {
        // Keep track of the base line number to ensure that we can calculate the line number of each log entry.
        // This becomes important when the total number of log entries exceeds the limit and is truncated.
        _baseLineNumber ??= lineNumber;

        if (logEntry.ParentId != null)
        {
            // If we have a parent id, then we know we're on a non-timestamped line that is part
            // of a multi-line log entry. We need to find the prior line from that entry
            for (int rowIndex = _logEntries.Count - 1; rowIndex >= 0; rowIndex--)
            {
                LogEntry? current = _logEntries[rowIndex];

                if (current.Id == logEntry.ParentId && logEntry.LineIndex - 1 == current.LineIndex)
                {
                    InsertAt(rowIndex + 1);
                    return;
                }
            }
        }
        else if (logEntry.Timestamp != null)
        {
            // Otherwise, if we have a timestamped line, we just need to find the prior line.
            // Since the rows are always in order, as soon as we see a timestamp
            // that is less than the one we're adding, we can insert it immediately after that
            for (int rowIndex = _logEntries.Count - 1; rowIndex >= 0; rowIndex--)
            {
                LogEntry? current = _logEntries[rowIndex];
                DateTimeOffset? currentTimestamp = current.Timestamp ?? current.ParentTimestamp;

                if (currentTimestamp != null && currentTimestamp <= logEntry.Timestamp)
                {
                    InsertAt(rowIndex + 1);
                    return;
                }
            }
        }

        // If we didn't find a place to insert then append it to the end. This happens with the first entry, but
        // could also happen if the logs don't have recognized timestamps.
        InsertAt(_logEntries.Count);

        void InsertAt(int index)
        {
            // Set the line number of the log entry.
            if (index == 0)
            {
                Debug.Assert(_baseLineNumber != null, "Should be set before this method is run.");
                logEntry.LineNumber = _baseLineNumber.Value;
            }
            else
            {
                logEntry.LineNumber = _logEntries[index - 1].LineNumber + 1;
            }

            // Insert the entry.
            _logEntries.Insert(index, logEntry);

            // If a log entry isn't inserted at the end then update the line numbers of all subsequent entries.
            for (int i = index + 1; i < _logEntries.Count; i++)
            {
                _logEntries[i].LineNumber++;
            }
        }
    }
}