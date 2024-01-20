// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Collections.Generic;
using DevHome.SetupFlow.Models;

namespace DevHome.SetupFlow.Services.WinGet;

/// <summary>
/// Thread-safe cache for packages
/// </summary>
internal class WinGetPackageCache : IWinGetPackageCache
{
    private readonly Dictionary<string, IWinGetPackage> _cache = new ();
    private readonly object _lock = new ();

    /// <inheritdoc />
    public IList<IWinGetPackage> GetPackages(IEnumerable<WinGetPackageUri> packageUris, out IEnumerable<WinGetPackageUri> packageUrisNotFound)
    {
        lock (_lock)
        {
            var foundPackages = new List<IWinGetPackage>();
            var notFoundPackageUris = new List<WinGetPackageUri>();

            foreach (var packageUri in packageUris)
            {
                if (TryGetPackage(packageUri, out var foundPackage))
                {
                    foundPackages.Add(foundPackage);
                }
                else
                {
                    notFoundPackageUris.Add(packageUri);
                }
            }

            packageUrisNotFound = notFoundPackageUris;
            return foundPackages;
        }
    }

    /// <inheritdoc />
    public bool TryGetPackage(WinGetPackageUri packageUri, out IWinGetPackage package)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(packageUri.ToUriString(), out package))
            {
                return true;
            }

            package = null;
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryAddPackage(WinGetPackageUri packageUri, IWinGetPackage package)
    {
        lock (_lock)
        {
            return _cache.TryAdd(packageUri.ToUriString(), package);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }
}
