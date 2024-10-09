// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Aspire;
using Aspire.Dashboard.Model;
using Google.Protobuf.WellKnownTypes;
using Turbine.Dashboard.Utils;

namespace Turbine.Dashboard.Model;

internal static class ResourceViewModelExtensions
{
    public static bool IsContainer(this ResourceViewModel resource)
    {
        return StringComparers.ResourceType.Equals(resource.ResourceType, KnownResourceTypes.Container);
    }

    public static bool IsProject(this ResourceViewModel resource)
    {
        return StringComparers.ResourceType.Equals(resource.ResourceType, KnownResourceTypes.Project);
    }

    public static bool IsExecutable(this ResourceViewModel resource, bool allowSubtypes)
    {
        if (StringComparers.ResourceType.Equals(resource.ResourceType, KnownResourceTypes.Executable))
        {
            return true;
        }

        if (allowSubtypes)
        {
            return StringComparers.ResourceType.Equals(resource.ResourceType, KnownResourceTypes.Project);
        }

        return false;
    }

    public static bool TryGetExitCode(this ResourceViewModel resource, out int exitCode)
    {
        return resource.TryGetCustomDataInt(KnownProperties.Resource.ExitCode, out exitCode);
    }

    public static bool TryGetContainerImage(this ResourceViewModel resource, [NotNullWhen(returnValue: true)] out string? containerImage)
    {
        return resource.TryGetCustomDataString(KnownProperties.Container.Image, out containerImage);
    }

    public static bool TryGetProjectPath(this ResourceViewModel resource, [NotNullWhen(returnValue: true)] out string? projectPath)
    {
        return resource.TryGetCustomDataString(KnownProperties.Project.Path, out projectPath);
    }

    public static bool TryGetExecutablePath(this ResourceViewModel resource, [NotNullWhen(returnValue: true)] out string? executablePath)
    {
        return resource.TryGetCustomDataString(KnownProperties.Executable.Path, out executablePath);
    }

    public static bool TryGetExecutableArguments(this ResourceViewModel resource, out ImmutableArray<string> arguments)
    {
        return resource.TryGetCustomDataStringArray(KnownProperties.Executable.Args, out arguments);
    }

    private static bool TryGetCustomDataString(this ResourceViewModel resource, string key, [NotNullWhen(returnValue: true)] out string? s)
    {
        if (resource.Properties.TryGetValue(key, out Value? value) && value.TryConvertToString(out string? valueString))
        {
            s = valueString;
            return true;
        }

        s = null;
        return false;
    }

    private static bool TryGetCustomDataStringArray(this ResourceViewModel resource, string key, out ImmutableArray<string> strings)
    {
        if (resource.Properties.TryGetValue(key, out Value? value) && value.ListValue is not null)
        {
            ImmutableArray<string>.Builder? builder = ImmutableArray.CreateBuilder<string>(value.ListValue.Values.Count);

            foreach (Value? element in value.ListValue.Values)
            {
                if (!element.TryConvertToString(out string? elementString))
                {
                    strings = default;
                    return false;
                }

                builder.Add(elementString);
            }

            strings = builder.MoveToImmutable();
            return true;
        }

        strings = default;
        return false;
    }

    private static bool TryGetCustomDataInt(this ResourceViewModel resource, string key, out int i)
    {
        if (resource.Properties.TryGetValue(key, out Value? value) && value.TryConvertToInt(out i))
        {
            return true;
        }

        i = 0;
        return false;
    }
}