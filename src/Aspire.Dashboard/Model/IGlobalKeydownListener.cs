// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Turbine.Dashboard.Model;

public interface IGlobalKeydownListener
{
    IReadOnlySet<TurbineKeyboardShortcut> SubscribedShortcuts { get; }

    Task OnPageKeyDownAsync(TurbineKeyboardShortcut shortcut);
}

public enum TurbineKeyboardShortcut
{
    Help = 100,
    Settings = 110,

    GoToResources = 200,
    GoToConsoleLogs = 210,
    GoToStructuredLogs = 220,
    GoToTraces = 230,
    GoToMetrics = 240,

    ToggleOrientation = 300,
    ClosePanel = 310,
    ResetPanelSize = 320,
    IncreasePanelSize = 330,
    DecreasePanelSize = 340,
}