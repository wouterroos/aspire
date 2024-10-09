// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Turbine.Dashboard.Components.Layout;

internal record MobileNavMenuEntry(string Text, Func<Task> OnClick, Icon? Icon = null, Regex? LinkMatchRegex = null);