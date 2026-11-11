#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Jlib.Multicomponent;

namespace Jlib.Runtime {
    /// <summary>
    /// Main-thread GasPressure publication cache. Install during mod loading.
    /// GasColour, external textures and TextureLerper animation remain independent.
    /// </summary>
    public static class PropertyTextureRevisionCache {
        public struct Key {
            public object Provider, Game, World, Manager, Buffer, Texture;
            public ulong Revision;
            public int WorldId, X0, Y0, X1, Y1;
            public float PressureMin, PressureMax, MinVisibility;

            internal bool Same(Key other) {
                return ReferenceEquals(Provider, other.Provider) && Revision == other.Revision
                    && ReferenceEquals(Game, other.Game) && ReferenceEquals(World, other.World)
                    && ReferenceEquals(Manager, other.Manager) && ReferenceEquals(Buffer, other.Buffer)
                    && ReferenceEquals(Texture, other.Texture) && WorldId == other.WorldId
                    && X0 == other.X0 && Y0 == other.Y0 && X1 == other.X1 && Y1 == other.Y1
                    && PressureMin == other.PressureMin && PressureMax == other.PressureMax
                    && MinVisibility == other.MinVisibility;
            }
        }
        public struct Scope {
            internal Key Key;
            internal ulong Generation;
            internal bool Pending;
        }
        private static Key completed;
        private static bool valid;
        private static ulong generation;
        public static string LastFailure { get; private set; }

        /// <summary>Infrastructure entry point. Invalid inputs always execute the original.</summary>
        public static bool Begin(Key key, out Scope state) {
            state = default(Scope);
            try {
                var provider = key.Provider as IMulticomponentGridRevisionProvider;
                if (provider == null || key.Revision == 0 || provider.GridRevision != key.Revision
                    || key.Game == null || key.World == null || key.Manager == null
                    || key.Buffer == null || key.Texture == null
                    || key.X1 < key.X0 || key.Y1 < key.Y0) {
                    Invalidate();
                    return true;
                }
                if (valid && completed.Same(key)) return false;
                Invalidate();
                state.Key = key;
                state.Generation = generation;
                state.Pending = true;
            } catch (Exception) { Invalidate(); }
            return true;
        }

        public static void Complete(bool ranOriginal, Scope state) {
            // A reset/destroy invoked during the original must not resurrect its
            // old colony references when the postfix subsequently completes.
            if (!state.Pending || state.Generation != generation) return;
            try {
                if (ranOriginal && ((IMulticomponentGridRevisionProvider)state.Key.Provider).GridRevision
                        == state.Key.Revision) {
                    completed = state.Key;
                    valid = true;
                } else Invalidate();
            } catch (Exception) { Invalidate(); }
        }

        public static void Invalidate() {
            unchecked { generation++; }
            valid = false;
            completed = default(Key);
        }

        public static Exception Finish(Exception error) {
            if (error != null) Invalidate();
            return error;
        }

        // Shared by production and host fixtures. Only the skipVisibility DynamicMethod
        // dereferences fields declared on the private nested struct. The ordinary
        // assembly wrapper forwards its typed arguments to a rooted delegate.
        internal static Type InstallTarget(Harmony harmony, MethodInfo target, MethodInfo before,
                MethodInfo[] invalidationTargets) {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (invalidationTargets == null) throw new ArgumentNullException(nameof(invalidationTargets));
            foreach (var lifecycle in invalidationTargets) {
                if (lifecycle == null) throw new MissingMethodException("PropertyTextures lifecycle entry is unavailable");
            }
            var parameters = target.GetParameters();
            if (target.IsStatic || target.ReturnType != typeof(void)
                || parameters.Length != 5 || !parameters[0].ParameterType.IsByRef)
                throw new ArgumentException("Expected UpdateProperty(ref properties, int, int, int, int)");
            var properties = parameters[0].ParameterType.GetElementType();
            var property = properties.GetField("simProperty", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var external = properties.GetField("updatedExternally", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var inputs = before.GetParameters();
            if (!properties.IsValueType || property == null || !property.FieldType.IsEnum
                || external == null || external.FieldType != typeof(bool)
                || !before.IsStatic || before.ReturnType != typeof(bool) || inputs.Length != 10
                || !inputs[0].ParameterType.IsAssignableFrom(target.DeclaringType)
                || inputs[1].ParameterType != property.FieldType || inputs[2].ParameterType != typeof(bool)
                || inputs[8].ParameterType != typeof(float) || inputs[9].ParameterType != typeof(Scope).MakeByRefType())
                throw new ArgumentException("Unsupported property texture prefix layout");
            for (int i = 1; i < 5; i++) {
                if (parameters[i].ParameterType != typeof(int) || inputs[i + 2].ParameterType != typeof(int))
                    throw new ArgumentException("Expected integer extents");
            }
            var pressure = AccessTools.Field(target.DeclaringType, "PressureRange");
            var visibility = AccessTools.Field(target.DeclaringType, "MinPressureVisibility");
            if (pressure == null || pressure.FieldType != inputs[7].ParameterType
                || visibility == null || visibility.FieldType != typeof(float))
                throw new ArgumentException("Unsupported pressure settings layout");
            var prefixTypes = new[] { inputs[0].ParameterType, properties.MakeByRefType(), typeof(int),
                typeof(int), typeof(int), typeof(int), inputs[7].ParameterType, typeof(float),
                typeof(Scope).MakeByRefType() };
            var module = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("Jlib.PropertyTextureRevisionCache.Patches"), AssemblyBuilderAccess.Run)
                .DefineDynamicModule("Patches");
            var delegateBuilder = module.DefineType("PropertyReader", TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(MulticastDelegate));
            var constructor = delegateBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) });
            constructor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
            var invoke = delegateBuilder.DefineMethod("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot | MethodAttributes.Virtual, typeof(bool), prefixTypes);
            invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
            var delegateType = delegateBuilder.CreateType();
            var reader = new DynamicMethod("ReadPrivateTextureProperties", typeof(bool), prefixTypes, target.Module, true);
            var il = reader.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldfld, property);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldfld, external);
            for (short i = 2; i <= 8; i++) il.Emit(OpCodes.Ldarg, i);
            il.Emit(OpCodes.Call, before);
            il.Emit(OpCodes.Ret);

            var wrapper = module.DefineType("GasPressurePatch", TypeAttributes.Public);
            var readerField = wrapper.DefineField("Reader", delegateType, FieldAttributes.Public | FieldAttributes.Static);
            var method = wrapper.DefineMethod("Prefix", MethodAttributes.Public | MethodAttributes.Static,
                typeof(bool), prefixTypes);
            string[] names = { "__instance", "__0", "__1", "__2", "__3", "__4",
                "___PressureRange", "___MinPressureVisibility", "__state" };
            for (int i = 0; i < names.Length; i++)
                method.DefineParameter(i + 1, i == 8 ? ParameterAttributes.Out : ParameterAttributes.None, names[i]);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, readerField);
            for (short i = 0; i <= 8; i++) il.Emit(OpCodes.Ldarg, i);
            il.Emit(OpCodes.Callvirt, delegateType.GetMethod("Invoke"));
            il.Emit(OpCodes.Ret);
            method = wrapper.DefineMethod("Postfix", MethodAttributes.Public | MethodAttributes.Static,
                typeof(void), new[] { typeof(bool), typeof(Scope) });
            method.DefineParameter(1, ParameterAttributes.None, "__runOriginal");
            method.DefineParameter(2, ParameterAttributes.None, "__state");
            il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(PropertyTextureRevisionCache).GetMethod(nameof(Complete)));
            il.Emit(OpCodes.Ret);
            method = wrapper.DefineMethod("Finalizer", MethodAttributes.Public | MethodAttributes.Static,
                typeof(Exception), new[] { typeof(Exception) });
            method.DefineParameter(1, ParameterAttributes.None, "__exception");
            il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(PropertyTextureRevisionCache).GetMethod(nameof(Finish)));
            il.Emit(OpCodes.Ret);
            // A wrapper-specific MethodInfo permits rollback without removing another
            // owner's invalidation hook that happens to call the same public helper.
            method = wrapper.DefineMethod("Invalidate", MethodAttributes.Public | MethodAttributes.Static,
                typeof(void), Type.EmptyTypes);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Call, typeof(PropertyTextureRevisionCache).GetMethod(nameof(Invalidate)));
            il.Emit(OpCodes.Ret);
            var type = wrapper.CreateType();
            type.GetField("Reader").SetValue(null, reader.CreateDelegate(delegateType));
            var prefix = type.GetMethod("Prefix");
            var postfix = type.GetMethod("Postfix");
            var finalizer = type.GetMethod("Finalizer");
            var invalidate = type.GetMethod("Invalidate");
            var attempted = new List<KeyValuePair<MethodBase, MethodInfo>> {
                new KeyValuePair<MethodBase, MethodInfo>(target, prefix),
                new KeyValuePair<MethodBase, MethodInfo>(target, postfix),
                new KeyValuePair<MethodBase, MethodInfo>(target, finalizer)
            };
            try {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix) { priority = Priority.Last },
                    postfix: new HarmonyMethod(postfix) { priority = Priority.Last },
                    finalizer: new HarmonyMethod(finalizer));
                foreach (var lifecycle in invalidationTargets) {
                    attempted.Add(new KeyValuePair<MethodBase, MethodInfo>(lifecycle, invalidate));
                    harmony.Patch(lifecycle, prefix: new HarmonyMethod(invalidate) { priority = Priority.First });
                }
            } catch (Exception installError) {
                // Include the failing Patch attempt, which may already have registered
                // its metadata. Attempt every rollback even if one Unpatch fails.
                var errors = new List<Exception> { installError };
                for (int i = attempted.Count - 1; i >= 0; i--) {
                    try { harmony.Unpatch(attempted[i].Key, attempted[i].Value); }
                    catch (Exception rollbackError) { errors.Add(rollbackError); }
                }
                Invalidate();
                if (errors.Count > 1) throw new AggregateException("Property texture hook rollback failed", errors);
                throw;
            }
            Invalidate();
            return type;
        }

#if !JLIB_HOST_TESTS
        private static bool installed;
        /// <summary>Returns false on unsupported layouts or patch failure, preserving normal updates.</summary>
        public static bool Install(Harmony harmony) {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            if (installed) return true;
            try {
                var properties = typeof(PropertyTextures).GetNestedType("TextureProperties", BindingFlags.NonPublic);
                if (properties == null) throw new MissingMemberException("PropertyTextures.TextureProperties");
                var target = AccessTools.Method(typeof(PropertyTextures), "UpdateProperty",
                    new[] { properties.MakeByRefType(), typeof(int), typeof(int), typeof(int), typeof(int) });
                // Resolve the full lifecycle before installing any hook. All four
                // entries are installed or rolled back together on this main thread.
                var lifecycle = new[] {
                    AccessTools.DeclaredMethod(typeof(PropertyTextures), "DestroyInstance", Type.EmptyTypes),
                    AccessTools.DeclaredMethod(typeof(PropertyTextures), "OnReset", new[] { typeof(object) }),
                    AccessTools.DeclaredMethod(typeof(PropertyTextures), "OnShadersReloaded", Type.EmptyTypes)
                };
                InstallTarget(harmony, target, typeof(PropertyTextureRevisionCache).GetMethod(nameof(Before)), lifecycle);
                installed = true;
                LastFailure = null;
                return true;
            } catch (Exception error) {
                Invalidate();
                LastFailure = error.ToString();
                return false;
            }
        }

        public static bool Before(PropertyTextures manager, PropertyTextures.Property property, bool external,
                int x0, int y0, int x1, int y1, UnityEngine.Vector2 pressureRange,
                float minVisibility, out Scope state) {
            state = default(Scope);
            if (property != PropertyTextures.Property.GasPressure) return true;
            try {
                var provider = MulticomponentCellAccess.ProviderIdentity as IMulticomponentGridRevisionProvider;
                var game = Game.Instance;
                var cluster = ClusterManager.Instance;
                if (external || provider == null || !MulticomponentCellAccess.IsReady
                    || game == null || game.IsLoading() || cluster == null
                    || !ReferenceEquals(manager, PropertyTextures.instance)) {
                    Invalidate();
                    return true;
                }
                var buffer = manager.textureBuffers[(int)property];
                return Begin(new Key {
                    Provider = provider, Revision = provider.GridRevision,
                    Game = game, World = cluster.activeWorld, WorldId = cluster.activeWorldId,
                    Manager = manager, Buffer = buffer, Texture = buffer.texture,
                    X0 = x0, Y0 = y0, X1 = x1, Y1 = y1,
                    PressureMin = pressureRange.x, PressureMax = pressureRange.y, MinVisibility = minVisibility
                }, out state);
            } catch (Exception) {
                Invalidate();
                return true;
            }
        }
#endif
    }
}
