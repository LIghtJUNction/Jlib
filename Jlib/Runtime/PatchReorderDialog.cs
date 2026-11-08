#if !JLIB_HOST_TESTS
/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using UnityEngine;

namespace Jlib.Runtime {
    /// <summary>
    /// Draggable window for inspecting Harmony hooks, adjusting declared patch priorities,
    /// and viewing live diagnostics. Explicit ordering constraints remain in effect.
    /// </summary>
    public static class PatchReorderDialog {
        private static GameObject activeDialog;

        /// <summary>Opens the draggable Harmony Patch Reorder and Debug dialog.</summary>
        public static void Show() {
            try {
                if (activeDialog != null) {
                    UnityEngine.Object.Destroy(activeDialog);
                    activeDialog = null;
                }

                var dialog = new PDialog("JlibPatchReorderDialog") {
                    Title = "Jlib 补丁优先级与实时调试器 / Harmony Priorities & Debugger",
                    Draggable = true,
                    Size = new Vector2(880f, 620f),
                    MaxSize = new Vector2(1100f, 800f),
                    DialogBackColor = PUITuning.Colors.DialogDarkBackground
                };

                dialog.Body.Direction = PanelDirection.Vertical;
                dialog.Body.Spacing = 8;
                dialog.Body.Margin = new RectOffset(12, 12, 12, 12);

                // Header instructions
                dialog.Body.AddChild(new PLabel("Instructions") {
                    Text = "每个阶段按声明优先级从高到低列出；最终执行顺序还受 before/after 约束及注册顺序影响。\n" +
                           "按钮调整 Priority 后重新应用补丁，并保留模组声明的排序约束。",
                    TextStyle = PUITuning.Fonts.TextLightStyle
                });

                // Scroll panel for hooked methods
                var methodListPanel = new PPanel("MethodListPanel") {
                    Direction = PanelDirection.Vertical,
                    Spacing = 6,
                    FlexSize = Vector2.one
                };

                PopulateMethodList(methodListPanel);

                var scrollPane = new PScrollPane("MethodScrollPane") {
                    Child = methodListPanel,
                    ScrollHorizontal = false,
                    ScrollVertical = true,
                    FlexSize = Vector2.one,
                    AlwaysShowVertical = true
                };

                dialog.Body.AddChild(scrollPane);

                // Footer action buttons using PDialog.AddButton
                dialog.AddButton("refresh", "刷新列表 / Refresh", "");
                dialog.AddButton("dumplog", "导出诊断报告 / Dump Log", "");
                dialog.AddButton("close", "关闭 / Close", "");

                dialog.DialogClosed = action => {
                    if (action == "refresh") {
                        Show();
                    } else if (action == "dumplog") {
                        JlibDiagnostics.DumpReport();
                    }
                };

                activeDialog = dialog.Build();
                if (activeDialog != null) {
                    activeDialog.SetActive(true);
                }
            } catch (Exception ex) {
                PUtil.LogError("Failed to open PatchReorderDialog: " + ex);
            }
        }

        private static void PopulateMethodList(PPanel container) {
            var hooked = JlibDiagnostics.GetHookedMethods();
            if (hooked.Count == 0) {
                container.AddChild(new PLabel("EmptyLabel") {
                    Text = "当前未检测到生效中的 Harmony 补丁方法。",
                    TextStyle = PUITuning.Fonts.TextLightStyle
                });
                return;
            }

            foreach (var hook in hooked) {
                var methodBox = new PPanel("MethodBox_" + hook.MethodName) {
                    Direction = PanelDirection.Vertical,
                    Spacing = 4,
                    Margin = new RectOffset(8, 8, 8, 8),
                    BackColor = new Color(0.18f, 0.20f, 0.24f, 0.95f),
                    FlexSize = new Vector2(1f, 0f)
                };

                // Method Title
                int totalPatches = hook.Prefixes.Count + hook.Transpilers.Count + hook.Postfixes.Count + hook.Finalizers.Count;
                methodBox.AddChild(new PLabel("MethodTitle") {
                    Text = $"[方法] {hook.MethodName} (补丁总数: {totalPatches})",
                    TextStyle = PUITuning.Fonts.TextLightStyle
                });

                // Prefixes
                AppendPatchRows(methodBox, hook.OriginalMethod, "Prefix (前置，先于原逻辑执行)", hook.Prefixes);
                // Transpilers
                AppendPatchRows(methodBox, hook.OriginalMethod, "Transpiler (IL 转译字节码)", hook.Transpilers);
                // Postfixes
                AppendPatchRows(methodBox, hook.OriginalMethod, "Postfix (后置，后于原逻辑执行)", hook.Postfixes);

                container.AddChild(methodBox);
            }
        }

        private static void AppendPatchRows(
            PPanel parent,
            MethodBase originalMethod,
            string categoryLabel,
            IReadOnlyList<PatchEntryInfo> patches) {
            if (patches.Count == 0) return;

            parent.AddChild(new PLabel("CategoryHeader") {
                Text = $"  ▶ {categoryLabel}:",
                TextStyle = PUITuning.Fonts.TextLightStyle
            });

            for (int i = 0; i < patches.Count; i++) {
                var patch = patches[i];
                var row = new PPanel($"PatchRow_{patch.Owner}_{i}") {
                    Direction = PanelDirection.Horizontal,
                    Spacing = 8,
                    Margin = new RectOffset(16, 8, 2, 2),
                    FlexSize = new Vector2(1f, 0f),
                    Alignment = TextAnchor.MiddleLeft
                };

                row.AddChild(new PLabel("PatchDesc") {
                    Text = $"#{i + 1} [{patch.PatchType}] Mod: {patch.Owner} | 声明优先级: {patch.Priority}\n   方法: {patch.MethodSignature}",
                    TextStyle = PUITuning.Fonts.TextLightStyle,
                    FlexSize = new Vector2(1f, 0f)
                });

                // Increase declared priority
                row.AddChild(new PButton("MoveUp") {
                    Text = "▲ +50",
                    ToolTip = "增加声明优先级（+50）；实际顺序仍受 before/after 约束",
                    OnClick = _ => {
                        if (JlibDiagnostics.AdjustPatchPriority(originalMethod, patch.PatchMethod, patch.PatchType, patch.Owner, +50, out string msg)) {
                            Show();
                        } else {
                            PUIElements.ShowMessageDialog(null, msg);
                        }
                    }
                }.SetKleiPinkStyle());

                // Decrease declared priority
                row.AddChild(new PButton("MoveDown") {
                    Text = "▼ -50",
                    ToolTip = "降低声明优先级（-50）；实际顺序仍受 before/after 约束",
                    OnClick = _ => {
                        if (JlibDiagnostics.AdjustPatchPriority(originalMethod, patch.PatchMethod, patch.PatchType, patch.Owner, -50, out string msg)) {
                            Show();
                        } else {
                            PUIElements.ShowMessageDialog(null, msg);
                        }
                    }
                }.SetKleiBlueStyle());

                parent.AddChild(row);
            }
        }
    }
}
#endif
