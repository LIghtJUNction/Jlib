#nullable enable
/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 *
 * Original Jlib contract helper. It does not reference the game, Harmony, or PLib,
 * so a consumer bootstrap can compile this file without loading Jlib.dll.
 */

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jlib.Runtime {
    /// <summary>Why a consumer accepted or rejected the loaded Jlib provider.</summary>
    public enum JlibCompatibilityStatus {
        Compatible = 0,
        ProviderMissing = 1,
        ProviderAmbiguous = 2,
        IdentityIncomplete = 3,
        StaticIdMismatch = 4,
        ProtocolMismatch = 5,
        VersionUnreadable = 6,
        AbiMismatch = 7,
        VersionOlderThanRequired = 8
    }

    /// <summary>One evaluation of the public Jlib provider contract.</summary>
    public sealed class JlibCompatibility {
        public JlibCompatibilityStatus Status { get; }
        public string Code { get; }
        public string Message { get; }
        public bool IsCompatible => Status == JlibCompatibilityStatus.Compatible;
        public int ProviderAbi { get; }
        public string? ProviderVersion { get; }
        public string? ProviderPLibVersion { get; }
        public string? ProviderStaticId { get; }
        public int RequiredAbi { get; }
        public string? MinimumVersion { get; }
        public int ProviderCount { get; }
        public string? ProviderAssemblyName { get; }

        internal JlibCompatibility(JlibCompatibilityStatus status, string message, int providerAbi,
                string? providerVersion, string? providerPLibVersion, string? providerStaticId,
                int requiredAbi, string? minimumVersion, int providerCount, string? providerAssemblyName) {
            Status = status;
            Code = CodeFor(status);
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ProviderAbi = providerAbi;
            ProviderVersion = providerVersion;
            ProviderPLibVersion = providerPLibVersion;
            ProviderStaticId = providerStaticId;
            RequiredAbi = requiredAbi;
            MinimumVersion = string.IsNullOrWhiteSpace(minimumVersion) ? null : minimumVersion.Trim();
            ProviderCount = providerCount;
            ProviderAssemblyName = providerAssemblyName;
        }

        public override string ToString() => Message;

        private static string CodeFor(JlibCompatibilityStatus status) {
            switch (status) {
            case JlibCompatibilityStatus.Compatible:
                return "jlib.compatible";
            case JlibCompatibilityStatus.ProviderMissing:
                return "jlib.provider.missing";
            case JlibCompatibilityStatus.ProviderAmbiguous:
                return "jlib.provider.ambiguous";
            case JlibCompatibilityStatus.IdentityIncomplete:
                return "jlib.identity.incomplete";
            case JlibCompatibilityStatus.StaticIdMismatch:
                return "jlib.static-id.mismatch";
            case JlibCompatibilityStatus.ProtocolMismatch:
                return "jlib.protocol.mismatch";
            case JlibCompatibilityStatus.VersionUnreadable:
                return "jlib.version.unreadable";
            case JlibCompatibilityStatus.AbiMismatch:
                return "jlib.abi.mismatch";
            case JlibCompatibilityStatus.VersionOlderThanRequired:
                return "jlib.version.older";
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown Jlib compatibility status.");
            }
        }
    }

    /// <summary>Thrown when <see cref="JlibApi.RequireCompatibility"/> rejects the loaded provider.</summary>
    public sealed class JlibCompatibilityException : InvalidOperationException {
        public JlibCompatibility Result { get; }

        public JlibCompatibilityException(JlibCompatibility result)
            : base(MessageOf(result)) {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        private static string MessageOf(JlibCompatibility result) {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            return result.Message;
        }
    }

    /// <summary>
    /// Game-free rules for the Jlib provider identity. ABI must match exactly.
    /// Additive APIs stay on the same ABI and are selected with the library version.
    /// </summary>
    public static class JlibContract {
        public const int ExpectedAbi = 1;
        public const string ExpectedStaticId = "LIghtJUNction.Jlib";
        public const string ExpectedPLibVersion = "4.25.0.0";
        public const string ApiTypeName = "Jlib.Runtime.JlibApi";
        public const string ProviderAssemblyName = "Jlib";

        public static bool TryParseVersion(string? text, out Version? version) {
            version = null;
            if (string.IsNullOrWhiteSpace(text) || !Version.TryParse(text.Trim(), out Version parsed))
                return false;
            version = new Version(parsed.Major, parsed.Minor < 0 ? 0 : parsed.Minor,
                parsed.Build < 0 ? 0 : parsed.Build, parsed.Revision < 0 ? 0 : parsed.Revision);
            return true;
        }

        /// <summary>Compares one provider identity with the ABI and minimum version a consumer requires.</summary>
        public static JlibCompatibility Evaluate(int providerAbi, string? providerVersion,
                string? providerPLibVersion, string? providerStaticId, int requiredAbi,
                string? minimumVersion) {
            ValidateRequest(requiredAbi, minimumVersion);
            string described = Describe(providerStaticId, providerVersion, providerAbi, providerPLibVersion);
            if (!string.Equals(providerStaticId, ExpectedStaticId, StringComparison.Ordinal))
                return Result(JlibCompatibilityStatus.StaticIdMismatch, providerAbi, providerVersion,
                    providerPLibVersion, providerStaticId, requiredAbi, minimumVersion, 1, null,
                    described + " does not use static ID " + ExpectedStaticId + ".");
            if (!string.Equals(providerPLibVersion, ExpectedPLibVersion, StringComparison.Ordinal))
                return Result(JlibCompatibilityStatus.ProtocolMismatch, providerAbi, providerVersion,
                    providerPLibVersion, providerStaticId, requiredAbi, minimumVersion, 1, null,
                    described + " does not use PLib protocol " + ExpectedPLibVersion + ".");
            if (!TryParseVersion(providerVersion, out Version? parsedVersion) || parsedVersion == null)
                return Result(JlibCompatibilityStatus.VersionUnreadable, providerAbi, providerVersion,
                    providerPLibVersion, providerStaticId, requiredAbi, minimumVersion, 1, null,
                    described + " does not expose a dotted library version.");
            if (providerAbi != requiredAbi)
                return Result(JlibCompatibilityStatus.AbiMismatch, providerAbi, providerVersion,
                    providerPLibVersion, providerStaticId, requiredAbi, minimumVersion, 1, null,
                    described + " does not match required ABI " + requiredAbi + ".");
            if (!string.IsNullOrWhiteSpace(minimumVersion)) {
                if (!TryParseVersion(minimumVersion, out Version? requiredVersion) || requiredVersion == null)
                    throw new ArgumentException("The minimum version is not a dotted numeric version.",
                        nameof(minimumVersion));
                if (parsedVersion < requiredVersion)
                    return Result(JlibCompatibilityStatus.VersionOlderThanRequired, providerAbi, providerVersion,
                        providerPLibVersion, providerStaticId, requiredAbi, minimumVersion, 1, null,
                        described + " is older than required " + minimumVersion.Trim() + ".");
            }
            string floor = string.IsNullOrWhiteSpace(minimumVersion) ? "ABI " + requiredAbi + "." :
                "ABI " + requiredAbi + " and minimum " + minimumVersion.Trim() + ".";
            return Result(JlibCompatibilityStatus.Compatible, providerAbi, providerVersion,
                providerPLibVersion, providerStaticId, requiredAbi, minimumVersion, 1, null,
                described + " satisfies " + floor);
        }

        /// <summary>
        /// Finds already loaded <c>Jlib.Runtime.JlibApi</c> types and evaluates the single match.
        /// This reads constants by reflection, so it reports the provider that is loaded,
        /// not constants copied into the calling assembly.
        /// </summary>
        public static JlibCompatibility Inspect(IEnumerable<Assembly?>? assemblies, int requiredAbi,
                string? minimumVersion) {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));
            ValidateRequest(requiredAbi, minimumVersion);
            var matches = new List<Assembly>();
            foreach (var assembly in assemblies) {
                if (assembly == null)
                    continue;
                Type? type;
                try {
                    type = assembly.GetType(ApiTypeName, throwOnError: false);
                } catch (ReflectionTypeLoadException) {
                    continue;
                } catch (TypeLoadException) {
                    continue;
                } catch (BadImageFormatException) {
                    continue;
                }
                if (type != null)
                    matches.Add(assembly);
            }
            if (matches.Count == 0)
                return Result(JlibCompatibilityStatus.ProviderMissing, 0, null, null, null, requiredAbi,
                    minimumVersion, 0, null, "No loaded assembly exposes " + ApiTypeName + ".");
            if (matches.Count > 1)
                return Result(JlibCompatibilityStatus.ProviderAmbiguous, 0, null, null, null, requiredAbi,
                    minimumVersion, matches.Count, null, "Found " + matches.Count +
                    " loaded Jlib providers (" + Names(matches) + "). Enable one " + ExpectedStaticId + ".");
            var provider = matches[0];
            var api = provider.GetType(ApiTypeName, throwOnError: false);
            string? assemblyName = provider.GetName().Name;
            if (api == null || !TryReadInt32(api, "AbiVersion", out int abi) ||
                    !TryReadString(api, "Version", out string? version) ||
                    !TryReadString(api, "PLibVersion", out string? protocol) ||
                    !TryReadString(api, "StaticId", out string? staticId))
                return Result(JlibCompatibilityStatus.IdentityIncomplete, 0, null, null, null, requiredAbi,
                    minimumVersion, 1, assemblyName, "Loaded " + assemblyName +
                    " is missing a JlibApi identity constant.");
            var evaluated = Evaluate(abi, version, protocol, staticId, requiredAbi, minimumVersion);
            return Result(evaluated.Status, evaluated.ProviderAbi, evaluated.ProviderVersion,
                evaluated.ProviderPLibVersion, evaluated.ProviderStaticId, requiredAbi, minimumVersion,
                1, assemblyName, evaluated.Message);
        }

        private static void ValidateRequest(int requiredAbi, string? minimumVersion) {
            if (requiredAbi < 0)
                throw new ArgumentOutOfRangeException(nameof(requiredAbi), requiredAbi,
                    "The required ABI cannot be negative.");
            if (!string.IsNullOrWhiteSpace(minimumVersion) && !TryParseVersion(minimumVersion, out _))
                throw new ArgumentException("The minimum version is not a dotted numeric version.",
                    nameof(minimumVersion));
        }

        private static bool TryReadInt32(Type type, string name, out int value) {
            value = 0;
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null || field.FieldType != typeof(int))
                return false;
            if (field.GetValue(null) is int number) {
                value = number;
                return true;
            }
            return false;
        }

        private static bool TryReadString(Type type, string name, out string? value) {
            value = null;
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null || field.FieldType != typeof(string))
                return false;
            if (field.GetValue(null) is string text && text.Length > 0) {
                value = text;
                return true;
            }
            return false;
        }

        private static string Names(List<Assembly> assemblies) {
            int shown = assemblies.Count < 4 ? assemblies.Count : 4;
            var names = new string[shown];
            for (int i = 0; i < shown; i++)
                names[i] = assemblies[i].GetName().Name ?? "?";
            return string.Join(", ", names);
        }

        private static string Describe(string? staticId, string? version, int abi, string? protocol) =>
            "Jlib provider " + (string.IsNullOrEmpty(staticId) ? "(no static ID)" : staticId) + " " +
            (string.IsNullOrEmpty(version) ? "(no version)" : version) + " (ABI " + abi + ", protocol " +
            (string.IsNullOrEmpty(protocol) ? "(none)" : protocol) + ")";

        private static JlibCompatibility Result(JlibCompatibilityStatus status, int providerAbi,
                string? providerVersion, string? providerPLibVersion, string? providerStaticId,
                int requiredAbi, string? minimumVersion, int providerCount, string? providerAssemblyName,
                string message) => new JlibCompatibility(status, message, providerAbi, providerVersion,
                    providerPLibVersion, providerStaticId, requiredAbi, minimumVersion, providerCount,
                    providerAssemblyName);
    }
}
