/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;

namespace PeterHan.PLib.Options {
    /// <summary>
    /// Denotes a string option property that should be rendered as a masked password input field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class PasswordOptionAttribute : DynamicOptionAttribute {
        public PasswordOptionAttribute(string category = null) : base(typeof(MaskedStringOptionsEntry), category) { }
    }
}
