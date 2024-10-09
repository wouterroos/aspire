// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Turbine.Dashboard.Model;

internal static class TargetLocationInterceptor
{
    public const string ResourcesPath = "/";
    public const string StructuredLogsPath = "/structuredlogs";

    public static bool InterceptTargetLocation(string appBaseUri, string originalTargetLocation, [NotNullWhen(true)] out string? newTargetLocation)
    {
        string path;
        Uri? uri = new Uri(originalTargetLocation, UriKind.RelativeOrAbsolute);

        // Location could be an absolute URL if clicking on link in the page.
        if (uri.IsAbsoluteUri)
        {
            // Don't want to modify the URL if it is to a different app.
            Uri? targetBaseUri = new Uri(uri.GetLeftPart(UriPartial.Authority));
            if (targetBaseUri != new Uri(appBaseUri))
            {
                newTargetLocation = null;
                return false;
            }

            path = uri.AbsolutePath;
        }
        else
        {
            path = originalTargetLocation;
        }

        if (string.Equals(path, ResourcesPath, StringComparisons.UrlPath))
        {
            newTargetLocation = StructuredLogsPath;
            return true;
        }

        newTargetLocation = null;
        return false;
    }
}