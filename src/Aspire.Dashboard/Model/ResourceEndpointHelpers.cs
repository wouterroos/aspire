// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aspire;

namespace Turbine.Dashboard.Model;

internal static class ResourceEndpointHelpers
{
    /// <summary>
    /// A resource has services and endpoints. These can overlap. This method attempts to return a single list without duplicates.
    /// </summary>
    public static List<DisplayedEndpoint> GetEndpoints(ResourceViewModel resource, bool includeInternalUrls = false)
    {
        List<DisplayedEndpoint>? endpoints = new List<DisplayedEndpoint>(resource.Urls.Length);

        foreach (UrlViewModel? url in resource.Urls)
        {
            if ((includeInternalUrls && url.IsInternal) || !url.IsInternal)
            {
                endpoints.Add(new DisplayedEndpoint
                {
                    Name = url.Name,
                    Text = url.Url.OriginalString,
                    Address = url.Url.Host,
                    Port = url.Url.Port,
                    Url = url.Url.Scheme is "http" or "https" ? url.Url.OriginalString : null
                });
            }
        }

        // Make sure that endpoints have a consistent ordering.
        // Order:
        // - https
        // - other urls
        // - endpoint name
        List<DisplayedEndpoint>? orderedEndpoints = endpoints
            .OrderByDescending(e => e.Url?.StartsWith("https") == true)
            .ThenByDescending(e => e.Url != null)
            .ThenBy(e => e.Name, StringComparers.EndpointAnnotationName)
            .ToList();

        return orderedEndpoints;
    }
}

[DebuggerDisplay("Name = {Name}, Text = {Text}, Address = {Address}:{Port}, Url = {Url}")]
public sealed class DisplayedEndpoint
{
    public required string Name { get; set; }
    public required string Text { get; set; }
    public string? Address { get; set; }
    public int? Port { get; set; }
    public string? Url { get; set; }
}