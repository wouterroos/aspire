// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Turbine.Dashboard.Utils;

internal static class JSInteropHelpers
{
    public static async Task SafeDisposeAsync(IJSObjectReference? jsObjectReference)
    {
        if (jsObjectReference is not null)
        {
            try
            {
                await jsObjectReference.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // Per https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-7.0#javascript-interop-calls-without-a-circuit
                // this is one of the calls that will fail if the circuit is disconnected, and we just need to catch the exception so it doesn't pollute the logs
            }
        }
    }
}