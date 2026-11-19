/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Jlib.Runtime {
    /// <summary>
    /// Resolves the verified property-texture callback layouts without executing
    /// the game's static initializer or redirecting a third-party replacement.
    /// </summary>
    public static class PropertyTextureCallbackTarget {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Finds a legacy static method or the owned method assigned to a U60
        /// callback field. Unknown signatures and initializer layouts are refused.
        /// </summary>
        /// <param name="owner">The native property-texture declaring type.</param>
        /// <param name="callbackName">The native method or callback field name.</param>
        /// <param name="textureRegionType">The native TextureRegion value type.</param>
        /// <param name="target">The verified callback body, or null on refusal.</param>
        /// <param name="reason">An empty string on success, otherwise a diagnostic code.</param>
        /// <returns>True only when one compatible native callback body was identified.</returns>
        public static bool TryResolve(Type owner, string callbackName, Type textureRegionType,
                out MethodInfo target, out string reason) {
            target = null;
            reason = "missing_callback";
            if (owner == null || textureRegionType == null || string.IsNullOrEmpty(callbackName))
                return false;
            var parameters = new[] { textureRegionType, typeof(int), typeof(int), typeof(int), typeof(int) };
            try {
                var legacy = owner.GetMethods(Members).Where(method => method.Name == callbackName &&
                    Matches(method, parameters)).ToArray();
                if (legacy.Length == 1) {
                    target = legacy[0];
                    reason = "";
                    return true;
                }
                if (legacy.Length > 1) {
                    reason = "ambiguous_method";
                    return false;
                }

                var field = owner.GetField(callbackName, Members);
                if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType) ||
                        !Matches(field.FieldType.GetMethod("Invoke"), parameters)) {
                    reason = "unsupported_callback_signature";
                    return false;
                }
                var initializer = owner.TypeInitializer;
                if (initializer == null) {
                    reason = "missing_callback_initializer";
                    return false;
                }

                // Do not read the field: GetValue would execute the game cctor.
                // The verified U60 assignment is ldftn, newobj delegate, stsfld.
                // Multicast, factory-generated and multiple assignments are refused.
                var code = PatchProcessor.GetOriginalInstructions(initializer)
                    .Where(instruction => instruction.opcode != OpCodes.Nop).ToArray();
                int store = -1;
                for (int i = 0; i < code.Length; i++) {
                    if (code[i].opcode != OpCodes.Stsfld || !(code[i].operand is FieldInfo assigned) ||
                            assigned.Module != field.Module || assigned.MetadataToken != field.MetadataToken)
                        continue;
                    if (store >= 0) {
                        reason = "multiple_callback_assignments";
                        return false;
                    }
                    store = i;
                }
                if (store < 2 || code[store - 1].opcode != OpCodes.Newobj ||
                        !(code[store - 1].operand is ConstructorInfo constructor) ||
                        constructor.DeclaringType != field.FieldType || code[store - 2].opcode != OpCodes.Ldftn ||
                        !(code[store - 2].operand is MethodInfo body)) {
                    reason = "unsupported_callback_initializer";
                    return false;
                }
                if (!Matches(body, parameters) || !OwnedBy(body.DeclaringType, owner)) {
                    reason = "unsupported_callback_body";
                    return false;
                }
                target = body;
                reason = "";
                return true;
            } catch (Exception error) {
                reason = "callback_inspection_failed:" + error.GetType().Name;
                return false;
            }
        }

        private static bool Matches(MethodInfo method, Type[] parameters) {
            if (method == null || method.ReturnType != typeof(void) || method.ContainsGenericParameters ||
                    method.IsAbstract)
                return false;
            var actual = method.GetParameters();
            if (actual.Length != parameters.Length)
                return false;
            for (int i = 0; i < actual.Length; i++)
                if (actual[i].ParameterType != parameters[i])
                    return false;
            return true;
        }

        private static bool OwnedBy(Type declaringType, Type owner) {
            if (declaringType == null || declaringType.Assembly != owner.Assembly)
                return false;
            for (var type = declaringType; type != null; type = type.DeclaringType)
                if (type == owner)
                    return true;
            return false;
        }
    }
}
