// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Security.Claims;

namespace Turbine.Dashboard.Extensions;

internal static class ClaimsIdentityExtensions
{
    /// <summary>
    /// Searches the claims in the <see cref="ClaimsIdentity.Claims"/> for each of the claim types in <paramref name="claimTypes" />
    /// in the order presented and returns the first one that it finds.
    /// </summary>
    public static string? FindFirst(this ClaimsIdentity identity, string[] claimTypes)
    {
        foreach (string? claimType in claimTypes)
        {
            Claim? claim = identity.FindFirst(claimType);
            if (claim is not null)
            {
                return claim.Value;
            }
        }

        return null;
    }
}