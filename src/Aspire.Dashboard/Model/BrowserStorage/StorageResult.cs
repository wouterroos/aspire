// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Turbine.Dashboard.Model.BrowserStorage;

public readonly record struct StorageResult<T>(bool Success, T? Value);
