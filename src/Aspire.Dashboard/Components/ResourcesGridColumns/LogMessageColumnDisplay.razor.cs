// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Components;

public partial class LogMessageColumnDisplay
{
    private bool _hasErrorInfo;
    private string? _errorInfo;

    protected override void OnInitialized()
    {
       _hasErrorInfo = TryGetErrorInformation(out _errorInfo);
    }

    private bool TryGetErrorInformation([NotNullWhen(true)] out string? errorInfo)
    {
        // exception.stacktrace includes the exception message and type.
        // https://opentelemetry.io/docs/specs/semconv/attributes-registry/exception/
        if (GetProperty("exception.stacktrace") is { Length: > 0 } stackTrace)
        {
            errorInfo = stackTrace;
            return true;
        }
        if (GetProperty("exception.message") is { Length: > 0 } message)
        {
            if (GetProperty("exception.type") is { Length: > 0 } type)
            {
                errorInfo = $"{type}: {message}";
                return true;
            }

            errorInfo = message;
            return true;
        }

        errorInfo = null;
        return false;

        string? GetProperty(string propertyName)
        {
            return LogEntry.Attributes.GetValue(propertyName);
        }
    }
}
