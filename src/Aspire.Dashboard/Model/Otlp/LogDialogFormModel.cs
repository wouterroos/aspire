// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;

namespace Turbine.Dashboard.Model.Otlp;

public class LogDialogFormModel
{
    [Required]
    public SelectViewModel<string>? Parameter { get; set; }
    [Required]
    public SelectViewModel<FilterCondition>? Condition { get; set; }
    [Required]
    public string? Value { get; set; }
}
