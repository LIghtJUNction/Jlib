/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Jlib.Runtime {
    /// <summary>Locates the Unity Mono runtime that hosts Oxygen Not Included.</summary>
    public static class NativePlatform {
        internal static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        internal static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        internal static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        internal static void RequireSupported() {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || !BitConverter.IsLittleEndian ||
                    !(IsWindows || IsMac || IsLinux))
                throw new PlatformNotSupportedException(
                    "Native component replacement requires a Windows, macOS, or Linux x64 game process.");
        }

        /// <summary>
        /// Resolves the MonoBleedingEdge embed runtime next to a managed game assembly
        /// such as Assembly-CSharp.dll.
        /// </summary>
        public static string MonoPath(string assemblyPath) {
            RequireSupported();
            string data = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(assemblyPath)));
            if (string.IsNullOrEmpty(data))
                throw new InvalidOperationException("The managed assembly path has no game data directory.");
            string root = Path.GetDirectoryName(data);
            if (IsMac) {
                var ancestor = new DirectoryInfo(data);
                while (ancestor != null && ancestor.Name != "Contents")
                    ancestor = ancestor.Parent;
                if (ancestor == null)
                    throw new InvalidOperationException("The game assembly is outside a macOS app bundle.");
                root = ancestor.FullName;
            }
            string[] candidates = IsWindows ? new[] {
                Path.Combine(root, "MonoBleedingEdge", "EmbedRuntime", "mono-2.0-bdwgc.dll"),
                Path.Combine(data, "MonoBleedingEdge", "EmbedRuntime", "mono-2.0-bdwgc.dll")
            } : IsMac ? new[] {
                Path.Combine(root, "Frameworks", "MonoBleedingEdge", "MonoEmbedRuntime", "osx", "libmonobdwgc-2.0.dylib"),
                Path.Combine(root, "Frameworks", "MonoBleedingEdge", "EmbedRuntime", "libmonobdwgc-2.0.dylib"),
                Path.Combine(data, "MonoBleedingEdge", "EmbedRuntime", "libmonobdwgc-2.0.dylib")
            } : new[] {
                Path.Combine(data, "MonoBleedingEdge", "x86_64", "libmonobdwgc-2.0.so")
            };
            foreach (string path in candidates) {
                if (File.Exists(path))
                    return path;
            }
            throw new FileNotFoundException("The game's Unity Mono runtime was not found in its platform layout.");
        }

        internal static StringComparison PathComparison =>
            IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }
}
