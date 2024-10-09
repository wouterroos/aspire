// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authorization;
using Turbine.Dashboard.Configuration;

namespace Turbine.Dashboard.Authentication.OpenIdConnect;

internal static class AuthorizationPolicyBuilderExtensions
{
    /// <summary>
    /// Validates that the the expected claim and value are present.
    /// </summary>
    /// <remarks>
    /// Checks are controlled by configuration.
    ///
    /// If <see cref="OpenIdConnectOptions.RequiredClaimType"/> is non-empty, a requirement for the claim is added.
    ///
    /// If a claim is being checked and <see cref="OpenIdConnectOptions.RequiredClaimValue"/> is non-empty, then the
    /// requirement is extended to also validate the specified value.
    /// </remarks>
    public static AuthorizationPolicyBuilder RequireOpenIdClaims(this AuthorizationPolicyBuilder builder, OpenIdConnectOptions options)
    {
        string? claimType = options.RequiredClaimType;
        string? claimValue = options.RequiredClaimValue;

        if (!string.IsNullOrWhiteSpace(claimType))
        {
            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                builder.RequireClaim(claimType, claimValue);
            }
            else
            {
                builder.RequireClaim(claimType);
            }
        }
        else
        {
            // AuthorizationPolicy must have at least one requirement.
            builder.RequireAuthenticatedUser();
        }

        return builder;
    }
}