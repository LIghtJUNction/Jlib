/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Collections.Concurrent;
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
	}
}
