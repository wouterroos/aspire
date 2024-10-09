// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Aspire;
using Google.Protobuf.WellKnownTypes;
using Turbine.Dashboard.Model;
using Enum = System.Enum;

namespace Aspire.ResourceService.Proto.V1;

partial class Resource
{
    /// <summary>
    /// Converts this gRPC message object to a view model for use in the dashboard UI.
    /// </summary>
    public ResourceViewModel ToViewModel()
    {
        try
        {
            return new()
            {
                Name = ValidateNotNull(Name),
                ResourceType = ValidateNotNull(ResourceType),
                DisplayName = ValidateNotNull(DisplayName),
                Uid = ValidateNotNull(Uid),
                CreationTimeStamp = ValidateNotNull(CreatedAt).ToDateTime(),
                Properties = Properties.ToFrozenDictionary(property => ValidateNotNull(property.Name), property => ValidateNotNull(property.Value), StringComparers.ResourcePropertyName),
                Environment = GetEnvironment(),
                Urls = GetUrls(),
                State = HasState ? State : null,
                KnownState = HasState ? Enum.TryParse(State, out KnownResourceState knownState) ? knownState : null : null,
                StateStyle = HasStateStyle ? StateStyle : null,
                Commands = GetCommands()
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($@"Error converting resource ""{Name}"" to {nameof(ResourceViewModel)}.", ex);
        }

        ImmutableArray<EnvironmentVariableViewModel> GetEnvironment()
        {
            return Environment
                .Select(e => new EnvironmentVariableViewModel(e.Name, e.Value, e.IsFromSpec))
                .ToImmutableArray();
        }

        ImmutableArray<UrlViewModel> GetUrls()
        {
            // Filter out bad urls
            return (from u in Urls
                    let parsedUri = Uri.TryCreate(u.FullUrl, UriKind.Absolute, out Uri? uri) ? uri : null
                    where parsedUri != null
                    select new UrlViewModel(u.Name, parsedUri, u.IsInternal))
                    .ToImmutableArray();
        }

        ImmutableArray<CommandViewModel> GetCommands()
        {
            return Commands
                .Select(c => new CommandViewModel(c.CommandType, c.DisplayName, c.ConfirmationMessage, c.Parameter))
                .ToImmutableArray();
        }

        T ValidateNotNull<T>(T value, [CallerArgumentExpression(nameof(value))] string? expression = null) where T : class
        {
            if (value is null)
            {
                throw new InvalidOperationException($"Message field '{expression}' on resource with name '{Name}' cannot be null.");
            }

            return value;
        }
    }
}

partial class ResourceCommandResponse
{
    public ResourceCommandResponseViewModel ToViewModel()
    {
        return new ResourceCommandResponseViewModel()
        {
            ErrorMessage = ErrorMessage,
            Kind = (Turbine.Dashboard.Model.ResourceCommandResponseKind)Kind
        };
    }
}