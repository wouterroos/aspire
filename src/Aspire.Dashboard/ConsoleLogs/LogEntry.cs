// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Turbine.Dashboard.Model;

[DebuggerDisplay("Timestamp = {(Timestamp ?? ParentTimestamp),nq}, Content = {Content}")]
public sealed class LogEntry
{
    public string? Content { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public LogEntryType Type { get; init; } = LogEntryType.Default;
    public int LineIndex { get; set; }
    public Guid? ParentId { get; set; }
    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset? ParentTimestamp { get; set; }
    public bool IsFirstLine { get; init; }
    public int LineNumber { get; set; }
}

public enum LogEntryType
{
    Default,
    Error
}