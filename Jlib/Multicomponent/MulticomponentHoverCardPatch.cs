using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace Jlib.Multicomponent {
    [HarmonyPatch(typeof(SelectToolHoverTextCard), nameof(SelectToolHoverTextCard.UpdateHoverElements))]
    public static class MulticomponentHoverCardPatch {
        private static readonly FieldInfo CurrentPos = AccessTools.Field(typeof(HoverTextDrawer), "currentPos");
        private static readonly FieldInfo RootPos = AccessTools.Field(typeof(HoverTextDrawer), "rootPos");
        private static readonly FieldInfo FirstBar = AccessTools.Field(typeof(HoverTextDrawer), "firstShadowBar");
        private static readonly FieldInfo MaxShadowX = AccessTools.Field(typeof(HoverTextDrawer), "maxShadowX");

        private static int _lastDrawnFrame = -1;
        private static int _lastDrawnCell = -1;

        public static bool DrawCardsOrTitle(HoverTextDrawer drawer, string title, TextStyleSetting style) {
            int cell = Grid.PosToCell(Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos()));
            if (CurrentPos == null || RootPos == null || FirstBar == null || MaxShadowX == null ||
                !Grid.IsValidCell(cell) || !MulticomponentCellAccess.TryGetCell(cell, out var snapshot) ||
                snapshot.ComponentCount < 2 || snapshot.TotalMass <= 0f) {
                drawer.DrawText(title, style);
                return false;
            }

            if (Time.frameCount == _lastDrawnFrame && cell == _lastDrawnCell) {
                // 当前帧已绘制过该格子的多组分卡片，坚决不重复绘制！
                return true;
            }
            _lastDrawnFrame = Time.frameCount;
            _lastDrawnCell = cell;

            // 关闭调用方此前开启的外层 ShadowBar，避免卡片阴影条嵌套或重叠
            drawer.EndShadowBar();

            Vector2 originalRoot = (Vector2)RootPos.GetValue(drawer);
            Vector2 start = (Vector2)CurrentPos.GetValue(drawer);
            start.x = originalRoot.x;
            float x = start.x, bottom = start.y;
            for (int i = 0; i < snapshot.ComponentCount; i++) {
                var item = snapshot.Components[i];
                if (item.Element == null || item.Mass <= 0f) continue;
                float fraction = item.Mass / snapshot.TotalMass;
                int width = Math.Max(210, (int)(460f * fraction));
                var cardRoot = new Vector2(x, start.y);
                RootPos.SetValue(drawer, cardRoot);
                CurrentPos.SetValue(drawer, cardRoot);
                FirstBar.SetValue(drawer, true);
                drawer.BeginShadowBar();
                var text = new StringBuilder();
                text.Append(item.Element.name).Append("  ").Append((fraction * 100f).ToString("0.0")).Append("%\n");
                text.Append(GameUtil.GetFormattedMass(item.Mass)).Append("  (总质量: ").Append(GameUtil.GetFormattedMass(snapshot.TotalMass)).Append(")\n");
                if (item.Phase == MulticomponentPhase.Liquid)
                    text.Append("参考密度 ").Append(item.Element.maxMass.ToString("0.#")).Append(" kg/m³\n");
                text.Append(GameUtil.GetFormattedTemperature(item.Temperature)).Append("\n\n");
                string desc = Regex.Replace(item.Element.Description() ?? "", "<[^>]*>", "").Replace("\r", "");
                int wrap = Math.Max(12, (width - 24) / 15);
                foreach (string paragraph in desc.Split('\n')) {
                    if (paragraph.Length == 0) { text.Append('\n'); continue; }
                    for (int offset = 0; offset < paragraph.Length; offset += wrap)
                        text.Append(paragraph.Substring(offset, Math.Min(wrap, paragraph.Length - offset))).Append('\n');
                }
                drawer.DrawText(text.ToString(), style);
                float maximum = (float)MaxShadowX.GetValue(drawer);
                MaxShadowX.SetValue(drawer, Math.Max(maximum, x + width));
                drawer.EndShadowBar();
                bottom = Math.Min(bottom, ((Vector2)CurrentPos.GetValue(drawer)).y);
                x += width + 18;
            }
            RootPos.SetValue(drawer, originalRoot);
            CurrentPos.SetValue(drawer, new Vector2(originalRoot.x, bottom));
            FirstBar.SetValue(drawer, false);
            return true;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo getter = AccessTools.PropertyGetter(typeof(Element), nameof(Element.nameUpperCase));
            MethodInfo draw = AccessTools.Method(typeof(HoverTextDrawer), nameof(HoverTextDrawer.DrawText), new[] { typeof(string), typeof(TextStyleSetting) });
            MethodInfo end = AccessTools.Method(typeof(HoverTextDrawer), nameof(HoverTextDrawer.EndShadowBar));
            int drawIndex = -1;
            for (int i = 0; i < code.Count; i++) {
                if (!Equals(code[i].operand, getter)) continue;
                int candidate = code.FindIndex(i + 1, c => Equals(c.operand, draw));
                if (candidate > i && candidate - i <= 8) { drawIndex = candidate; break; }
            }
            int endIndex = drawIndex < 0 ? -1 : code.FindIndex(drawIndex + 1, c => Equals(c.operand, end));
            if (drawIndex < 0 || endIndex < 0 || endIndex + 1 >= code.Count) return code;
            Label done = generator.DefineLabel();
            code[endIndex + 1].labels.Add(done);
            code[drawIndex].opcode = OpCodes.Call;
            code[drawIndex].operand = AccessTools.Method(typeof(MulticomponentHoverCardPatch), nameof(DrawCardsOrTitle));
            code.Insert(drawIndex + 1, new CodeInstruction(OpCodes.Brtrue, done));
            return code;
        }
    }
}
