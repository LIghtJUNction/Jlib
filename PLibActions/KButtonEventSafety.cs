/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Reflection;

namespace PeterHan.PLib.Actions {
    /// <summary>
    /// Guard methods to safely access and extend KButtonEvent action buffers,
    /// preventing IndexOutOfRangeException when actions registered by mods exceed Action.NumActions.
    /// </summary>
    public static class KButtonEventSafety {
        private static readonly FieldInfo IsActionField = typeof(KButtonEvent).GetField("mIsAction",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private const int MinimumActionArrayLength = (int)Action.NumActions + 1;

        public static bool TryGetActionArray(KButtonEvent buttonEvent, out bool[] isAction) {
            isAction = IsActionField?.GetValue(buttonEvent) as bool[];
            return isAction != null;
        }

        public static void EnsureActionArrayCapacity(KButtonEvent buttonEvent, int minimumLength = MinimumActionArrayLength) {
            if (buttonEvent == null || !TryGetActionArray(buttonEvent, out var isAction))
                return;

            int targetLength = Math.Max(MinimumActionArrayLength, minimumLength);
            if (isAction.Length >= targetLength)
                return;

            var expanded = new bool[targetLength];
            Array.Copy(isAction, expanded, isAction.Length);
            IsActionField.SetValue(buttonEvent, expanded);
        }

        public static bool SafeIsAction(KButtonEvent buttonEvent, Action action) {
            if (buttonEvent == null)
                return false;

            if (TryGetActionArray(buttonEvent, out var isAction)) {
                var index = (int)action;
                return index >= 0 && index < isAction.Length && isAction[index];
            }

            return buttonEvent.GetAction() == action;
        }

        public static bool SafeTryConsume(KButtonEvent buttonEvent, Action action) {
            if (buttonEvent == null || buttonEvent.Consumed)
                return false;

            if (action != Action.NumActions && SafeIsAction(buttonEvent, action))
                buttonEvent.Consumed = true;

            return buttonEvent.Consumed;
        }

        public static int GetActionArrayLength(KButtonEvent buttonEvent) {
            return TryGetActionArray(buttonEvent, out var isAction) ? isAction.Length : 0;
        }
    }
}
