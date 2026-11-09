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
        public const string Version = "0.3.2";
        public const string PLibVersion = JlibContract.ExpectedPLibVersion;

        /// <summary>
        /// Compares this loaded provider with the ABI and minimum library version a consumer requires.
        /// ABI must match exactly. <paramref name="minimumVersion"/> may be null when any version of this ABI is acceptable.
        /// </summary>
        public static JlibCompatibility CheckCompatibility(int requiredAbi, string? minimumVersion = null) {
            JlibDiagnostics.RecordCall("JlibApi.CheckCompatibility");
            return JlibContract.Evaluate(AbiVersion, Version, PLibVersion, StaticId, requiredAbi, minimumVersion);
        }

        /// <summary>Throws <see cref="JlibCompatibilityException"/> when <see cref="CheckCompatibility"/> is not compatible.</summary>
        public static void RequireCompatibility(int requiredAbi, string? minimumVersion = null) {
            JlibDiagnostics.RecordCall("JlibApi.RequireCompatibility");
            var result = CheckCompatibility(requiredAbi, minimumVersion);
            if (!result.IsCompatible)
                throw new JlibCompatibilityException(result);
        }

        /// <summary>
        /// Reflects over already loaded assemblies for <c>Jlib.Runtime.JlibApi</c>.
        /// Call this on the provider assembly that the game loaded. A bootstrap that has not
        /// loaded that assembly should compile <c>JlibContract</c> and call <see cref="JlibContract.Inspect"/> instead.
        /// </summary>
        public static JlibCompatibility DiscoverProviders(int requiredAbi, string? minimumVersion = null) {
            JlibDiagnostics.RecordCall("JlibApi.DiscoverProviders");
            return JlibContract.Inspect(AppDomain.CurrentDomain.GetAssemblies(), requiredAbi, minimumVersion);
        }

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

        /// <summary>
        /// Redirects a P/Invoke DLL name for one loaded managed assembly's MonoImage to an
        /// absolute native library path. See <see cref="GameComponentReplacement.Replace"/>.
        /// Requires library version 0.3.0 or newer; ABI remains 1.
        /// </summary>
        public static GameComponentReplacementResult ReplaceNativeLibrary(Assembly managedAssembly,
                string dllMapName, string absoluteReplacementPath,
                GameComponentReplacementOptions? options = null) =>
            GameComponentReplacement.Replace(managedAssembly, dllMapName, absoluteReplacementPath, options);

        /// <summary>
        /// Redirects Oxygen Not Included's <c>SimDLL</c> P/Invoke imports for
        /// <paramref name="gameAssembly"/> to <paramref name="absoluteReplacementPath"/>.
        /// </summary>
        public static GameComponentReplacementResult ReplaceSimDll(Assembly gameAssembly,
                string absoluteReplacementPath, GameComponentReplacementOptions? options = null) =>
            GameComponentReplacement.ReplaceSimDll(gameAssembly, absoluteReplacementPath, options);

        /// <summary>Looks up a path previously applied by <see cref="ReplaceNativeLibrary"/>.</summary>
        public static bool TryGetMappedNativeLibrary(Assembly managedAssembly, string dllMapName,
                out string? path) =>
            GameComponentReplacement.TryGetMappedPath(managedAssembly, dllMapName, out path);

        /// <summary>
        /// Records an interface invocation in the Jlib dynamic profiler, tracking call count and caller mod.
        /// </summary>
        public static void TrackInterfaceCall(string interfaceName, Assembly? caller = null) =>
            JlibDiagnostics.RecordCall(interfaceName, caller);

        /// <summary>Gets or sets whether real-time debug diagnostics are emitted to Player.log.</summary>
        public static bool DebugMode {
            get => JlibDiagnostics.DebugMode;
            set => JlibDiagnostics.DebugMode = value;
        }

        /// <summary>Generates a comprehensive diagnostic text report of interfaces, calls, and Harmony pipelines.</summary>
        public static string DumpDiagnosticReport() => JlibDiagnostics.GenerateReport();

#if !JLIB_HOST_TESTS
        /// <summary>Returns true if the caller is running on the Unity main rendering thread.</summary>
        public static bool IsMainThread => JlibDispatcher.IsMainThread;

        /// <summary>Throws an exception if the caller is not executing on Unity's main thread.</summary>
        public static void RequireMainThread(string? operationName = null) =>
            JlibDispatcher.RequireMainThread(operationName);

        /// <summary>Executes the specified action on the Unity main thread, inlining if already on main thread.</summary>
        public static void RunOnMainThread(System.Action action) => JlibDispatcher.Enqueue(action);

        /// <summary>Executes the specified action on a subsequent Unity frame update.</summary>
        public static void RunOnMainThreadDeferred(System.Action action) => JlibDispatcher.EnqueueDeferred(action);

        /// <summary>Executes a delegate on the Unity main thread and blocks the worker thread until completion.</summary>
        public static T InvokeOnMainThread<T>(Func<T> func, int timeoutMs = 10000) =>
            JlibDispatcher.Invoke(func, timeoutMs);
#endif
    }
}
