// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System;
using System.Web;

namespace DevHome.SetupFlow.Models;
public class WinGetPackageUri
{
    [Flags]
    public enum Parameters
    {
        None = 0,
        Version = 1 << 0,

        // Add all parameters here
        All = Version,
    }

    /// <summary>
    /// Windows package manager custom protocol scheme
    /// </summary>
    private const string WinGetScheme = "x-ms-winget";
    private const string VersionQueryParameter = "version";

    public string CatalogName { get; }

    public string PackageId { get; }

    public string Version { get; }

    public WinGetPackageUri(string catalogName, string packageId, string version = null)
    {
        CatalogName = catalogName;
        PackageId = packageId;
        Version = version;
    }

    public static bool TryCreate(Uri uri, out WinGetPackageUri packageUri)
    {
        // Ensure the Uri is not null
        if (uri == null)
        {
            packageUri = null;
            return false;
        }

        // Ensure the Uri is a WinGet Uri
        if (uri.Scheme == WinGetScheme && uri.Segments.Length == 2)
        {
            var packageId = uri.Segments[1];
            var catalogUriName = uri.Host;

            // Read query parameters
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            var version = queryParams.Get(VersionQueryParameter);

            packageUri = new (catalogUriName, packageId, version);
            return true;
        }

        packageUri = null;
        return false;
    }

    public static bool TryCreate(string stringUri, out WinGetPackageUri packageUri)
    {
        // Ensure the string is a valid Uri
        packageUri = null;
        return Uri.TryCreate(stringUri, UriKind.Absolute, out var uri) && TryCreate(uri, out packageUri);
    }

    public string ToUriString(Parameters includeParameters = Parameters.None)
    {
        var queryParams = HttpUtility.ParseQueryString(string.Empty);

        if (includeParameters.HasFlag(Parameters.Version) && !string.IsNullOrWhiteSpace(Version))
        {
            queryParams.Add(VersionQueryParameter, Version);
        }

        var queryString = queryParams.Count > 0 ? $"?{queryParams}" : string.Empty;
        return $"{WinGetScheme}://{CatalogName}/{PackageId}{queryString}";
    }

    public override string ToString()
    {
        return ToUriString();
    }

    public bool AreEqual(string stringUri, Parameters includeParameters = Parameters.None)
    {
        return TryCreate(stringUri, out var uri) && AreEqual(uri, includeParameters);
    }

    public bool AreEqual(WinGetPackageUri uri, Parameters includeParameters = Parameters.None)
    {
        if (uri == null)
        {
            return false;
        }

        if (CatalogName != uri.CatalogName || PackageId != uri.PackageId)
        {
            return false;
        }

        if (includeParameters.HasFlag(Parameters.Version) && Version != uri.Version)
        {
            return false;
        }

        return true;
    }
}
