/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using TMPro;
using UnityEngine;

namespace PeterHan.PLib.Options {
    /// <summary>
    /// An options entry which represents a sensitive string or token and displays a password-masked text field.
    /// </summary>
    public class MaskedStringOptionsEntry : StringOptionsEntry {
        public MaskedStringOptionsEntry(string field, IOptionSpec spec) : base(field, spec) { }

        public MaskedStringOptionsEntry(string field)
            : base(field, new OptionAttribute(field ?? "Password", string.Empty, "Security")) { }

        public override GameObject GetUIComponent() {
            GameObject field = base.GetUIComponent();
            TMP_InputField input = field.GetComponentInChildren<TMP_InputField>();
            if (input == null)
                throw new InvalidOperationException("PLib did not create the text input field.");

            input.contentType = TMP_InputField.ContentType.Password;
            input.ForceLabelUpdate();
            return field;
        }
    }
}
