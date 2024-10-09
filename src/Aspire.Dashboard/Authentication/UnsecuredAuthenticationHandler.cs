// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Turbine.Dashboard.Authentication;

public class UnsecuredAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public UnsecuredAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        ClaimsIdentity? id = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "Local"), new Claim(FrontendAuthorizationDefaults.UnsecuredClaimName, bool.TrueString)],
            FrontendAuthenticationDefaults.AuthenticationSchemeUnsecured);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), Scheme.Name)));
    }
}