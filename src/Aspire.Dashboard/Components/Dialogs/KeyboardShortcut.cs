// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Turbine.Dashboard.Components.Dialogs;

public record KeyboardShortcutCategory(string Category, List<KeyboardShortcut> Shortcuts);

public record KeyboardShortcut(string[] Keys, string Description);