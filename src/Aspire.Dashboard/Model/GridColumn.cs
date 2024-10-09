// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

namespace Turbine.Dashboard.Model;

public record GridColumn(string Name, string? DesktopWidth, string? MobileWidth = null, Func<bool>? IsVisible = null);