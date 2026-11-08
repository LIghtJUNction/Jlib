/*
 * Copyright 2026 Jlib contributors
 * SPDX-License-Identifier: MIT
 * The full permission and warranty notice is in the repository LICENSE.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using PeterHan.PLib.Core;

namespace Jlib.Runtime {
    /// <summary>
    /// Call statistics for a specific discovered interface or API method.
    /// </summary>
    public sealed class ApiCallStats {
        public string ApiName { get; }
        private long totalCalls;
        public long TotalCalls => totalCalls;

        private readonly ConcurrentDictionary<string, long> callers = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, long> Callers => callers;

        public string LastCaller { get; private set; }
        public System.DateTime LastCallTime { get; private set; }

        public ApiCallStats(string apiName) {
            ApiName = apiName ?? throw new ArgumentNullException(nameof(apiName));
            LastCallTime = System.DateTime.UtcNow;
            LastCaller = "";
        }

        public void Increment(string callerMod) {
            System.Threading.Interlocked.Increment(ref totalCalls);
            callers.AddOrUpdate(callerMod, 1, (_, current) => current + 1);
            LastCaller = callerMod;
            LastCallTime = System.DateTime.UtcNow;
        }

        public void Reset() {
            System.Threading.Interlocked.Exchange(ref totalCalls, 0);
            callers.Clear();
            LastCaller = "";
        }
    }

    /// <summary>
    /// Dynamically discovered functional module metadata.
    /// </summary>
    public sealed class DiscoveredModuleInfo {
        public string NamespacePrefix { get; }
        public string DisplayName { get; }
        public IReadOnlyList<Type> DiscoveredTypes { get; }
        public int TotalMethods { get; }

        public DiscoveredModuleInfo(string nsPrefix, string displayName, List<Type> types) {
            NamespacePrefix = nsPrefix;
            DisplayName = displayName;
            DiscoveredTypes = types.AsReadOnly();
            int methodCount = 0;
            foreach (var t in types) {
                try {
                    methodCount += t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length;
                } catch {
                    // Ignore reflection issues on individual types
                }
            }
            TotalMethods = methodCount;
        }
    }

    /// <summary>
    /// Information about a single patch applied via Harmony.
    /// </summary>
    public sealed class PatchEntryInfo {
        public string PatchType { get; } // "Prefix", "Postfix", "Transpiler", "Finalizer"
        public string Owner { get; }
        public int Priority { get; }
        public MethodInfo PatchMethod { get; }
        public string MethodSignature => $"{PatchMethod.DeclaringType?.FullName ?? "Unknown"}.{PatchMethod.Name}";

        public PatchEntryInfo(string patchType, string owner, int priority, MethodInfo patchMethod) {
            PatchType = patchType;
            Owner = owner ?? "Unknown";
            Priority = priority;
            PatchMethod = patchMethod;
        }
    }

    /// <summary>
    /// Information about a hooked game method and its Harmony patch pipeline.
    /// </summary>
    public sealed class HookedMethodInfo {
        public MethodBase OriginalMethod { get; }
        public string MethodName => $"{OriginalMethod.DeclaringType?.FullName ?? "Unknown"}.{OriginalMethod.Name}";
        public IReadOnlyList<PatchEntryInfo> Prefixes { get; }
        public IReadOnlyList<PatchEntryInfo> Transpilers { get; }
        public IReadOnlyList<PatchEntryInfo> Postfixes { get; }
        public IReadOnlyList<PatchEntryInfo> Finalizers { get; }

        public HookedMethodInfo(
            MethodBase original,
            List<PatchEntryInfo> prefixes,
            List<PatchEntryInfo> transpilers,
            List<PatchEntryInfo> postfixes,
            List<PatchEntryInfo> finalizers) {
            OriginalMethod = original;
            Prefixes = prefixes.AsReadOnly();
            Transpilers = transpilers.AsReadOnly();
            Postfixes = postfixes.AsReadOnly();
            Finalizers = finalizers.AsReadOnly();
        }
    }

    /// <summary>
    /// Dynamic interface discovery, invocation profiling, caller attribution,
    /// Harmony hook coverage inspection, and reordering diagnostics.
    /// </summary>
    public static class JlibDiagnostics {
        /// <summary>Enables verbose debug tracing to Player.log.</summary>
        public static bool DebugMode { get; set; } = false;

        private static readonly ConcurrentDictionary<string, ApiCallStats> callStatistics =
            new ConcurrentDictionary<string, ApiCallStats>(StringComparer.Ordinal);

        /// <summary>
        /// Records an interface or API method invocation, attributing it to the caller mod.
        /// </summary>
        public static void RecordCall(string apiName = null, Assembly callerAssembly = null) {
            try {
                if (string.IsNullOrEmpty(apiName)) {
                    var frame = new StackFrame(1, false);
                    var method = frame.GetMethod();
                    apiName = method != null ? $"{method.DeclaringType?.Name}.{method.Name}" : "UnknownInterface";
                }

                if (callerAssembly == null) {
                    var trace = new StackTrace(1, false);
                    var frames = trace.GetFrames();
                    if (frames != null) {
                        var selfAsm = typeof(JlibDiagnostics).Assembly;
                        foreach (var f in frames) {
                            var m = f.GetMethod();
                            var asm = m?.DeclaringType?.Assembly;
                            if (asm == null || asm == selfAsm) continue;
                            var asmName = asm.GetName().Name ?? "";
                            if (asmName == "mscorlib" || asmName.StartsWith("System") || asmName.StartsWith("UnityEngine"))
                                continue;
                            callerAssembly = asm;
                            break;
                        }
                    }
                }

                string callerModLabel = ResolveCallerLabel(callerAssembly);
                var stats = callStatistics.GetOrAdd(apiName, name => new ApiCallStats(name));
                stats.Increment(callerModLabel);

                if (DebugMode) {
                    PUtil.LogDebug($"[Jlib Diagnostics] {apiName} called by '{callerModLabel}' (Total: {stats.TotalCalls})");
                }
            } catch {
                // Diagnostics must never fail host operations or throw
            }
        }

        private static string ResolveCallerLabel(Assembly assembly) {
            if (assembly == null) return "Unknown Caller";
            try {
                var mod = PUtil.GetModInfo(assembly);
                if (mod != null) {
                    string title = mod.title?.Trim() ?? "";
                    string staticId = mod.staticID?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(staticId))
                        return $"{title} [{staticId}]";
                    if (!string.IsNullOrEmpty(staticId))
                        return staticId;
                    if (!string.IsNullOrEmpty(title))
                        return title;
                }
            } catch {
                // Fall back to assembly name
            }
            return assembly.GetName().Name ?? "Unknown";
        }

        /// <summary>
        /// Dynamically discovers all functional modules and public types in Jlib via reflection.
        /// No hardcoded enum - any newly added types or namespaces in future updates are detected automatically.
        /// </summary>
        public static IReadOnlyList<DiscoveredModuleInfo> DiscoverModules() {
            var modules = new Dictionary<string, (string DisplayName, List<Type> Types)>(StringComparer.Ordinal);
            var selfAsm = typeof(JlibDiagnostics).Assembly;

            Type[] exportedTypes;
            try {
                exportedTypes = selfAsm.GetExportedTypes();
            } catch (ReflectionTypeLoadException ex) {
                exportedTypes = ex.Types.OfType<Type>().ToArray();
            }

            foreach (var type in exportedTypes) {
                if (type == null) continue;
                string ns = type.Namespace ?? "Jlib";
                string prefix = GetNamespacePrefix(ns);
                if (!modules.TryGetValue(prefix, out var modEntry)) {
                    string friendlyName = GetModuleDisplayName(prefix);
                    modEntry = (friendlyName, new List<Type>());
                    modules[prefix] = modEntry;
                }
                modEntry.Types.Add(type);
            }

            var result = new List<DiscoveredModuleInfo>();
            foreach (var kvp in modules.OrderBy(k => k.Key, StringComparer.Ordinal)) {
                result.Add(new DiscoveredModuleInfo(kvp.Key, kvp.Value.DisplayName, kvp.Value.Types));
            }
            return result;
        }

        private static string GetNamespacePrefix(string ns) {
            if (ns.StartsWith("PeterHan.PLib.")) {
                var parts = ns.Split('.');
                if (parts.Length >= 3) return $"PeterHan.PLib.{parts[2]}";
            }
            if (ns.StartsWith("Jlib.")) {
                var parts = ns.Split('.');
                if (parts.Length >= 2) return $"Jlib.{parts[1]}";
            }
            return ns;
        }

        private static string GetModuleDisplayName(string prefix) {
            switch (prefix) {
                case "PeterHan.PLib.Core": return "PLib.Core (基础工具 / 反射增强 / 模组所有权)";
                case "PeterHan.PLib.Options": return "PLib.Options (模组配置 / 动态选项 / 热刷新)";
                case "PeterHan.PLib.UI": return "PLib.UI (声明式 UI / 自动布局 / 弹窗组件)";
                case "PeterHan.PLib.Actions": return "PLib.Actions (自定义按键 / 工具栏扩展)";
                case "PeterHan.PLib.Buildings": return "PLib.Buildings (建筑注册 / 管线端口 / 范围显示)";
                case "PeterHan.PLib.Lighting": return "PLib.Lighting (动态多边形光照 / 射线投射)";
                case "PeterHan.PLib.Database": return "PLib.Database (Codex 法典注入 / 殖民地成就)";
                case "PeterHan.PLib.AVC": return "PLib.AVC (自动版本检查 / 创意工坊更新提示)";
                case "Jlib.Runtime": return "Jlib.Runtime (前置契约 / 原生重定向 / 沙盒资源)";
                case "Jlib.Multicomponent": return "Jlib.Multicomponent (多分量网格模拟兼容管线)";
                default: return prefix;
            }
        }

        /// <summary>Returns the current call statistics.</summary>
        public static IReadOnlyDictionary<string, ApiCallStats> GetCallStats() => callStatistics;

        /// <summary>Resets all call statistics.</summary>
        public static void ResetCallStats() {
            callStatistics.Clear();
            if (DebugMode) {
                PUtil.LogDebug("[Jlib Diagnostics] Call statistics reset.");
            }
        }

        /// <summary>
        /// Generates a comprehensive diagnostic report of discovered modules,
        /// interface invocations, caller mod provenance, and active patch pipelines.
        /// </summary>
        public static string GenerateReport() {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Jlib 诊断与性能监测快照 (Generated: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC)");
            sb.AppendLine($"版本: {JlibApi.Version} (ABI {JlibApi.AbiVersion}, PLib {JlibApi.PLibVersion})");
            sb.AppendLine($"调试模式: {(DebugMode ? "已开启 (Enabled)" : "已关闭 (Disabled)")}");
            sb.AppendLine("================================================================================\n");

            sb.AppendLine("--- 1. 动态反射发现的模块与接口清单 (Zero-Hardcoding Dynamic Discovery) ---");
            var modules = DiscoverModules();
            int totalTypes = 0;
            int totalMethods = 0;
            foreach (var mod in modules) {
                totalTypes += mod.DiscoveredTypes.Count;
                totalMethods += mod.TotalMethods;
                sb.AppendLine($"• [{mod.NamespacePrefix}] {mod.DisplayName}");
                sb.AppendLine($"  - 类型数: {mod.DiscoveredTypes.Count}, 公开方法数: {mod.TotalMethods}");
            }
            sb.AppendLine($"汇总: 共发现 {modules.Count} 个模块，{totalTypes} 个公开类型，{totalMethods} 个公开 API 方法。\n");

            sb.AppendLine("--- 2. 接口调用次数与来源 Mod 统计 (Call Counters & Caller Attribution) ---");
            if (callStatistics.IsEmpty) {
                sb.AppendLine("当前尚无接口调用记录（可能处于启动初期，或尚未有依赖 Mod 触发 API）。");
            } else {
                foreach (var kvp in callStatistics.OrderByDescending(k => k.Value.TotalCalls)) {
                    var stats = kvp.Value;
                    sb.AppendLine($"• 接口: {stats.ApiName} -> 总调用: {stats.TotalCalls} 次 (最后调用: {stats.LastCallTime:HH:mm:ss})");
                    foreach (var caller in stats.Callers.OrderByDescending(c => c.Value)) {
                        sb.AppendLine($"    └─ 调用者: {caller.Key} (调用 {caller.Value} 次)");
                    }
                }
            }
            sb.AppendLine();

            sb.AppendLine("--- 3. Harmony 补丁与声明优先级 (Declared Patch Priorities) ---");
            sb.AppendLine("各阶段按声明优先级从高到低列出；before/after 约束及注册顺序也参与最终执行顺序。");
            var hooked = GetHookedMethods();
            if (hooked.Count == 0) {
                sb.AppendLine("当前没有检测到活动的 Harmony 补丁。");
            } else {
                sb.AppendLine($"共检测到 {hooked.Count} 个被挂钩的方法：\n");
                foreach (var method in hooked.Take(50)) {
                    sb.AppendLine($"[挂钩方法] {method.MethodName}");
                    if (method.Prefixes.Count > 0) {
                        sb.AppendLine("  Prefix 声明优先级列表 (前置补丁):");
                        for (int i = 0; i < method.Prefixes.Count; i++) {
                            var p = method.Prefixes[i];
                            sb.AppendLine($"    #{i + 1} Owner: {p.Owner} | Priority: {p.Priority} | Method: {p.MethodSignature}");
                        }
                    }
                    if (method.Transpilers.Count > 0) {
                        sb.AppendLine("  Transpiler 声明优先级列表 (转译补丁):");
                        for (int i = 0; i < method.Transpilers.Count; i++) {
                            var p = method.Transpilers[i];
                            sb.AppendLine($"    #{i + 1} Owner: {p.Owner} | Priority: {p.Priority} | Method: {p.MethodSignature}");
                        }
                    }
                    if (method.Postfixes.Count > 0) {
                        sb.AppendLine("  Postfix 声明优先级列表 (后置补丁):");
                        for (int i = 0; i < method.Postfixes.Count; i++) {
                            var p = method.Postfixes[i];
                            sb.AppendLine($"    #{i + 1} Owner: {p.Owner} | Priority: {p.Priority} | Method: {p.MethodSignature}");
                        }
                    }
                    sb.AppendLine();
                }
                if (hooked.Count > 50) {
                    sb.AppendLine($"... 另有 {hooked.Count - 50} 个挂钩方法未在简报中展开。可在游戏内调试面板中查看全部。");
                }
            }
            sb.AppendLine("================================================================================");
            return sb.ToString();
        }

        /// <summary>Writes the full diagnostic report to Player.log.</summary>
        public static void DumpReport() {
            string report = GenerateReport();
            PUtil.LogWarning("\n" + report);
        }

        /// <summary>
        /// Inspects Harmony patches by stage in descending declared priority.
        /// Harmony's before/after constraints and registration order also affect execution order.
        /// </summary>
        public static IReadOnlyList<HookedMethodInfo> GetHookedMethods() {
            var list = new List<HookedMethodInfo>();
            try {
                var methods = Harmony.GetAllPatchedMethods();
                if (methods == null) return list;

                foreach (var method in methods) {
                    if (method == null) continue;
                    var info = Harmony.GetPatchInfo(method);
                    if (info == null) continue;

                    var prefixes = ConvertPatches("Prefix", info.Prefixes);
                    var transpilers = ConvertPatches("Transpiler", info.Transpilers);
                    var postfixes = ConvertPatches("Postfix", info.Postfixes);
                    var finalizers = ConvertPatches("Finalizer", info.Finalizers);

                    if (prefixes.Count > 0 || transpilers.Count > 0 || postfixes.Count > 0 || finalizers.Count > 0) {
                        list.Add(new HookedMethodInfo(method, prefixes, transpilers, postfixes, finalizers));
                    }
                }
            } catch (Exception ex) {
                if (DebugMode) PUtil.LogWarning("Failed to enumerate patched methods: " + ex);
            }
            return list;
        }

        private static List<PatchEntryInfo> ConvertPatches(string patchType, IEnumerable<Patch> patches) {
            var result = new List<PatchEntryInfo>();
            if (patches == null) return result;
            foreach (var p in patches) {
                if (p?.PatchMethod != null) {
                    result.Add(new PatchEntryInfo(patchType, p.owner, p.priority, p.PatchMethod));
                }
            }
            return result.OrderByDescending(p => p.Priority).ToList();
        }

        /// <summary>
        /// Updates a patch's declared priority and rebuilds its Harmony pipeline.
        /// Explicit ordering constraints remain in effect.
        /// </summary>
        /// <remarks>
        /// The owner must have exactly one patch in the selected stage. Harmony removes patches
        /// by owner and stage; ambiguous selections are refused without changing the pipeline.
        /// The owner "*" is refused because Harmony treats it as an all-owner removal wildcard.
        /// A change is also refused when another patch in that stage has the same priority,
        /// since Harmony's public API cannot restore the original registration index on failure.
        /// </remarks>
        public static bool AdjustPatchPriority(
            MethodBase originalMethod,
            MethodInfo patchMethod,
            string patchType,
            string owner,
            int deltaPriority,
            out string message) {
            if (originalMethod == null) {
                message = "目标方法为空";
                return false;
            }
            if (patchMethod == null) {
                message = "补丁方法为空";
                return false;
            }
            HarmonyPatchType type;
            switch (patchType) {
                case "Prefix": type = HarmonyPatchType.Prefix; break;
                case "Postfix": type = HarmonyPatchType.Postfix; break;
                case "Transpiler": type = HarmonyPatchType.Transpiler; break;
                case "Finalizer": type = HarmonyPatchType.Finalizer; break;
                default:
                    message = $"不支持的补丁类型: {patchType}";
                    return false;
            }
            if (string.IsNullOrWhiteSpace(owner)) {
                message = "补丁所有者为空";
                return false;
            }
            if (owner == "*") {
                message = "所有者 * 是 Harmony 的批量移除通配符，无法单独调整该补丁";
                return false;
            }

            try {
                var info = Harmony.GetPatchInfo(originalMethod);
                if (info == null) {
                    message = "未找到目标方法的补丁信息";
                    return false;
                }

                IEnumerable<Patch> targetList = type switch {
                    HarmonyPatchType.Prefix => info.Prefixes,
                    HarmonyPatchType.Postfix => info.Postfixes,
                    HarmonyPatchType.Transpiler => info.Transpilers,
                    _ => info.Finalizers
                };
                var owned = targetList.Where(p => string.Equals(p.owner, owner, StringComparison.Ordinal)).ToArray();
                if (owned.Length != 1 || owned[0].PatchMethod != patchMethod) {
                    message = "未找到唯一匹配的补丁；同一所有者在该阶段注册多个补丁时无法单独调整";
                    return false;
                }

                var existingPatch = owned[0];
                int currentPriority = existingPatch.priority;
                if (deltaPriority == 0) {
                    message = $"补丁 [{patchMethod.Name}] 的声明优先级保持 {currentPriority}";
                    return true;
                }
                if (targetList.Any(p => !ReferenceEquals(p, existingPatch) && p.priority == currentPriority)) {
                    message = "该阶段有相同声明优先级的补丁；重新注册无法保留原注册顺序，调整已取消";
                    return false;
                }
                int newPriority = checked(currentPriority + deltaPriority);
                if (newPriority == -1)
                    throw new ArgumentOutOfRangeException(nameof(deltaPriority), "Harmony 将 -1 保留为默认优先级标记");
                var harmony = new Harmony(owner);
                var harmonyMethod = new HarmonyMethod(patchMethod) {
                    priority = newPriority,
                    before = existingPatch.before,
                    after = existingPatch.after,
                    debug = existingPatch.debug
                };

                // Removing by MethodInfo would also remove other owners and stages.
                harmony.Unpatch(originalMethod, type, owner);
                try {
                    ApplyPatch(harmony, originalMethod, type, harmonyMethod);
                } catch (Exception applyError) {
                    harmonyMethod.priority = currentPriority;
                    try {
                        ApplyPatch(harmony, originalMethod, type, harmonyMethod);
                    } catch (Exception restoreError) {
                        throw new AggregateException("补丁调整失败且恢复失败，请重启游戏", applyError, restoreError);
                    }
                    throw;
                }

                message = $"已将补丁 [{patchMethod.Name}] 的声明优先级调整为 {newPriority} (原: {currentPriority})，补丁已重新应用";
                if (DebugMode) {
                    PUtil.LogDebug($"[Jlib Diagnostics] {message}");
                }
                return true;
            } catch (Exception ex) {
                message = "调整补丁顺序失败: " + ex.Message;
                PUtil.LogError(message);
                return false;
            }
        }

        private static void ApplyPatch(Harmony harmony, MethodBase original, HarmonyPatchType type,
                HarmonyMethod patch) => harmony.Patch(original,
                    prefix: type == HarmonyPatchType.Prefix ? patch : null,
                    postfix: type == HarmonyPatchType.Postfix ? patch : null,
                    transpiler: type == HarmonyPatchType.Transpiler ? patch : null,
                    finalizer: type == HarmonyPatchType.Finalizer ? patch : null);
    }
}
