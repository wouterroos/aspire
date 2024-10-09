// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Turbine.Dashboard.Model.BrowserStorage;

public class SessionBrowserStorage : BrowserStorageBase, ISessionStorage
{
    public SessionBrowserStorage(ProtectedSessionStorage protectedSessionStorage) : base(protectedSessionStorage)
    {
    }
}
