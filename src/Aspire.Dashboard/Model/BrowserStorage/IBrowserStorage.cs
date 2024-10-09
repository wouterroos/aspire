// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Turbine.Dashboard.Model.BrowserStorage;

public interface IBrowserStorage
{
    Task<StorageResult<T>> GetAsync<T>(string key);

    Task SetAsync<T>(string key, T value);
}