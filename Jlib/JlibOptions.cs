using System.Collections.Generic;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace Jlib.Runtime {
    /// <summary>Read-only mod screen entry listing the mods that currently use Jlib.</summary>
    public sealed class JlibOptions : IOptions {
        public IEnumerable<IOptionsEntry> CreateOptions() {
            yield return new TextBlockOptionsEntry("Dependents", new OptionAttribute(
                FormatDependents(),
                "已加载并引用 Jlib 的模组。每次打开此页面时重新读取。",
                "依赖 / Dependents"));
        }

        public void OnOptionsChanged() {
        }

        internal static string FormatDependents() {
            var dependents = PUtil.DependentMods();
            if (dependents.Count == 0)
                return "当前没有已加载的模组依赖 Jlib。";
            return "正在依赖 Jlib 的模组（" + dependents.Count + "）：\n" +
                string.Join("\n", dependents);
        }
    }
}
