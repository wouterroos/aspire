// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Turbine.Dashboard.Extensions;

internal static class StringExtensions
{
    public static string SanitizeHtmlId(this string input)
    {
        StringBuilder? sanitizedBuilder = new StringBuilder(capacity: input.Length);

        foreach (char c in input)
        {
            if (IsValidHtmlIdCharacter(c))
            {
                sanitizedBuilder.Append(c);
            }
            else
            {
                sanitizedBuilder.Append('_');
            }
        }

        return sanitizedBuilder.ToString();

        static bool IsValidHtmlIdCharacter(char c)
        {
            // Check if the character is a letter, digit, underscore, or hyphen
            return char.IsLetterOrDigit(c) || c == '_' || c == '-';
        }
    }

    /// <summary>
    /// Returns the two initial letters of the first and last words in the specified <paramref name="name"/>.
    /// If only one word is present, a single initial is returned. If <paramref name="name"/> is null, empty or
    /// white space only, <paramref name="defaultValue"/> is returned.
    /// </summary>
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static string? GetInitials(this string name, string? defaultValue = default)
    {
        ReadOnlySpan<char> s = name.AsSpan().Trim();

        if (s.Length == 0)
        {
            return defaultValue;
        }

        int lastSpaceIndex = s.LastIndexOf(' ');

        if (lastSpaceIndex == -1)
        {
            return s[0].ToString().ToUpperInvariant();
        }

        // The name contained two or more words. Return the initials from the first and last.
        return $"{char.ToUpperInvariant(s[0])}{char.ToUpperInvariant(s[lastSpaceIndex + 1])}";
    }
}