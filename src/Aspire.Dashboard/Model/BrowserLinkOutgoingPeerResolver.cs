// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Aspire;
using Turbine.Dashboard.Otlp.Model;

namespace Turbine.Dashboard.Model;

public sealed class BrowserLinkOutgoingPeerResolver : IOutgoingPeerResolver
{
    public IDisposable OnPeerChanges(Func<Task> callback)
    {
        return new NullSubscription();
    }

    private sealed class NullSubscription : IDisposable
    {
        public void Dispose()
        {
        }
    }

    public bool TryResolvePeerName(KeyValuePair<string, string>[] attributes, [NotNullWhen(true)] out string? name)
    {
        // There isn't a good way to identify the HTTP request the BrowserLink middleware makes to
        // the IDE to get the script tag. The logic below looks at the host and URL and identifies
        // the HTTP request by its shape.
        // Example URL: http://localhost:59267/6eed7c2dedc14419901b813e8fe87a86/getScriptTag
        //
        // There is the chance future BrowserLink changes make this detection invalid.
        // Also, it's possible to misidentify a HTTP request.
        //
        // A long term improvement here is to add tags to the BrowserLink client and then detect the
        // values in the span's attributes.
        const string lastSegment = "getScriptTag";

        // url.full replaces http.url but look for both for backwards compatibility.
        string? url = OtlpHelpers.GetValue(attributes, "url.full") ?? OtlpHelpers.GetValue(attributes, "http.url");

        // Quick check of URL with EndsWith before more expensive Uri parsing.
        if (url != null && url.EndsWith(lastSegment, StringComparisons.UrlPath))
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && string.Equals(uri.Host, "localhost", StringComparisons.UrlHost))
            {
                string[]? parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    if (Guid.TryParse(parts[0], out _) && string.Equals(parts[1], lastSegment, StringComparisons.UrlPath))
                    {
                        name = "Browser Link";
                        return true;
                    }
                }
            }
        }

        name = null;
        return false;
    }
}