// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using Turbine.Dashboard.Resources;

namespace Turbine.Dashboard.Model;

public class TokenFormModel
{
    [Required(ErrorMessageResourceType = typeof(Login), ErrorMessageResourceName = nameof(Login.TokenRequiredErrorMessage))]
    public string? Token { get; set; }
}
