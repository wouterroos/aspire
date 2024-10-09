// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Turbine.Dashboard.ConsoleLogs;

public static partial class UrlParser
{
    private static readonly Regex s_urlRegEx = GenerateUrlRegEx();

    public static bool TryParse(string? text, [NotNullWhen(true)] out string? modifiedText)
    {
        if (text is not null)
        {
            Match? urlMatch = s_urlRegEx.Match(text);

            StringBuilder? builder = new StringBuilder(text.Length * 2);

            int nextCharIndex = 0;
            while (urlMatch.Success)
            {
                if (urlMatch.Index > 0)
                {
                    builder.Append(text[(nextCharIndex)..urlMatch.Index]);
                }

                int urlStart = urlMatch.Index;
                nextCharIndex = urlMatch.Index + urlMatch.Length;
                string? url = text[urlStart..nextCharIndex];

                builder.Append(CultureInfo.InvariantCulture, $"<a target=\"_blank\" href=\"{url}\">{url}</a>");
                urlMatch = urlMatch.NextMatch();
            }

            if (builder.Length > 0)
            {
                if (nextCharIndex < text.Length)
                {
                    builder.Append(text[(nextCharIndex)..]);
                }

                modifiedText = builder.ToString();
                return true;
            }
        }

        modifiedText = null;
        return false;
    }

    // Regular expression that detects http/https URLs in a log entry
    // Based on the RegEx used in Windows Terminal for the same purpose, but limited
    // to only http/https URLs
    //
    // Explanation:
    // /b                             - Match must start at a word boundary (after whitespace or at the start of the text)
    // https?://                      - http:// or https://
    // [-A-Za-z0-9+&@#/%?=~_|$!:,.;]* - Any character in the list, matched zero or more times.
    // [A-Za-z0-9+&@#/%=~_|$]         - Any character in the list, matched exactly once
    [GeneratedRegex("\\bhttps?://[-A-Za-z0-9+&@#/%?=~_|$!:,.;]*[A-Za-z0-9+&@#/%=~_|$]")]
    private static partial Regex GenerateUrlRegEx();
}