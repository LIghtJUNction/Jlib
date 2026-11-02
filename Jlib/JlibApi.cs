#nullable enable
/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Reflection;
using PeterHan.PLib.Core;

namespace Jlib.Runtime {
    /// <summary>Stable identity inspected by consumer bootstraps before loading their implementation.</summary>
    public static class JlibApi {
        public const int AbiVersion = JlibContract.ExpectedAbi;
        public const string StaticId = JlibContract.ExpectedStaticId;
        public const string Version = "0.2.0";
        public const string PLibVersion = JlibContract.ExpectedPLibVersion;

        /// <summary>
        /// Compares this loaded provider with the ABI and minimum library version a consumer requires.
        /// ABI must match exactly. <paramref name="minimumVersion"/> may be null when any version of this ABI is acceptable.
        /// </summary>
        public static JlibCompatibility CheckCompatibility(int requiredAbi, string? minimumVersion = null) =>
            JlibContract.Evaluate(AbiVersion, Version, PLibVersion, StaticId, requiredAbi, minimumVersion);

        /// <summary>Throws <see cref="JlibCompatibilityException"/> when <see cref="CheckCompatibility"/> is not compatible.</summary>
        public static void RequireCompatibility(int requiredAbi, string? minimumVersion = null) {
            var result = CheckCompatibility(requiredAbi, minimumVersion);
            if (!result.IsCompatible)
                throw new JlibCompatibilityException(result);
        }

        /// <summary>
        /// Reflects over already loaded assemblies for <c>Jlib.Runtime.JlibApi</c>.
        /// Call this on the provider assembly that the game loaded. A bootstrap that has not
        /// loaded that assembly should compile <c>JlibContract</c> and call <see cref="JlibContract.Inspect"/> instead.
        /// </summary>
        public static JlibCompatibility DiscoverProviders(int requiredAbi, string? minimumVersion = null) =>
            JlibContract.Inspect(AppDomain.CurrentDomain.GetAssemblies(), requiredAbi, minimumVersion);

        /// <summary>
        /// Associates a dynamically loaded implementation with its owning mod before that implementation's OnLoad.
        /// The same pair may be registered again. A different owner, a blank content path, or the Jlib provider itself is rejected.
        /// <see cref="PUtil.RegisterModAssembly"/> stays available for existing PLib callers and is not given these extra checks.
        /// </summary>
        public static void RegisterConsumer(Assembly implementation, KMod.Mod owner) {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrWhiteSpace(owner.ContentPath))
                throw new ArgumentException("The owning mod must have a content path.", nameof(owner));
            if (string.Equals(implementation.GetName().Name, JlibContract.ProviderAssemblyName, StringComparison.Ordinal) ||
                    implementation.GetType(JlibContract.ApiTypeName, throwOnError: false) != null)
                throw new InvalidOperationException("The Jlib provider assembly cannot be registered as a consumer.");
            if (string.Equals(owner.staticID, StaticId, StringComparison.Ordinal))
                throw new InvalidOperationException("The Jlib provider mod cannot be registered as a consumer.");
            PUtil.RegisterModAssembly(implementation, owner);
        }
    }
}
