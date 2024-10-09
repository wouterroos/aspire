// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Turbine.Dashboard.Utils;

internal static class IBrowserStorageExtensions
{
    /// <summary>
    /// Retrieves the value associated with the specified key.
    /// If there is a CryptographicException, return default instead of throwing. A CryptographicException can occur
    /// because the local data protection key for the dashboard has changed, and previously stored data can no longer be
    /// successfully decrypted.
    /// </summary>
    public static async Task<StorageResult<T>> SafeGetAsync<T>(this IBrowserStorage value, string key)
    {
        try
        {
            return await value.GetAsync<T>(key).ConfigureAwait(false);
        }
        catch (CryptographicException ex)
        {
            Debug.WriteLine($"Failed to decrypt data for key '{key}': {ex.Message}");
            return default;
        }
    }
}