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
    /// Interactive draggable window allowing players and modders to inspect active
    /// Harmony hooks, view caller pipelines, dynamically adjust patch execution and override
    /// order, and trigger live debugging diagnostics.
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
                    Title = "Jlib 补丁覆盖流水线与实时调试器 / Harmony Pipeline & Debugger",
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
                    Text = "【动态补丁覆盖流水线调试】\n" +
                           "系统已动态枚举当前游戏中所有活动的 Harmony 挂钩。Prefix 按优先级从大到小依次先于原方法执行；\n" +
                           "点击 [▲ 提前] 或 [▼ 延后] 即可实时重置补丁 Priority 并即时生效，排查与解决 Mod 冲突！",
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
                    Text = $"#{i + 1} [{patch.PatchType}] Mod: {patch.Owner} | 优先级: {patch.Priority}\n   方法: {patch.MethodSignature}",
                    TextStyle = PUITuning.Fonts.TextLightStyle,
                    FlexSize = new Vector2(1f, 0f)
                });

                // Move Up button
                row.AddChild(new PButton("MoveUp") {
                    Text = "▲ 提前",
                    ToolTip = "增加补丁优先级（+50），使其在流水线中更早执行",
                    OnClick = _ => {
                        if (JlibDiagnostics.AdjustPatchPriority(originalMethod, patch.PatchMethod, patch.PatchType, patch.Owner, +50, out string msg)) {
                            Show();
                        }
                    }
                }.SetKleiPinkStyle());

                // Move Down button
                row.AddChild(new PButton("MoveDown") {
                    Text = "▼ 延后",
                    ToolTip = "降低补丁优先级（-50），使其在流水线中更晚执行",
                    OnClick = _ => {
                        if (JlibDiagnostics.AdjustPatchPriority(originalMethod, patch.PatchMethod, patch.PatchType, patch.Owner, -50, out string msg)) {
                            Show();
                        }
                    }
                }.SetKleiBlueStyle());

                parent.AddChild(row);
            }
        }
    }
}
#endif
