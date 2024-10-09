// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Turbine.Dashboard.Otlp.Storage;

/// <summary>
/// Indicates the purpose of the subscription.
/// </summary>
public enum SubscriptionType
{
    /// <summary>
    /// On subscription notification the app will read the latest data.
    /// </summary>
    Read,
    /// <summary>
    /// On subscription notification the app won't read the latest data.
    /// </summary>
    Other
}
