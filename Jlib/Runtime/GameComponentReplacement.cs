/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Jlib.Runtime {
    /// <summary>Outcome of a native game-component replacement attempt.</summary>
    public enum GameComponentReplacementState {
        /// <summary>The image-scoped Mono DLL map was inserted for the first time.</summary>
        Applied = 0,
        /// <summary>The same assembly, DLL map name, and absolute path were already mapped.</summary>
        AlreadyApplied = 1,
        /// <summary>No mapping was inserted; see <see cref="GameComponentReplacementResult.Message"/>.</summary>
        Refused = 2
    }

    /// <summary>Result of <see cref="GameComponentReplacement.Replace"/>.</summary>
    public sealed class GameComponentReplacementResult {
        public GameComponentReplacementState State { get; }
        public string Message { get; }
        public string DllMapName { get; }
        public string ReplacementPath { get; }
        public bool Succeeded => State == GameComponentReplacementState.Applied ||
            State == GameComponentReplacementState.AlreadyApplied;

        internal GameComponentReplacementResult(GameComponentReplacementState state, string message,
                string dllMapName, string replacementPath) {
            State = state;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            DllMapName = dllMapName ?? string.Empty;
            ReplacementPath = replacementPath ?? string.Empty;
        }

        public override string ToString() => Message;
    }

    /// <summary>Optional guards for native component replacement.</summary>
    public sealed class GameComponentReplacementOptions {
        /// <summary>
        /// When true (default), refuse if a public static <c>Game.Instance</c> property already
        /// returns a non-null value. Mapping after the game object exists is unsafe.
        /// </summary>
        public bool RequireNoGameInstance { get; set; } = true;

        /// <summary>Managed type that owns the static <c>Instance</c> property. Defaults to <c>Game</c>.</summary>
        public string GameTypeName { get; set; } = "Game";
    }

    /// <summary>
    /// Replaces a native library that the game loads through P/Invoke by inserting an
    /// image-scoped Mono DLL map (<c>mono_dllmap_insert</c>). Typical use is redirecting
    /// <c>SimDLL</c> to a custom native simulation library before <c>Game.Instance</c> exists.
    /// </summary>
    /// <remarks>
    /// Call only inside the Unity Mono process that hosts Oxygen Not Included. The mapping
    /// cannot be undone without restarting the game. Official ABI validation of the replacement
    /// binary remains the caller's responsibility.
    /// </remarks>
    public static class GameComponentReplacement {
        /// <summary>P/Invoke library name used by Oxygen Not Included for its simulation DLL.</summary>
        public const string SimDllMapName = "SimDLL";

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, string> MappedPaths =
            new Dictionary<string, string>(StringComparer.Ordinal);
        // Retain target handles so the OS does not unload a mapped library while Mono holds
        // cached native entry points for that image.
        private static readonly List<NativeModule> RetainedTargets = new List<NativeModule>();

        /// <summary>
        /// Maps <paramref name="dllMapName"/> for the MonoImage of <paramref name="managedAssembly"/>
        /// to <paramref name="absoluteReplacementPath"/>.
        /// </summary>
        public static GameComponentReplacementResult Replace(Assembly managedAssembly, string dllMapName,
                string absoluteReplacementPath, GameComponentReplacementOptions options = null) {
            if (managedAssembly == null)
                throw new ArgumentNullException(nameof(managedAssembly));
            if (dllMapName == null)
                throw new ArgumentNullException(nameof(dllMapName));
            if (absoluteReplacementPath == null)
                throw new ArgumentNullException(nameof(absoluteReplacementPath));

            string mapName = dllMapName.Trim();
            if (mapName.Length == 0)
                throw new ArgumentException("A non-empty P/Invoke DLL map name is required.", nameof(dllMapName));

            string path;
            try {
                if (!Path.IsPathRooted(absoluteReplacementPath))
                    return Refused(mapName, absoluteReplacementPath,
                        "The replacement path must be absolute.");
                path = Path.GetFullPath(absoluteReplacementPath);
                if (!File.Exists(path))
                    return Refused(mapName, path, "The replacement native library was not found.");
            } catch (Exception error) {
                return Refused(mapName, absoluteReplacementPath,
                    "Invalid replacement path: " + error.GetType().Name + ": " + error.Message);
            }

            string assemblyLocation = managedAssembly.Location;
            if (string.IsNullOrWhiteSpace(assemblyLocation) || !File.Exists(assemblyLocation))
                return Refused(mapName, path,
                    "The managed assembly has no load path that Mono can identify.");

            string assemblyKey = Path.GetFullPath(assemblyLocation);
            string registryKey = RegistryKey(assemblyKey, mapName);
            options = options ?? new GameComponentReplacementOptions();

            lock (Gate) {
                if (MappedPaths.TryGetValue(registryKey, out string existing)) {
                    if (string.Equals(existing, path, NativePlatform.PathComparison))
                        return new GameComponentReplacementResult(GameComponentReplacementState.AlreadyApplied,
                            "The same image-scoped mapping is already active; restart is required to change it.",
                            mapName, existing);
                    return Refused(mapName, path,
                        "A different replacement is already mapped for this assembly and DLL name (" +
                        existing + "). Restart is required.");
                }

                try {
                    NativePlatform.RequireSupported();
                    if (Type.GetType("Mono.Runtime") == null)
                        return Refused(mapName, path,
                            "Native component replacement requires Unity Mono.");
                    if (options.RequireNoGameInstance)
                        RequireNoGameInstance(managedAssembly, options.GameTypeName);

                    string monoPath = NativePlatform.MonoPath(assemblyKey);
                    var mapper = new MonoDllMap(NativeModule.OpenAlreadyLoaded(monoPath));
                    IntPtr image = mapper.FindImage(assemblyKey);
                    if (image == IntPtr.Zero)
                        return Refused(mapName, path,
                            "The exact loaded MonoImage for the managed assembly was not found.");

                    // Open and retain the target before the irreversible image-scoped mutation.
                    NativeModule target = NativeModule.OpenTarget(path);
                    mapper.Insert(image, mapName, path);
                    RetainedTargets.Add(target);
                    MappedPaths[registryKey] = path;
                    return new GameComponentReplacementResult(GameComponentReplacementState.Applied,
                        "Image-scoped mapping inserted; gameReady remains unverified by Jlib. Restart is required to remove or replace it.",
                        mapName, path);
                } catch (Exception error) {
                    return Refused(mapName, path,
                        "Native component replacement refused: " + error.GetType().Name + ": " + error.Message);
                }
            }
        }

        /// <summary>Convenience wrapper that maps <see cref="SimDllMapName"/> for Assembly-CSharp.</summary>
        public static GameComponentReplacementResult ReplaceSimDll(Assembly gameAssembly,
                string absoluteReplacementPath, GameComponentReplacementOptions options = null) =>
            Replace(gameAssembly, SimDllMapName, absoluteReplacementPath, options);

        /// <summary>Returns the absolute path previously mapped for this assembly and DLL name, if any.</summary>
        public static bool TryGetMappedPath(Assembly managedAssembly, string dllMapName, out string path) {
            path = null;
            if (managedAssembly == null || string.IsNullOrWhiteSpace(dllMapName) ||
                    string.IsNullOrWhiteSpace(managedAssembly.Location))
                return false;
            string key = RegistryKey(Path.GetFullPath(managedAssembly.Location), dllMapName.Trim());
            lock (Gate)
                return MappedPaths.TryGetValue(key, out path);
        }

        private static string RegistryKey(string assemblyFullPath, string dllMapName) =>
            assemblyFullPath + "\0" + dllMapName;

        private static GameComponentReplacementResult Refused(string dllMapName, string path, string message) =>
            new GameComponentReplacementResult(GameComponentReplacementState.Refused, message, dllMapName, path);

        private static void RequireNoGameInstance(Assembly managedAssembly, string gameTypeName) {
            string typeName = string.IsNullOrWhiteSpace(gameTypeName) ? "Game" : gameTypeName.Trim();
            Type game = managedAssembly.GetType(typeName, throwOnError: false) ??
                Type.GetType(typeName + ", Assembly-CSharp", throwOnError: false);
            if (game == null)
                throw new InvalidOperationException(
                    "Could not locate type '" + typeName + "' to verify Game.Instance is unset.");
            PropertyInfo instance = game.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instance == null)
                throw new InvalidOperationException(
                    "Type '" + typeName + "' has no public static Instance property.");
            if (instance.GetValue(null, null) != null)
                throw new InvalidOperationException(
                    "Mapping is only permitted during startup before a Game instance exists.");
        }
    }
}
