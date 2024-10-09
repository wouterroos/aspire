// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Turbine.Dashboard.Extensions;

internal static class AssemblyExtensions
{
    public static string? GetDisplayVersion(this Assembly assembly)
    {
        // The package version is stamped into the assembly's AssemblyInformationalVersionAttribute at build time, followed by a '+' and
        // the commit hash, e.g.:
        // [assembly: AssemblyInformationalVersion("8.0.0-preview.2.23604.7+e7762a46d31842884a0bc72c92e07ba700c99bf5")]

        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (version is not null)
        {
            int plusIndex = version.IndexOf('+');

            if (plusIndex > 0)
            {
                return version[..plusIndex];
            }

            return version;
        }

        // Fallback to the file version, which is based on the CI build number, and then fallback to the assembly version, which is
        // product stable version, e.g. 8.0.0.0
        version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetCustomAttribute<AssemblyVersionAttribute>()?.Version;

        return version;
    }
}