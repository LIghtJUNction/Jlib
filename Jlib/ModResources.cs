/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.IO;
using System.Reflection;
using System.Text;
using PeterHan.PLib.Core;

namespace Jlib.Runtime {
    /// <summary>Reads files relative to the consumer's owning Mod content directory.</summary>
    /// <remarks>
    /// Ownership is resolved on every call, including after late consumer registration.
    /// Path containment is lexical: symbolic links and junctions are followed by the file
    /// system. This API is for trusted Mod resources, not a file system security boundary.
    /// </remarks>
    public static class ModResources {
        /// <summary>Resolves a relative resource path without requiring the file to exist.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The path is empty, absolute, or escapes the Mod root.</exception>
        /// <exception cref="InvalidOperationException">No owner with an absolute content path is available.</exception>
        public static string ResolvePath(Assembly implementation, string relativePath) {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));
            if (relativePath == null)
                throw new ArgumentNullException(nameof(relativePath));
            // Accept either separator on every host, but reject drive-relative paths and ADS.
            string normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar).
                Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(normalized) || normalized[0] == Path.DirectorySeparatorChar ||
                    normalized.IndexOf(':') >= 0 || normalized.IndexOf('\0') >= 0 || Path.IsPathRooted(normalized))
                throw new ArgumentException("A relative resource path is required.", nameof(relativePath));

            string contentPath = PUtil.GetModInfo(implementation)?.ContentPath;
            if (string.IsNullOrWhiteSpace(contentPath) || !Path.IsPathRooted(contentPath))
                throw new InvalidOperationException("The assembly has no owning Mod with an absolute content path.");
            string root = Path.GetFullPath(contentPath);
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, normalized));
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;
            if (!path.StartsWith(prefix, comparison) || path.Length <= prefix.Length)
                throw new ArgumentException("The resource path must be inside the Mod root.", nameof(relativePath));
            return path;
        }

        /// <summary>Reads a resource as bytes. File system exceptions propagate to the caller.</summary>
        public static byte[] ReadAllBytes(Assembly implementation, string relativePath) {
            return File.ReadAllBytes(ResolvePath(implementation, relativePath));
        }

        /// <summary>Reads text using UTF-8 with BOM detection, as in File.ReadAllText.</summary>
        public static string ReadAllText(Assembly implementation, string relativePath) {
            return File.ReadAllText(ResolvePath(implementation, relativePath));
        }

        /// <summary>Reads text using the supplied encoding, with BOM detection.</summary>
        public static string ReadAllText(Assembly implementation, string relativePath, Encoding encoding) {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            return File.ReadAllText(ResolvePath(implementation, relativePath), encoding);
        }
    }
}
