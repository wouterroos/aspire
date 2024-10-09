// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Turbine.Dashboard.Model;

public class EnvironmentVariablesDialogViewModel
{
    public required List<EnvironmentVariableViewModel> EnvironmentVariables { get; init; }
    public bool ShowSpecOnlyToggle { get; set; }
}