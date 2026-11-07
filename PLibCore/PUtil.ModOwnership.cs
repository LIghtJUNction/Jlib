/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.PLib.Core {
	public static partial class PUtil {
		private static readonly ConcurrentDictionary<Assembly, KMod.Mod>
			registeredModAssemblies = new ConcurrentDictionary<Assembly, KMod.Mod>();

		/// <summary>
		/// Associates a dynamically loaded implementation assembly with its owning mod.
		/// Register before the implementation reads configuration or registers options.
		/// Repeating the same association is safe; reassignment to another mod is rejected.
		/// </summary>
		public static void RegisterModAssembly(Assembly implementation, KMod.Mod owner) {
			if (implementation == null)
				throw new ArgumentNullException(nameof(implementation));
			if (owner == null)
				throw new ArgumentNullException(nameof(owner));
			if (!ReferenceEquals(registeredModAssemblies.GetOrAdd(implementation, owner), owner))
				throw new InvalidOperationException("The assembly is already registered to another mod.");
		}

		/// <summary>Finds the owning mod, including implementations registered after startup.</summary>
		public static KMod.Mod GetModInfo(Assembly implementation) {
			if (implementation == null)
				throw new ArgumentNullException(nameof(implementation));
			if (registeredModAssemblies.TryGetValue(implementation, out KMod.Mod owner))
				return owner;
			var mods = Global.Instance?.modManager?.mods;
			if (mods != null)
				foreach (var mod in mods) {
					var assemblies = mod?.loaded_mod_data?.dlls;
					if (assemblies != null)
						foreach (var candidate in assemblies)
							if (ReferenceEquals(candidate, implementation))
								return mod;
				}
			return null;
		}

		internal static string GetRegisteredModPath(Assembly implementation) {
			return registeredModAssemblies.TryGetValue(implementation, out KMod.Mod owner) ?
				owner.ContentPath : null;
		}

		/// <summary>
		/// Names of mods that use this library. Includes explicit registrations and loaded
		/// assemblies whose references name Jlib. The Jlib provider itself is omitted.
		/// </summary>
		public static IReadOnlyList<string> DependentMods() {
			var labels = new SortedSet<string>(StringComparer.Ordinal);
			foreach (var owner in registeredModAssemblies.Values)
				AddLabel(labels, owner);
			var mods = Global.Instance?.modManager?.mods;
			if (mods != null)
				foreach (var mod in mods) {
					var assemblies = mod?.loaded_mod_data?.dlls;
					if (assemblies == null)
						continue;
					foreach (var candidate in assemblies)
						if (DependsOnJlib(candidate)) {
							AddLabel(labels, mod);
							break;
						}
				}
			var result = new string[labels.Count];
			labels.CopyTo(result);
			return result;
		}

		private static void AddLabel(ISet<string> labels, KMod.Mod mod) {
			if (mod == null)
				return;
			string staticId = Text(mod, "staticID");
			if (string.Equals(staticId, Jlib.Runtime.JlibContract.ExpectedStaticId, StringComparison.Ordinal))
				return;
			string title = Text(mod, "title");
			string contentPath = mod.ContentPath == null ? "" : mod.ContentPath.Trim();
			string label = staticId.Length > 0 && title.Length > 0 ? title + " (" + staticId + ")" :
				(staticId.Length > 0 ? staticId : (title.Length > 0 ? title : contentPath));
			if (!string.IsNullOrEmpty(label))
				labels.Add(label);
		}

		private static bool DependsOnJlib(Assembly assembly) {
			if (assembly == null || assembly == typeof(PUtil).Assembly)
				return false;
			AssemblyName[] references;
			try {
				references = assembly.GetReferencedAssemblies();
			} catch (NotSupportedException) {
				return false;
			}
			foreach (var reference in references)
				if (reference.Name == "Jlib")
					return true;
			return false;
		}

		private static string Text(object target, string name) {
			var type = target.GetType();
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
			var value = (type.GetProperty(name, flags)?.GetValue(target) ??
				type.GetField(name, flags)?.GetValue(target)) as string;
			return value?.Trim() ?? "";
		}
	}
}
