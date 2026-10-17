// Copyright (c) HoneyDrunk Studios. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Security.Cryptography.Pkcs;

namespace PocketQuests.Tests.Unit;

/// <summary>Guards the implementation selected by the API host dependency graph.</summary>
public sealed class PkcsRuntimeSecurityTests
{
    /// <summary>The loaded PKCS implementation includes the .NET 10 advisory fixes.</summary>
    [Fact]
    public void LoadedPkcsRuntime_MeetsAdvisoryPatchedMinimum()
    {
        // GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx require 10.0.13 on .NET 10.
        // Inspect the loaded implementation: NuGet can resolve patched 9.x while
        // the ASP.NET shared framework supplies an affected 10.x assembly instead.
        var assembly = typeof(SignedCms).Assembly;
        var informationalVersion = Assert.IsType<AssemblyInformationalVersionAttribute>(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>());
        var loadedVersion = Version.Parse(informationalVersion.InformationalVersion.Split('+')[0]);

        Assert.True(
            loadedVersion >= new Version(10, 0, 13),
            $"Loaded PKCS {loadedVersion}; the .NET 10 advisory minimum is 10.0.13.");
    }
}
