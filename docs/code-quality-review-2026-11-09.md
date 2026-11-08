# Jlib 代码质量审查与修复

日期：2026-11-09（Asia/Taipei）。

本轮修复了 Harmony 补丁调整、资产加载失败清理和多组分悬停卡片中的明确缺陷，并为普通提交建立 Linux、Windows 自动回归。审查重点是 Jlib 新增的运行时、资源、所有权、多组分及构建发布代码；继承的 PLib 模块通过双目标编译检查。公开 ABI 和版本保持为 ABI 1、0.3.0。

- 审查基线：[0519ec0](https://github.com/LIghtJUNction/Jlib/commit/0519ec0fa7e99a878dba54dfc3bdb81a57f3559e)。
- 已验证的代码版本：[b57443c](https://github.com/LIghtJUNction/Jlib/commit/b57443c957b3c9094b0612277978a324e94a4865)。
- 完整代码差异：[0519ec0…b57443c](https://github.com/LIghtJUNction/Jlib/compare/0519ec0fa7e99a878dba54dfc3bdb81a57f3559e...b57443c957b3c9094b0612277978a324e94a4865)。

## 发现与修复

| 问题 | 修复后的行为 | 验证依据 |
| --- | --- | --- |
| 补丁调整在校验类型、所有者和注册关系之前删除补丁，失败请求也会改变游戏行为 | 先完成校验，再按所有者和阶段精确移除；无法唯一选中时保留原补丁并返回失败 | `DiagnosticsPatchCoverage` 使用真实 Harmony 执行 Prefix 与 Transpiler |
| 同一方法用于多个所有者或阶段时被一起移除；重建丢失排序约束 | 保留其他注册以及原有 `before`、`after`、`debug`；新顺序应用失败时恢复原配置 | 检查注册数量、元数据及被补丁修改后的实际返回值 |
| `*` 会触发 Harmony 批量删除；重新注册同优先级补丁会丢失原注册次序 | 在移除前拒绝通配所有者和同阶段同优先级的非零调整；零增量直接成功；界面显示拒绝原因 | 检查所有者数量、注册索引、优先级、实际执行结果及重建次数 |
| 图像解码、Sprite 创建或 KAnim 部分加载抛错后遗漏纹理清理 | 共用 `LoadTexture`，由创建方在失败时释放纹理；成功交给游戏的 KAnim 资源保留游戏所有权 | 24 条资产生命周期断言，检查清理次数、缓存和失败后继续加载 |
| 同帧重复悬停绘制漏关阴影条；全局去重误吞另一个绘制器的卡片 | 所有替换绘制路径关闭调用者的阴影条；以弱引用表按绘制器保存帧与格子状态 | 28 条悬停生命周期断言，覆盖重复调用、多个绘制器、跨帧和回退 |
| 无效寻路 Prefix 仍安装在高频路径，另有无调用的私有渲染方法 | 删除无行为的补丁安装、Prefix、反射字段和死方法；保留公开游泳阈值与 `EvaluateSwimState` | 调用点审查和双目标编译；未据此宣称游戏帧率提升 |
| 普通提交缺少自动宿主回归；发布测试可能跳过实际 ZIP | 新增 Linux/Windows 自动回归；发布流程显式检查两份 DLL 和生成的 ZIP | GitHub Actions 通过，发布工作流 YAML 解析通过，实际产物检查通过 |

诊断界面现在明确展示“声明优先级”。Harmony 的 `before/after` 约束和注册顺序也参与最终执行顺序，按钮与报告中的文字已与此保持一致。调度器的说明也改为实际的短锁交换队列、锁外执行回调。

## 验证结果

| 验证层级 | 结果 | 范围 |
| --- | --- | --- |
| 基线宿主套件 | 187 条断言通过 | 原有所有权、资源路径和契约行为 |
| 旧逻辑负对照 | 按预期失败 | 非法补丁类型删除 Prefix；通配所有者误删；同优先级恢复改变返回值；解码异常未释放纹理；悬停条未闭合；跨绘制器去重 |
| 最终本地完整套件 | **464 条断言通过，无产物检查跳过** | 新旧宿主回归、两个目标 DLL 元数据、Mod ZIP、NuGet 包 |
| 双目标打包 | **通过** | `net48`、`netstandard2.1`，生成 `Jlib.zip` 和 `LIghtJUNction.Jlib.0.3.0.nupkg` |
| Linux/Windows 自动回归 | **均通过** | [b57443c 的 Actions 运行](https://github.com/LIghtJUNction/Jlib/actions/runs/42500990089) |
| 完整推送范围空白检查 | **通过** | `git diff --check 0519ec0fa7e99a878dba54dfc3bdb81a57f3559e b57443c957b3c9094b0612277978a324e94a4865` |

使用 .NET SDK 10.0.401，Harmony 行为回归使用官方 `Lib.Harmony` 2.4.2，在测试自有方法上实际执行补丁与撤销。资产和悬停测试执行真实 Jlib 源码，Unity/game 对象边界使用内存替身；测试不执行游戏 DLL。

构建仍报告 52 条现有警告：4 条 net48 空值分析、6 条未使用变量、4 条旧 API、38 条 XML 参数注释，部分在两个目标中重复出现。本轮没有新增全局警告豁免。

可在具有合法游戏引用的环境中复现：

```sh
dotnet pack Jlib/Jlib.csproj -c Release -m:1 -nr:false \
  -p:UseSharedCompilation=false -p:OniManagedPath="$ONI_MANAGED_PATH"
dotnet run --project Jlib.Tests/Jlib.Tests.csproj -c Release \
  -p:BuildInParallel=false
```

本轮没有启动真实 ONI/Unity 游戏会话。实际 GPU 资源释放、画面效果、音频与 AssetBundle 加载仍需要游戏环境验证；上述结果是宿主行为、编译及包契约证据。

## 提交

| 提交 | 内容 |
| --- | --- |
| [4011f33](https://github.com/LIghtJUNction/Jlib/commit/4011f33b7df787f6e8542f4f38794b744033cbb8) | Linux/Windows 自动宿主回归 |
| [4bfc71c](https://github.com/LIghtJUNction/Jlib/commit/4bfc71c84755880951948538e0255eebb30e1636) | 资产构建失败清理与对应测试 |
| [0fc4d90](https://github.com/LIghtJUNction/Jlib/commit/0fc4d90bd5d34a0a58e3658ba47450d39737d000) | Harmony 调整保护、失败恢复及准确的诊断说明 |
| [13955f7](https://github.com/LIghtJUNction/Jlib/commit/13955f76de64b36ab4cce51a1e25f2d484bea4a0) | 悬停绘制生命周期、无效代码清理与发布产物检查 |
| [b57443c](https://github.com/LIghtJUNction/Jlib/commit/b57443c957b3c9094b0612277978a324e94a4865) | 通配所有者、同优先级注册顺序保护和界面错误反馈 |

OniMods 已在 [a4181f8](https://github.com/LIghtJUNction/OniMods/commit/a4181f8cbeebe7de1d62a1cbd3e062d11c92aa74) 将 Jlib 子模块指向上述代码版本，并将跟踪分支从旧发布分支改为 `main`。

## 自检

- 改动价值：每个行为修复对应明确的失败场景。
- 表达清晰：区分声明优先级、实际执行顺序及资源所有权。
- 证据强度：新回归在旧实现上失败，在修复实现上通过。
- 覆盖范围：明确区分宿主、产物、CI 和真实游戏运行。
- 实现复杂度：复用 Harmony、Unity 生命周期 API 和现有测试工程；删除无效路径，保留公开消费者接口。
