// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Turbine.Dashboard.Authentication.OtlpApiKey;
using Turbine.Dashboard.Authentication.Connection;
using Turbine.Dashboard.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Turbine.Dashboard.Authentication;

public sealed class OtlpCompositeAuthenticationHandler(
    IOptionsMonitor<DashboardOptions> dashboardOptions,
    IOptionsMonitor<OtlpCompositeAuthenticationHandlerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
        : AuthenticationHandler<OtlpCompositeAuthenticationHandlerOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        DashboardOptions? options = dashboardOptions.CurrentValue;

        foreach (string? scheme in GetRelevantAuthenticationSchemes())
        {
            AuthenticateResult? result = await Context.AuthenticateAsync(scheme).ConfigureAwait(false);

            if (result.Failure is not null)
            {
                return result;
            }
        }

        ClaimsIdentity? id = new ClaimsIdentity([new Claim(OtlpAuthorization.OtlpClaimName, bool.TrueString)]);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), Scheme.Name));

        IEnumerable<string> GetRelevantAuthenticationSchemes()
        {
            yield return ConnectionTypeAuthenticationDefaults.AuthenticationSchemeOtlp;

            if (options.Otlp.AuthMode is OtlpAuthMode.ApiKey)
            {
                yield return OtlpApiKeyAuthenticationDefaults.AuthenticationScheme;
            }
            else if (options.Otlp.AuthMode is OtlpAuthMode.ClientCertificate)
            {
                yield return CertificateAuthenticationDefaults.AuthenticationScheme;
            }
        }
    }
}

public static class OtlpCompositeAuthenticationDefaults
{
    public const string AuthenticationScheme = "OtlpComposite";
}

public sealed class OtlpCompositeAuthenticationHandlerOptions : AuthenticationSchemeOptions
{
}