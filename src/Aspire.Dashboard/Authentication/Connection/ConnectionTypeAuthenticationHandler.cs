// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Turbine.Dashboard.Authentication.Connection;

public class ConnectionTypeAuthenticationHandler : AuthenticationHandler<ConnectionTypeAuthenticationHandlerOptions>
{
    public ConnectionTypeAuthenticationHandler(IOptionsMonitor<ConnectionTypeAuthenticationHandlerOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        IConnectionTypeFeature? connectionTypeFeature = Context.Features.Get<IConnectionTypeFeature>();

        if (connectionTypeFeature == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("No type specified on this connection."));
        }

        if (!connectionTypeFeature.ConnectionTypes.Contains(Options.RequiredConnectionType))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Connection type {Options.RequiredConnectionType} is not enabled on this connection."));
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }
}

public static class ConnectionTypeAuthenticationDefaults
{
    public const string AuthenticationSchemeFrontend = "ConnectionFrontend";
    public const string AuthenticationSchemeOtlp = "ConnectionOtlp";
}

public sealed class ConnectionTypeAuthenticationHandlerOptions : AuthenticationSchemeOptions
{
    public ConnectionType RequiredConnectionType { get; set; }
}