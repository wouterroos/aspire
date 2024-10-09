// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Turbine.Dashboard.Authentication.Connection;
using Turbine.Dashboard.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Turbine.Dashboard.Authentication;

public sealed class FrontendCompositeAuthenticationHandler(
    IOptionsMonitor<DashboardOptions> dashboardOptions,
    IOptionsMonitor<FrontendCompositeAuthenticationHandlerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
        : AuthenticationHandler<FrontendCompositeAuthenticationHandlerOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        AuthenticateResult? result = await Context.AuthenticateAsync(ConnectionTypeAuthenticationDefaults.AuthenticationSchemeFrontend).ConfigureAwait(false);
        if (result.Failure is not null)
        {
            return AuthenticateResult.Fail(
                result.Failure,
                new AuthenticationProperties(
                    items: new Dictionary<string, string?>(),
                    parameters: new Dictionary<string, object?> { [TurbinePolicyEvaluator.SuppressChallengeKey] = true }));
        }

        result = await Context.AuthenticateAsync().ConfigureAwait(false);
        return result;
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        string? scheme = GetRelevantAuthenticationScheme();
        if (scheme != null)
        {
            await Context.ChallengeAsync(scheme).ConfigureAwait(false);
        }
    }

    private string? GetRelevantAuthenticationScheme()
    {
        return dashboardOptions.CurrentValue.Frontend.AuthMode switch
        {
            FrontendAuthMode.OpenIdConnect => FrontendAuthenticationDefaults.AuthenticationSchemeOpenIdConnect,
            FrontendAuthMode.BrowserToken => FrontendAuthenticationDefaults.AuthenticationSchemeBrowserToken,
            _ => null
        };
    }
}

public static class FrontendCompositeAuthenticationDefaults
{
    public const string AuthenticationScheme = "FrontendComposite";
}

public sealed class FrontendCompositeAuthenticationHandlerOptions : AuthenticationSchemeOptions
{
}