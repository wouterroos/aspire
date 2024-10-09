// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Turbine.Dashboard.Model.BrowserStorage;

public abstract class BrowserStorageBase : IBrowserStorage
{
    private readonly ProtectedBrowserStorage _protectedBrowserStorage;

    protected BrowserStorageBase(ProtectedBrowserStorage protectedBrowserStorage)
    {
        _protectedBrowserStorage = protectedBrowserStorage;
    }

    public async Task<StorageResult<T>> GetAsync<T>(string key)
    {
        ProtectedBrowserStorageResult<T> result = await _protectedBrowserStorage.GetAsync<T>(key).ConfigureAwait(false);
        return new StorageResult<T>(result.Success, result.Value);
    }

    public async Task SetAsync<T>(string key, T value)
    {
        await _protectedBrowserStorage.SetAsync(key, value!).ConfigureAwait(false);
    }
}