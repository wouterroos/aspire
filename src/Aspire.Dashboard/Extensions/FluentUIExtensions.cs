// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Turbine.Dashboard.Extensions;

internal static class FluentUIExtensions
{
    public static Dictionary<string, object> GetClipboardCopyAdditionalAttributes(string? text, string? precopy, string? postcopy, params (string Attribute, object Value)[] additionalAttributes)
    {
        // No onclick attribute is added here. The CSP restricts inline scripts, including onclick.
        // Instead, a click event listener is added to the document and clicking the button is bubbled up to the event.
        // The document click listener looks for a button element and these attributes.
        Dictionary<string, object>? attributes = new Dictionary<string, object>(StringComparers.Attribute)
        {
            { "data-text", text ?? string.Empty },
            { "data-precopy", precopy ?? string.Empty },
            { "data-postcopy", postcopy ?? string.Empty },
            { "data-copybutton", "true" }
        };

        foreach ((string? attribute, object? value) in additionalAttributes)
        {
            attributes.Add(attribute, value);
        }

        return attributes;
    }

    public static Dictionary<string, object> GetOpenTextVisualizerAdditionalAttributes(string textValue, string textValueDescription, params (string Attribute, object Value)[] additionalAttributes)
    {
        Dictionary<string, object>? attributes = new Dictionary<string, object>(StringComparers.Attribute)
        {
            { "data-text", textValue },
            { "data-textvisualizer-description", textValueDescription }
        };

        foreach ((string? attribute, object? value) in additionalAttributes)
        {
            attributes.Add(attribute, value);
        }

        return attributes;
    }
}