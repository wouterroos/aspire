// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

namespace Turbine.Dashboard.Model;

public readonly record struct ResourceLogLine(int LineNumber, string Content, bool IsErrorMessage)
{
}
