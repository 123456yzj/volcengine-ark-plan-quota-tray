# 验证与验收（verification）

## 当前状态（真实）

- 应用：**已创建并可构建 / 运行**。正式 `bin/` 为 v0.2 构建产物**尚未更新**；
  v0.3（UX011）候选 `bin-release/`（368/368）；v0.4（UX012）`bin-v04/`（518/518）；
  v0.5–v0.11 历史候选 `bin-v05/`–`bin-v11/` 保留；已验收 v0.12（UX020）`bin-v12/`、v0.13（UX021）`bin-v13/` 为历史；
  **v0.14（UX022）候选 `bin-v14/` 已由 T052 构建并经主代理 2026-10-04 独立验收接受**，
  主代理 2026-10-04 独立验收接受 T041–T047（T047 最终 **963/0**、smoke 0）与 T050（bin-v13 重建、1030/0、smoke 0）。
  主代理已在授权内本地升级：`launch.ps1 -OutputDir bin-v14 -PreviousProcessId 2268` → 旧进程 CLOSED（exit 0）、
  LAUNCHED pid 10600 `D:\ark_left\bin-v14\ark_left.exe`（**当前仍运行该 pid 10600**）。
  **v0.15（UX023）候选 `bin-v15/` 已由独立验收 `general` 新会话 `ses_efa9662e0ffe2FnSqf1hyzruaR`
  构建 / 验证并经主代理 2026-10-04 接受代码与自动检查**：`build.ps1 -OutputDir bin-v15` 成功、
  隔离单测 **1160/0**、隔离 `verify-interaction.ps1` 通过、`git diff --check` 通过；入口改为
  `launch.ps1 -OutputDir bin-v15`、`start.cmd` 现指向 `bin-v15`（入口实施 `ses_efa91a0b9ffeAiEUuBNk4jt1OD`；
  入口新 `general` 会话 `ses_efa905d9fffeJZ0cVrtvgDylgo` 复跑隔离 launcher **20/20** suffix dbcf614f exit 0）。
  **真实升级尚未执行**：当前仍运行 `bin-v14` PID 10600；正式 `bin` 仍 v0.2、历史候选未覆盖；
  T048 / T051 / T052-DOC 为纯文档收尾，**未改源码 / 脚本 / 二进制**。
- 需求基线：`v0.1`（Q-001 已由用户确认关闭）。
  交互改进层 **v0.2 done**；**v0.3（UX011）368/368**；**v0.4（UX012）518/518**；
  **v0.5（UX013）T032 532/532**；v0.6–v0.13（UX014–UX021）由主代理独立验收接受 T041–T047、T050
  （538/0、668/0、732/0、820/0、launcher 20、906/0、最终 963/0、1030/0）；
  **v0.14（UX022）由主代理独立验收接受 T052**（bin-v14 单测 **1088/0**、smoke 0、8/20 项）；
  **v0.15（UX023）由主代理 2026-10-04 接受 T057**（bin-v15 隔离单测 **1160/0**、隔离 `verify-interaction.ps1` 通过、`git diff --check` 通过）；
  Q005 CLOSED，Q006 CLOSED（T031 闭环）；T007 人工仍 pending，系统 Gate 未通过；
  见下方历史 T027 / T031 / T032 与文末新增「T052 主代理 UX022 接管独立验收记录」。
- 历史 v0.4 T031 构建命令：`powershell -NoProfile -ExecutionPolicy Bypass -File D:\ark_left\build.ps1 -OutputDir D:\ark_left\bin-v04`（3 exe 成功、无告警；完整结果见 T031 章节）。
- 启动命令：双击 `start.cmd`（当前 → `launch.ps1 -OutputDir bin-v15`），或运行 `bin-v15\ark_left.exe`（`--show` 可强制显示）。
  **当前候选**：`start.cmd` 现指向 `bin-v15`（v0.15 UX023，T057 主代理 2026-10-04 已接受代码与自动检查；**真实升级尚未执行**）；正式 `bin` 仍 v0.2、历史候选未覆盖。
  体验 v0.15 可直接双击 `start.cmd` 或运行 `bin-v15\ark_left.exe --show`，不要同时启动双实例。
  主代理在本轮授权内**实际关闭旧 PID 2268** 并启动 `bin-v14`（pid 10600）；v0.13（T050，PID 9160 → 15260）与 T048（PID 15796 → bin-v12）升级记录保留为历史（见 [`../README.md`](../README.md) 启动说明）。
- 历史 v0.4 T031 测试命令：隔离 `ARK_LEFT_STATE_DIR=D:\ark_left\bin-v04\t031-main-state` 后执行 `D:\ark_left\bin-v04\ark_left-tests.exe`：
  **passed 518 / failed 0，退出 0**（env 已恢复）。v0.2 最终收尾为 **245** 项；v0.3 为 **368** 项；
  v0.4 **当时源码**为 **518** 项（历史值；命令见下方「v0.4（UX012）T031 主代理独立验证」）。
- 历史 v0.4 T031 交互集成检查：`powershell -NoProfile -ExecutionPolicy Bypass -File D:\ark_left\tests\verify-interaction.ps1 -OutputDir D:\ark_left\bin-v04`
  （隔离环境，8 项 ok，退出 0；仅操作自建 PID，不联网 / 无真实 CLI）。
- 真实查询命令：`powershell -NoProfile -ExecutionPolicy Bypass -File check.ps1`
  （历史主代理已确认 `scope_verdict=Same` / `status=Ok`；T027 / T031 本轮**均未执行**真实 CLI / 联网 / 真实账号切换）
- 应用层验证：见下方执行记录；v0.2 / T026 / v0.3 T027 / v0.4 T031 / v0.5 T032 历史保留，
  最新为文末「T052 主代理 UX022 接管独立验收记录」；「T051 / T050」记录仍为历史，
  「T048 主代理 v0.12 独立验证记录」覆盖 T041–T047。

## 本轮执行记录（真实）

执行日期：2026-10-03。环境：Windows，无 dotnet SDK；
编译器 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5，.NET Framework 4.8）。

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 构建 | `build.ps1` | 通过：生成 `bin/ark_left.exe`、`bin/ark_left-check.exe`、`bin/ark_left-tests.exe` |
| 单元测试 | `bin\ark_left-tests.exe` / `test.ps1` | 通过：`passed: 81, failed: 0`（合成匿名 fixtures） |
| 真实 CLI 查询 | `bin\ark_left-check.exe` / `check.ps1` | 通过：`cli_found=true`、`status=Ok`，输出脱敏（产品 / 周期 / 剩余 / 重置） |
| GUI 冒烟（布局 + 子控件断言） | `bin\ark_left.exe --smoke-test` | 通过：退出码 0；loading / 大数据渲染 Bounds 均在工作区内，卡片子控件不越界，关闭为隐藏；写出 `bin/preview.png`；不联网 |
| 正常启动存活 | 启动 `bin\ark_left.exe` 后观察 4–5s | 通过：进程存活（非 smoke 不自动关闭），随后人工结束 |

**主代理独立复核（2026-10-03，随后）**：主代理在独立执行环境重跑
`build.ps1`、`test.ps1`（81/81）、`check.ps1`（`status=Ok`）、`--smoke-test`（退出 0），
全部通过；包装脚本同样成功。该独立结果与上表一致，作为 T-006 的验收证据。

真实查询脱敏输出（**不含 viewer / 账号 ID / 凭据**）：

```
cli_found=true
status=Ok
product=Agent Plan code=agent-plan state=subscribed
  period=5 小时 remaining=…% source=ok amount=… reset=…
  period=每周   remaining=…% source=ok amount=… reset=…
  period=每月   remaining=…% source=ok amount=… reset=…
```

> 上表中具体百分比 / 数值为**运行时会变动的真实数据**，此处以省略号占位，
> 避免把某次快照写死进文档。命令本身已真实执行。

## 本轮验收用例（已执行）

| 需求编号 | 验收场景 | 验证方式 | 证据位置 | 结果 | 未决问题 |
| --- | --- | --- | --- | --- | --- |
| R001 | 托盘图标、浮窗定位（工作区 / DPI / 多屏） | `--smoke-test` 布局断言 + 代码路径（`Screen.FromPoint` 仅在打开时取屏） | 本文件 | **部分执行**：进程 / 建窗 / Bounds 断言通过；多屏 / 高 DPI 的**视觉定位**人工未测 | 无 |
| R002 | 以剩余百分比为主展示；`percent=25` 显示剩余 75%；剩余绝对量展示；缺字段不补 0 | `ark_left-tests.exe` 合成用例 + 真实查询 | 本文件 | **通过**（逻辑层）；视觉排版人工未测 | Q-001 已关闭 |
| R003 | 未登录 / CLI 缺失 / 未订阅 / 部分桶错误 / 超时 / 失败 | `ark_left-tests.exe` 测试模式（与真实路径共用认证闸门与判定） | 本文件 | **通过**（逻辑层状态区分）；真实未登录场景未构造 | 无 |
| R004 | 手动刷新、关闭浮窗不退出、托盘菜单退出 | `--smoke-test`（关闭为隐藏断言）+ 正常启动存活 | 本文件 | **部分执行**：生命周期 / 隐藏通过；交互点击人工未测 | Q-002 已决定（不启用自动刷新） |
| R001/R004 | 多屏 / 高 DPI / 托盘点击与菜单（人工） | 有图形界面环境人工操作 | 本文件（T-007） | **未执行（pending）** | Q-002 |

> 说明：逻辑、进程与布局边界验证已完成；**Windows 托盘的视觉呈现、点击交互、多屏 /
> 高 DPI 实际定位未做人工视觉验证**，如实标注为"人工未测"，不记为通过。

## 修复轮记录（2026-10-03）

针对独立审查意见的修复与回归：

1. **认证闸门统一**：`auth` 退出码 / 启动失败 / `logged_in` 缺失或非布尔 / 取消 / 超时
   均先判定；只有退出 0 且 `logged_in=true` 才查询额度。`usage` 非零退出不得伪装成功。
   测试 transport 与真实路径共用 `JudgeAuth` / `JudgeInvocation` / `JudgeUsage`。
2. **进程读取有界**：改 `ReadToEndAsync` + 进程等待 + 整体 30s 超时；仅支持原生 `.exe`；
   `ARK_LEFT_CLI` 必须为存在的绝对 `.exe`；`Dispose` 后拒绝启动新进程；进程处理失败
   不会以 `Started=true, ExitCode=0` 冒充成功。
3. **UI 定位**：打开时记录所在 `Screen`，loading / 结果渲染后均按该工作区重算并
   `ConstrainToOpenScreen`；不再用移动后的光标屏幕决定重绘定位；修正激活 / 失活常量
   （`WM_ACTIVATE=0x0006`）并用 `OnDeactivate` 兜底隐藏。
4. **句柄释放**：`ClearContent` 逐个 `Dispose`；字体按样式缓存并在 `Dispose` 释放；
   移除重复 `SetLoading`；额度行改为展示**剩余绝对量 / 总量**；条目错误时仍展示可用 period。
5. **解析健壮性**：`items[null]`、`period=null`、period `error` 均反映为部分 / 格式错误，
   不再误判为未订阅；`updated_at` 按 **item 级**解析并展示；数值支持 `decimal` 等且
   超范围 epoch 不崩溃；错误文案固定化，不回显可能含 token 的上游文本。
6. **布局烟雾测试**：`--smoke-test` 在多周期大数据下断言 `Bounds` 在工作区内、**卡片内
   子控件不越出卡片**、关闭为隐藏；并写出 `bin/preview.png`（仅合成窗口，不截桌面）。

- 回归命令：`bin\ark_left-tests.exe` → `passed: 81, failed: 0`。
- 真实查询与冒烟命令同上表，均通过。

## 最终修复轮记录（2026-10-03）

1. **LayoutChrome**：`_header`/`_footer` 的 `Resize` 触发 `LayoutChrome`，并对初始化期
   null 做 guard；标题宽度随头部宽度计算，按钮右对齐。
2. **卡片子控件**：改为 `CardPanel`（宽度驱动 `Layout(innerW)`），在卡片自身宽度确定后
   重新计算百分比 / 进度条 / 文本位置；烟雾测试新增**内部控件 bounds 断言**。
3. **消息卡**：按文本测量（`MeasureString` 换行）可变高度，登录指令长文本不再被截断；
   内容超出时由工作区高度上限 + 滚动保证可读。
4. **进程读取**：只从 `RanToCompletion` 的 Task 取值，faulted/canceled/未完成不 `.Result`；
   drain 超时标 `drain-timeout` 失败而非空串当成功；异常时 `KillQuiet` 防遗留活进程；
   `_disposed` / token 检查与 `proc.Start` 及注册同一 `_gate`，`Dispose` 同 gate。
5. **烟雾测试**：timer 在断言 try **之前**启动，异常也不会挂起消息循环；re-show 断言使用
   `OpenScreenRef` 的工作区；正常合成渲染后 `DrawToBitmap` 保存 `bin/preview.png`。

- 复测：`test.ps1` → 81/81；`--smoke-test` → 退出 0；`bin/preview.png` 生成（404x425）。
- **主代理独立复核**上述命令全部通过（见上一节）。T-006 `done`，T-007 人工验证 `pending`。

## 交互改进 v0.2 实现轮验证（2026-10-03，真实执行）

范围：实现 UX001–UX010（`src/`、`tests/`）、交付 `docs/setup.md`、同步文档。
环境：Windows，无 dotnet SDK；`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5）。

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 构建 | `build.ps1` | 通过：生成 `ark_left.exe` / `ark_left-check.exe` / `ark_left-tests.exe` |
| 单元测试 | `ark_left-tests.exe` / `test.ps1` | 通过：**passed: 213, failed: 0**（含原 81 项） |
| 真实 CLI 查询 | `ark_left-check.exe` | 通过：`cli_found=true`、`status=Ok`（脱敏输出，未见真实身份） |
| 离线 GUI 冒烟 | `ark_left.exe --smoke-test` | 通过：退出码 0；loading / large / error / identity / reshow 的 Bounds 在工作区内；卡片子控件不越界；**loading 与结果同尺寸**；关闭为隐藏；写出 `bin/preview.png` / `preview-error.png` / `preview-identity.png` |
| 冒烟不写用户 marker | 冒烟前后检查 `%LOCALAPPDATA%\ark_left\first-run.done` | 通过：冒烟前后**均不存在**，未被创建 |
| 重复启动 IPC（隔离状态 + 隔离实例名） | `ARK_LEFT_STATE_DIR=<bin\test-state>` + `ARK_LEFT_INSTANCE_SUFFIX=<rand>` 启动首实例 + **不带 `--show`** 的第二实例 | 通过：第二实例**无 MessageBox** 且退出码 0、约 1s；首实例仍在（进程数 1 → 1）；隔离 marker 被首实例创建；测试后清理；真实用户实例未被触碰 |
| 测试状态隔离 | `test.ps1` 设置 `ARK_LEFT_STATE_DIR=bin\test-state` | 通过（脚本已配置，未触碰用户 marker） |

**新增自动用例（在 213 项内）**：`PercentFormatCases`（`<1%` / 一位小数 / 已用尽）、
`RelativeFormatCases`（新鲜度 / 倒计时 / 已过重置不推断恢复）、`RiskSummaryCases`
（每产品最低、忽略未知 / 错误、不求和）、`ScopeFingerprintCases` / `ScopeValidationCases`
（严格账户等值、不子串匹配、region/project/主子身份冲突、缺字段 unknown、TRN 解析）、
`PanelModelIdentityCases`（缓存 / `BeginQuery` 隐藏 / auth 未确认取消不清缓存 /
同 scope 失败保留）、`PanelModelStaleCases`、`PanelModelUnknownVerdictCases`
（Unknown viewer 不缓存）、`PanelModelNullIdentityCases`（active_profile 缺失不复用）、
`PanelStateAfterMismatch`、`HideControllerCases`（Toggle/Evt/Tick 各种顺序，含
deactivate-before-mousedown、抑制期、取消后仍可隐藏）、`PanelPositionerCases`、
`IdentityParseCases`（真实 auth/usage schema，`viewer.profile` 字符串）、
`IdentityDisplayCases`（type 映射产品 / 平台，不泄露 TRN / 用户名）、`ViewEmptyProductsCases`
（空 items 有明确消息）、`IpcSignalCases`（隔离命名事件）、`MarkerIsolationCases`、
`ProgressStageCases`（阶段 / 认证确认含 null identity / 取消）。

### 本轮**人工未测**（不记为通过）

- 托盘图标**真实点击 / 失焦 / 右键菜单**（含 UX001 竞态在真实 Explorer 下的表现）。
- 多屏 / 高 DPI 的**视觉**定位（延续 T-007）。
- 取消 / 重试 / 慢响应的**真实手感**；倒计时**真实走动**；键盘 `Tab` / 读屏 / tooltip。
- “打开设置指南”的真实记事本调用与剪贴板占用时的提示。
- 真实身份从 A 切到 B 的界面行为（scope 逻辑由合成用例覆盖，真实切换未构造）。

> 说明：上表自动检查**不构成**人工视觉或真实 Explorer 交互验收；相关项继续由 T-007 跟踪。
> `preview*.png` 为合成窗口自绘合成图，**非桌面截图**，且**不含真实账号数据**。

## 交互改进 v0.2 复核修复轮验证（2026-10-03，真实执行）

范围：处理主代理独立复核提出的未决项（identity / scope、面板缓存复用与终态、
`HideController` 切换消费、首启 banner 冒烟与告警清理、`check` scope_verdict、
可复现交互集成脚本），并同步文档。**不新增业务规则、不改 `usage plan` 契约**。
环境：Windows，无 dotnet SDK；`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5）。

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 构建（告警清理） | `build.ps1` | 通过：生成 3 个 exe；**原有 CS0414 `_firstRunBanner` 未使用告警已消除** |
| 单元测试 | `ark_left-tests.exe` / `test.ps1` | 通过：**passed: 237, failed: 0**（在既有 213 基础上新增 24 项回归） |
| 离线 GUI 冒烟 | `ark_left.exe --smoke-test` | 通过：退出码 0；含首启 banner 存在断言 + `preview-intro.png`；仍**不写用户 marker**、不触真实单例 IPC |
| 交互集成（隔离环境） | `tests/verify-interaction.ps1` | 通过：见下（本机真实执行，退出码 0） |
| 真实 CLI 查询 | `check.ps1` | **本轮未执行**（任务约定真实远端查询由主代理稍后跑）；`ark_left-check.exe` 已重新构建，新增 `scope_verdict=` 输出（`Same/Unknown/Mismatch`，不含 ID / hash / viewer / 凭据） |

### 交互集成脚本实际输出（2026-10-03，本机）

```
verify-interaction: suffix=14f18bc3
ok: first instance visible window
ok: WM_CLOSE hid the panel (process kept running)
ok: second instance (no --show) exited 0
ok: first instance window woken by second launch
ok: still a single test instance
ok: silent startup (marker present, no window)
ok: --show second instance exited 0
ok: --show woke the silent instance
verify-interaction passed
```

- 脚本使用隔离 `ARK_LEFT_STATE_DIR` + `ARK_LEFT_INSTANCE_SUFFIX`，并将 `ARK_LEFT_CLI`
  指向不存在的 exe 以避免联网；只操作本脚本启动的 PID，`finally` 终止这些进程、
  恢复环境并删除隔离目录；**未触碰真实用户实例或用户 marker**。
- 上述为进程 / 窗口可见性层证据，**仍不构成**人工视觉 / 手感验收。

### 复核修复项与对应回归

1. **身份与 scope（`Identity.cs`）**：`ScopeValidation.Validate` 子用户分支在
   `viewer.IsRootKnown && IsRoot==true` 时返回 `Mismatch`（主子身份冲突，即使无 `user_id`）；
   `TryParseOwnerTrn` 要求 `parts[2]`（region 槽）必须为空、`user/<id>` 的 id 非空，
   保持真实 `trn:iam::<account>:root|user/<id>`；viewer 关键字段缺失仍判 `Unknown`。
   回归：`ScopeValidationCases`（subUserVsRootMismatch / subUserVsOtherUserMismatch /
   subUserMissingRootUnknown）、`ScopeFingerprintCases`（nonEmptyRegionSlotUnknown /
   emptyUserIdUnknown）。
2. **面板缓存复用绑定本次查询（`ViewState.cs` / `TrayApp.cs`）**：
   `HasReusableData` 增加 `_pendingScopeKnown && _pendingScope.Matches(ConfirmedScope)`，
   确保取消 / 失败仅在**本次**同 scope 时复用；auth 失败 / 身份 unknown 清空并清旧身份提示。
   新增**共享终态处理器** `TrayApp.ApplyFinalOutcome`，由 UI 运行时与冒烟测试共用，
   严格按最终 `QueryOutcome.AuthConfirmed` 决定，不依赖异步 `Progress` 顺序。
   回归：`PanelModelReuseTiedToCurrentQuery`、`PanelModelAuthHintClearing`；
   冒烟新增“无 / 延迟 progress 下的终态”断言。
3. **托盘切换消费（`TrayApp.cs` `HideController`）**：新增一次性
   `ConsumeToggle(actualVisible)` 并重置；`Toggle(now, actualVisible)` 捕获**真实**可见性、
   取消 pending 并停 hideTimer；长按期间 `Evt` 在有捕获时不再排队隐藏，
   避免 mouseup 前隐藏导致重新打开；缺 MouseDown 的下一次 Click 用当下 `Visible` 而非旧捕获。
   回归：`HideControllerCases`（deactivate-before-down / longpress / no-down-next-click / menu）。
4. **首启 banner 与告警（`TrayApp.cs`）**：删除未使用字段 `_firstRunBanner`（消除 CS0414）；
   记录实际渲染的 `_introCard`，冒烟新增首启 intro 存在断言并写 `preview-intro.png`（合成自绘）。
   展示百分比已具备 `<1%` 诊断口径（`PercentFormat`）。
5. **诊断输出（`Check.cs`）**：改用 `QueryDetailedAsync`，输出
   `scope_verdict=Same/Unknown/Mismatch`（**不输出 ID / hash / viewer / 凭据**）；
   `Mismatch` 以非零退出码（4）返回。
6. **可复现集成脚本**：新增 `tests/verify-interaction.ps1`（ASCII shell，PInvoke
   `EnumWindows` + `GetWindowThreadProcessId` + `IsWindowVisible`，只操作本脚本 PID）。

> 本轮自动检查**不构成**人工视觉或真实 Explorer 交互验收；真实托盘点击 / 失焦 /
> 多屏 / 高 DPI / 键盘读屏仍为**人工未测**（T-007 承接）。历史 213 项测试为**旧记录**，
> 保留不抹除；本轮为 237 项。

## 交互改进 v0.2 最终收尾轮验证（2026-10-03，真实执行）

范围：四项最小修正（scope 空白字段归 Unknown、取消终态权威清缓存、
`Check.Remaining` 与 UI 口径统一、文档收尾），**不扩大范围、不改业务规则**。
环境：Windows，无 dotnet SDK；`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5）。

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 构建 | `build.ps1` | 通过：3 个 exe，**无编译告警** |
| 单元测试 | `test.ps1` | 通过：**passed: 245, failed: 0**（复核修复轮 237 基础上新增 8 项空白/缺失回归） |
| 离线 GUI 冒烟 | `ark_left.exe --smoke-test` | 通过：退出码 0；含首启 banner + `preview-intro.png` + **未确认取消清缓存**断言 |
| 交互集成（隔离环境） | `tests/verify-interaction.ps1` | 通过：8 项 ok，退出码 0 |
| 真实 CLI 查询 | `check.ps1` | **主代理已执行**：`scope_verdict=Same`、`status=Ok`（脱敏，无 ID / 余额）；**最终修正轮未再远端重跑** |

### 收尾修正项

1. **空白 / 缺失字段不再当冲突**（`src/Identity.cs` `ScopeValidation.Validate`）：
   以 `IsNullOrWhiteSpace` 判定字段“存在性”；account / profile / region / project /
   user 为空或纯空白一律视为**缺失 → Unknown**，非空值仍按原样严格比较（不 trim、
   不子串匹配），避免误改真实 ID 或错配。回归新增 8 项（whitespaceAccount / emptyAccount /
   whitespaceProfile / whitespaceRegion / whitespaceProject / whitespaceUser /
   rootWhitespaceAccount 均 Unknown；`" 123456789 "` 仍 Mismatch）。
2. **取消终态权威清缓存**（`src/TrayApp.cs` `ApplyFinalOutcome`）：`Cancelled` 且
   `AuthConfirmed=false` 时先 `ApplyAuthResult`（硬清候选缓存）再 `ApplyCancelled`，
   即使此前 Progress 已把同 scope 数据铺到界面也不会被取消复活。
   冒烟新增回归：先制造“已缓存同 scope”状态，再投递未确认取消，断言 `Model.Last == null`
   且终态为 `Error`。
3. **诊断百分比与 UI 一致**（`src/Check.cs` `Remaining`）：改用
   `PercentFormat.Remaining`；极低但非零显示 `<1%`，真实 0 显示“已用尽”，不再出现误导性的 `0%`。
4. **文档收尾**：T-020 独立复核与自动验证本轮通过 → `done`；T-007 人工保留 `pending`；
   测试数量更新为 **245**（历史 213 保留不抹除）；本次真实 Agent scope 已确认 `Same`。
   **不把其他套餐真人试验或托盘手感记为通过。**

> 本轮自动检查与真实 `scope_verdict=Same` 确认**不构成**人工视觉 / 真实托盘手感验收；
> 多屏 / 高 DPI / 键盘读屏 / 其他套餐真人数据仍为**人工未测**（T-007 承接）。

## v0.3（UX011）T026 收尾轮验证（2026-10-03，真实执行）

范围：本轮由 ds41-writer 在**独立会话**承接 T026 收尾与 T027 证据文档；
**不重做**旧 Sol T026 已落地的 `src/TrayApp.cs` / `tests/QuotaTests.cs` / 文档。
冻结范围内仅做两处**路径 correctness 修复**（`build.ps1`、`tests/verify-interaction.ps1`
支持绝对 `-OutputDir`），其余为证据 / 状态文档如实同步。
环境：Windows，无 dotnet SDK；`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
（C# 5，.NET Framework 4.8）。本轮**未联网、未真实查询、未 kill / restart 正式 PID 16252**；
所有起进程均为本会话自建。

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 构建 | `build.ps1 -OutputDir bin-validation` | 通过：生成 3 个 exe，无告警 |
| 单元测试 | `bin-validation\ark_left-tests.exe` | 通过：**passed: 368, failed: 0**（历史 245 项保留） |
| 离线 GUI 冒烟 | `bin-validation\ark_left.exe --smoke-test` | 通过：退出码 **0**；未创建任何 state 目录（不写 marker） |
| 隔离交互集成 | `tests\verify-interaction.ps1 -OutputDir bin-validation` | 通过：**8 项 ok，退出码 0**；仅操作脚本自建 PID |
| 绝对路径复测 | `build.ps1 -OutputDir D:\ark_left\bin-validation` 与同名集成脚本 | 均退出 0；修复前 `Join-Path` 会拼成 `D:\ark_left\D:\ark_left\bin-validation`（修复前已实测确认） |
| 真实 CLI 查询 | `check.ps1` | **本轮未执行**（禁止真实联网查询）；历史主代理 `scope_verdict=Same` / `status=Ok` 保留 |

过程观察：

- 冒烟**未创建**隔离 `ARK_LEFT_STATE_DIR` 目录 → 不写用户 / 隔离 marker。
- 集成脚本使用不存在的 `ARK_LEFT_CLI` + 隔离 state / 实例名；流程 `--show` 可见 →
  `WM_CLOSE` 隐藏不退出 → 第二实例无 `--show` 退出 0 并唤起 → 单实例 → 静默启动 →
  `--show` 唤起，均为 **ok**。

### 本轮代码审查结论（对照冻结的 UX011 验收点，未改动代码）

主代理关注点逐项核对（只读源码 + 上述自测）：

1. **DisplayKey 与真实 renderer 字段同步**：`DisplayKey`（`src/TrayApp.cs`）收集产品
   `ProductTitle` / 订阅 known 文案 / 产品错误 / `Periods.Count`，及每周期
   `LabelDisplay` / `Error` / `PercentKnown` 的 `RemainingForBar` 文案与 `"R"` 原值 /
   `AmountKnown` 数值 / `HasReset` 时间 / `UnknownNote` 条件；**忽略 `fetchedAt`、
   服务端 `updated_at` 与未展示的 `used` / `total`**。与 `BuildProductCard` / `BuildPeriodRows`
   实际渲染字段一致；`ui.subscriptionKnownCompared` / `ui.unknownPercent` / `ui.periodError`
   用例覆盖订阅 known、未知、桶错误的 key 变更。
2. **相同宽度 Reflow 不重建 / 首次初始化顺序**：`CardPanel._laidOutWidth` 仅当**实际内部宽度**
   变化时 `Reflow`；`OnHandleCreated` 先 `RunLayout`，`BuildProductCard` 设 `Width` 后
   `ForceLayout`，`RenderCards` 先定 `ClientSize` 再 `PerformLayout`，避免高度 / handle /
   再打开触发重建。冒烟 `AssertSameControls`（waiting / same-data-new-time / failure /
   same-size-open）与 `ui.sameCard` / `ui.sameChild`、`actualRefresh.waitSameChild` 断言引用不变。
3. **未知错误不显示 0**：`PercentFormat.Remaining` 对 `<=0` 返回“已用尽”、`0<v<1` 返回 `"<1%"`；
   未知走“剩余未知”，`QuotaBar.Value=-1` 画空条，未知 / 桶错误**不补 0**；`ui.unknownPercent` 断言。
4. **smoke 覆盖 Production 刷新按钮与打开路径**：
   - `ActualRefreshButtonCases` 走**真实** `RefreshRequested` → 生产 `StartRefresh` →
     `SnapshotController.Refresh`，验证按钮常驻可点、双击 single-flight（`queries==1`）、
     等待期子控件 / footer 时间 / 滚动 / 焦点不变、失败保留。
   - `ActualOpenEntryCases` 通过生产 `OpenEntryForTest` 逐项调用真实 `QueueShow`（firstRun / --show）、
     `OnExternalShow`（IPC）、`ShowFromMenu`、`OnTrayMouseDown` + `OnTrayClick`，分别断言可见且 `queries==0`。
   - 实际第二进程 IPC 信号另由隔离脚本验证；**不假称**真实 Explorer 点击计数已验收。

### 本轮未测（不记为通过）

- 真实账号查询、真实身份 A→B 切换、真实 Explorer 托盘点击 / 失焦 / 右键菜单、多屏 / 高 DPI
  视觉、键盘 / 读屏、剪贴板 / 记事本真实调用——延续 **T-007 `pending`**。
- T026 待**主代理复核**，T027 待**独立验证**；Q005 仍 `DECIDED`，未闭环。
- 正式 `bin` 仍为 v0.2；`start.cmd` 未改，v0.3 需按 README 说明从 `bin-validation` 运行
  或正式重建 `bin`。

### 源码快照（SHA256，供无 Git 复现）

本项目无 Git，按 [`README.md`](README.md) §5 记录相关文件 SHA256；机器可读清单为
`bin-validation/t026-sha256.csv`（正常生成证据输出）。

| 文件 | SHA256（本轮构建 / 单测版本） |
| --- | --- |
| src/PersistentState.cs | AEC25602B7CEB691F3AA961A86B2E6AB02B692D0013CB2CAC8F015C2011C3F80 |
| src/ViewState.cs | A014CC245C6DDC08882CF15E36B7AD47700D5FF60B03F48A54F25C402008C8D7 |
| src/TrayApp.cs | C9933F9B563D00E589EF97D40C838C1109B56A12E5AA10FF512DD16A89528CAC |
| tests/QuotaTests.cs | 379AB565D1427B63E1624EE3E5C230D043258F4499DDB4BEB05A627C97E6F187 |
| tests/verify-interaction.ps1 | DDF7EEFF5DDE3EE626DA6A3971548F3EB1626FE07EC88D08458BECA56483AEFC |
| build.ps1 | 18DA5F5B5730D4A369C73B3BA5B41BB936CA114F656D4FA5A20D87232D51FE22 |

> 说明：上表与 `docs/tasks.md` T025 记录中的**旧**快照不同，是因为 T026 已修改
> `src/TrayApp.cs` / `tests/QuotaTests.cs`，本轮又修正 `build.ps1` / `tests/verify-interaction.ps1`；
> 旧值属**历史轮次**，保留不抹除。**本轮未执行真实查询**；测试数量由旧记录的 334 / 245
> 更新为当前源码的 **368**，历史记录保留。

## v0.3（UX011）T027 主代理独立验证

日期：2026-10-03。采用业务 `v0.1` + UX011 `v0.3` + 缓存格式1。**实际执行者为主代理；Sol fallback writer 仅记录主代理已验证事实，未重跑测试。** 主代理独立按 UX011 A–F 审查 Controller、PanelModel、DisplayKey 及对应测试，无新增实现缺陷，并接受 T025 数据实现、T026 展示实现和 T027 独立自动验证。

环境：Windows NT 10.0.26200.0；PowerShell 5.1.26100.9444；Framework64 v4.0.30319 csc，C# 5 / .NET Framework 4.8。

主代理本轮实际命令与结果：

- `Test-Path -LiteralPath D:\ark_left` 成功后，执行 `powershell -NoProfile -ExecutionPolicy Bypass -File D:\ark_left\build.ps1 -OutputDir D:\ark_left\bin-release`：3 个 exe 构建成功，无告警。
- `$env:ARK_LEFT_STATE_DIR='D:\ark_left\bin-release\t027-test-state'; & 'D:\ark_left\bin-release\ark_left-tests.exe'`：**passed 368 / failed 0，退出 0**。
- 独立 smoke：`$env:ARK_LEFT_STATE_DIR='D:\ark_left\bin-release\t027-smoke-state'; $env:ARK_LEFT_INSTANCE_SUFFIX='t027-release-smoke'; $t027Smoke=Start-Process -FilePath 'D:\ark_left\bin-release\ark_left.exe' -ArgumentList '--smoke-test' -PassThru; $t027Smoke.WaitForExit(20000); $t027Smoke.ExitCode`：20 秒内退出，exit 0，隔离 state 不存在。
- `powershell -NoProfile -ExecutionPolicy Bypass -File D:\ark_left\tests\verify-interaction.ps1 -OutputDir D:\ark_left\bin-release`：**8 项 ok，退出 0**。

源码版本核对由主代理执行：`src/PersistentState.cs`、`src/ViewState.cs`、`src/TrayApp.cs`、`tests/QuotaTests.cs`、`tests/verify-interaction.ps1`、`build.ps1` 当前 SHA256 全部与 `bin-validation/t026-sha256.csv` 匹配，源码未变。版本清单：`bin-release/t027-sha256.csv`，由主代理生成，覆盖 `src/*.cs`、测试源码与交互脚本、`build.ps1`、改动文档及 3 个 exe。应用 `bin-release/ark_left.exe` SHA256：`C7A4F5CE5908E5E782A77D9F619C9CA03489EE549566E37034CFB8660A58AAC6`。

| 需求 | 主代理独立证据 | 已验证与未测边界 |
| --- | --- | --- |
| UX011 A | `controller.*`、`actualEntry.*`、隔离 IPC | 打开零查询、启动后台一次、隐藏轮询、single-flight 自动验证通过。 |
| UX011 B | `actualRefresh.*`、`ui.same*`、smoke | 等待期间数据 / 时间 / 焦点 / 滚动保持，同语义引用保持；真人手感未测。 |
| UX011 C | `ui.empty`、`emptyWaitSame` | 首次空态与等待保持，未知不假 0。 |
| UX011 D | smoke 冗余文本断言、主代理审阅合成 `preview.png` | 简化内容确认；合成预览审阅不构成人工桌面验收。 |
| UX011 E | `PersistentSnapshotCases`、`cache.*` | DPAPI、损坏拒绝、时间保持、原子失败保旧、重启恢复自动验证通过。 |
| UX011 F | `controller.*`、`progressController.*` | 新 scope 立即清、NotLoggedIn / Mismatch / Unknown、普通失败保留；真实账号切换未测。 |
| R001 | 处理器 / 布局、隔离 IPC 自动证据 | 自动证据通过；真人多屏托盘未测。 |
| R002 | `PercentWins`、`ZeroIsValid`、`MissingIsUnknown` | 合成数值及 UI 验证；其他套餐真人数据未测。 |
| R003 | 失败 / CLI 缺失 / 未登录 / 未订阅 / partial 合成场景 | 自动证据通过；本轮无真实 CLI / 联网查询。 |
| R004 | 刷新按钮、WM_CLOSE 隐藏、单实例自动证据 | 自动证据通过；真人退出菜单未测。 |

结论：**T025 / T026 / T027 done，Q005 CLOSED（仅技术决定闭环）**。T007 人工仍 pending，系统 Gate 未通过，非系统交付。本轮无真实 CLI / 联网 / 真实账号切换。`bin-release` 新部署候选已备好，未替换旧运行实例；主代理当轮观察 PID 16252 仍运行旧 `bin` exe，未停止、未覆盖，`start.cmd` 仍默认 `bin`。体验新版时先退出旧托盘，再运行 `bin-release\ark_left.exe --show`，不能同时双实例切换新版。

## v0.4（UX012）T031 主代理独立验证

日期：2026-10-03。采用业务 `v0.1` + 交互 `v0.4 UX012` + 额度缓存 `格式 1` + floating 设置 `格式 1`。**实际执行者为主代理；实施 writer 会话已结束（本次收尾 writer 仅记录主代理已验证事实，未重跑测试、未承担独立审查）。** 主代理独立审阅并接受 T029 需求登记、T030 v0.4 UX012 实现（含修复轮 2/2）与 T031 独立自动验证。

环境：Windows NT 10.0.26200.0；PowerShell 5.1.26100.9444；Framework64 `v4.0.30319` csc，C# 5 / .NET Framework 4.8。

主代理本轮实际命令与结果：

- `Test-Path -LiteralPath D:\ark_left` 为真后，执行 `powershell -NoProfile -ExecutionPolicy Bypass -File D:\ark_left\build.ps1 -OutputDir D:\ark_left\bin-v04`：**3 个 exe 构建成功，无告警**。
- 隔离 `$env:ARK_LEFT_STATE_DIR='D:\ark_left\bin-v04\t031-main-state'` 后执行 `D:\ark_left\bin-v04\ark_left-tests.exe`：**passed 518 / failed 0，退出 0**；env 已恢复。
- `$env:ARK_LEFT_STATE_DIR='D:\ark_left\bin-v04\t031-main-smoke-state'`（预先不存在）、`$env:ARK_LEFT_INSTANCE_SUFFIX='t031-main-smoke'`，`Start-Process -FilePath D:\ark_left\bin-v04\ark_left.exe -ArgumentList '--smoke-test' -PassThru` + `WaitForExit(20000)`：**20 秒内退出，exit 0，未创建 state**；env 已恢复。合成预览 circle 75 / 0 / 100 / unknown 及 settings；主代理审阅预览 75 / 100 / unknown / settings，**不构成人工桌面验收**。
- `powershell -NoProfile -ExecutionPolicy Bypass -File D:\ark_left\tests\verify-interaction.ps1 -OutputDir D:\ark_left\bin-v04`：**8 项 ok，退出 0**；仅操作自建 PID，**不联网 / 无真实 CLI**。
- 应用 `bin-v04/ark_left.exe` SHA256：`4F4DE91A4E18FA8677C996102FA545FEB8D02234686842221F339DA034277ED3`。
  版本清单：`bin-v04/t031-sha256.csv`（主代理生成）。T027 清单主代理核对：只有 `src/TrayApp.cs`、`tests/QuotaTests.cs`、`docs/tasks.md`、`docs/decisions.md`、`docs/requirements/interaction-improvements.md`、`docs/contracts.md` 有既定修改，另加新 `src/FloatingQuotaForm.cs`；其余 baseline 文件（含 `bin`、`bin-release`）未改（后续文档同步会再变，源码其余不变）。
- 本轮未停止 / 替换用户进程或正式 `bin`；主代理当前查旧 PID 16252 **已不存在**（原因未确认，**不假装仍运行**），未改 `start.cmd` 默认 `bin`。新候选 `bin-v04`；用户如有旧实例，退出托盘后运行 `bin-v04\ark_left.exe --show`。

覆盖简表（每项标明自动已验证与人工未测边界）：

| 需求 | 主代理独立证据 | 已验证与未测边界 |
| --- | --- | --- |
| UX012 A（圆圈 / 水波 / 真穿透） | 真实生产鼠标 down / move / up 拖动阈值单测、椭圆 Region、DPI 缩放、0 空 / 100 满水位、unknown 不假 0 | 自动已验证；**多屏 / 高 DPI 视觉与真人托盘手感人工未测** |
| UX012 B（选择 / 设置记忆） | 配置选择加载、缺失保留、坏版本 / extra 字段、原子失败保旧、真实共享菜单 `settings` 保存 / cancel / 0 query | 自动已验证；**真实账号多套餐切换人工未测** |
| UX012 C（详情布局 / 恢复） | 第一次静默菜单打开初始化 circle、详情按圆圈所在 screen 与实际 size、窄屏非零 fallback 及 auto-restore rule、双窗口同 PanelView 身份清理同步 / unknown no 0 | 自动已验证；**真实 Explorer / 多屏 / 键盘读屏人工未测** |
| UX011（R001–R004 保留） | 原 single-flight / 后台 poll / cache stability 保持；R001–R004 自动证据 | 自动已验证；**真人退出菜单 / 真实账号切换人工未测**（T-007 pending） |

结论：**T029 / T030 / T031 done（主代理接受）**，`Q-006 CLOSED`（T031 闭环）；业务 `v0.1`、UX012 `v0.4`、缓存 `格式1`、floating `格式1` 为采用版本。T007 人工仍 pending，**系统 Gate 未通过，非系统交付**。本轮无真实 CLI / 联网 / 真实账号切换。

## 文档检查记录

### 初始化检查（历史真实记录，保留）

| 检查项 | 对象 | 结果 | 依据 |
| --- | --- | --- | --- |
| 文件存在性 | 全部 7 个文档 | 通过 | 主代理逐文件读取确认 |
| 路径与链接一致性 | README 目录导航 | 通过 | 主代理逐文件读取并核对与仓库结构一致 |
| 未虚构业务/接口 | requirements / contracts | 通过 | 主代理逐文件读取确认均为待定义占位 |

- 检查日期：2026-10-03
- 检查方式：主代理逐文件读取并核对，未运行任何脚本或应用。
- 检查范围：全部 7 个文件。
- 版本状态：本次检查的是**未纳入版本控制的初始化文档**。
- 说明：以上"通过"仅指文档层面的人工核对，**不声称任何应用验证**。

### 本轮复核（v0.1，应用已实现）

| 检查项 | 对象 | 结果 | 依据 |
| --- | --- | --- | --- |
| 需求已具体化并提升为 v0.1 | docs/requirements.md | 通过 | Q-001 已关闭，版本 v0.1 |
| 未把建议写成用户确认 | 全部文档 | 通过 | 设计建议显式标注【设计建议】 |
| 数据字段来源已由真实查询确认 | docs/contracts.md | 通过 | `usage plan` 真实脱敏输出，viewer 不落盘 |
| 未虚构代码目录 / 实现完成 | docs/tasks.md / README.md | 通过 | 任务状态与证据对应真实文件 |
| 未决问题已登记 | docs/decisions.md | 通过 | Q-001 CLOSED，Q-002 / Q-003 DECIDED |
| 未存储真实身份样例 | src / tests | 通过 | 源码仅含合成匿名 fixtures，无 viewer / ID |

### 本轮文档复核与需求目录迁移（2026-10-03，在 v0.1 复核之后）

- 范围：**仅文档**。未修改应用 / 脚本，未重启任何运行程序，未重新执行构建 / 测试 / 查询。
- 动作：建立 `docs/requirements/`（`README.md` 索引、`product-requirements.md` 正式需求、
  `implemented-behavior.md` 已实现行为）；`docs/requirements.md` 改为兼容入口；
  同步 `README.md` / `AGENTS.md` 的路径引用。
- 编号与版本：`R001–R004`、`v0.1` **原样保留**，未改业务规则。
- 依据：本轮读取 `AGENTS.md`、`docs/` 各文档与 `src/` 真实代码
  （`Program.cs` / `TrayApp.cs` / `QuotaCli.cs` / `QuotaParser.cs` / `Models.cs` / `Check.cs`）
  做实现事实梳理。

| 检查项 | 对象 | 结果 | 依据 |
| --- | --- | --- | --- |
| 唯一权威需求基线 | docs/requirements/ | 通过 | 正文仅存于新目录；旧 `requirements.md` 为入口 |
| 编号 / 版本未漂移 | R001–R004、v0.1 | 通过 | 新目录沿用原编号与版本 |
| 需求与实现差异已登记 | implemented-behavior.md 第 15 节 | 通过 | 超时口径、失焦隐藏等逐条对照，未反向改业务 |
| 验证分级未夸大 | product-requirements / implemented-behavior | 通过 | 明确已实现 / 自动已验证 / 人工待验证 / 未实现四分 |
| 未声称本轮运行测试 | 全部新增文档 | 通过 | 81 项标注为**既有最近记录**，本轮仅文档复核 |
| 未填真实余额 / 账号 ID | 全部新增文档 | 通过 | 数据快照留空 / 匿名 |
| 链接与相对路径 | 新目录 3 文件 + README / AGENTS | 通过 | 逐条用 `Test-Path` 解析，全部 resolve 到真实文件；详见下方 |
| 时间语义按源码核实 | implemented-behavior.md 第 9 节 | 通过 | `reset_at` 仅字符串；`updated_at` 可 epoch 毫秒 / 字符串 |
| 内存清空边界按源码核实 | implemented-behavior.md 第 5、11 节 | 通过 | `ShowPanel` 先显示 `_last`；防重入 / 隐藏刷新不清空 |
| 隐私表述按源码核实 | implemented-behavior.md 第 12 节 | 通过 | `JavaScriptSerializer` 反序列化整份 JSON，只提取业务字段 |

**相对链接解析核对（本轮真实执行）**：对 7 个已改文档的 Markdown 链接逐条用
`[System.IO.Path]::GetFullPath` + `Test-Path` 解析，结果全部 `OK`（resolve 到存在的文件）。

- `docs/requirements/` 下文档到源码使用 `../../src/*.cs`：实测 resolves 到
  `D:\ark_left\src\*`（存在），为本轮采用的正确前缀。
- **链接前缀复核（已关闭）**：曾出现“改用 `../../../src/`”的建议，实测
  `../../../src/Program.cs` from `docs/requirements/` resolves 到 `D:\src\Program.cs`
  （**不存在**），且 `docs/src` 也不存在。**主代理已采纳实际解析结果**：
  正确前缀为 `../../src/`，原建议作废；`../../../src/` 会断链，不再使用。
  本次以工具复核为准，结论已闭环，无需用户确认。
- `tests/` 未被新文档以相对链接引用（仅在正文以文字提及），故无 tests 链接需调整。
- 全仓库 Markdown 链接复核：43 条，缺失 0。

> 本轮**不重置**任何历史状态：T-006 仍 `done`，T-007 仍 `pending`，Q-001 `CLOSED`、
> Q-002 / Q-003 `DECIDED` 保持不变。上述“通过”仅指文档一致性核对，**不构成应用验证**。
> 本轮为**非功能性文档修正**，不提升需求版本（仍 `v0.1`）。

## 证据要求

每条验收记录应能追溯到：执行的命令/操作、运行环境、代码或文档版本、结果位置。
未实际执行的项标注"未执行"或"人工未测"，不写"通过"。

## 应用验证项

- 构建命令、启动命令、测试命令：已确定，见上方。
- 验收场景覆盖情况：见上表。
- 未验证项：托盘视觉交互、多屏 / 高 DPI 视觉定位、真实未登录场景。

## Definition of Done（适应风险）

- 按改动风险选择匹配的检查；不适用的检查说明原因，不记为通过。
- 影响本次验收的关键问题未关闭时，不得标记完成。
- 未执行或无法执行的检查必须显式标注，不得声称已通过。
- T-006 已由主代理独立复核完成（`done`）；T-007 人工视觉 / 真实托盘手感保留 `pending`。
- T-020 独立复核与自动验证本轮通过（`done`）；其自动证据**不替代** T-007 人工验收。

## 已知限制（如实）

1. **多屏 / 高 DPI / 托盘点击与右键菜单交互人工未测**（T-007 `pending`）：代码在打开时用
   `Screen.FromPoint(Cursor.Position)` 记录工作区，并以 `GetDpiForMonitor` / `DeviceDpi`
   适配；烟雾测试仅断言主屏 `Bounds` 与内部控件边界，**不构成视觉验收**。
2. **托盘图标可能被 Windows 收入折叠区**：系统行为，不承诺强制可见。
3. **未构造真实未登录 / CLI 缺失环境**：这两类状态由 `ark_left-tests.exe` 测试模式覆盖。
4. **Coding Plan 若仅返回 percent**：剩余量与总量标为未知，仅显示剩余百分比；
   单位统一写作"额度"，**未验证其是否为 Token**。
5. 子进程超时固定 30s，超时 / 取消 / 退出都会终止活动子进程；`Dispose` 后拒绝新进程。
6. `--smoke-test` 只验证布局边界、子控件边界、固定尺寸与隐藏语义，**不是视觉 / 交互验收**；
   `bin/preview*.png` 为合成窗口的自绘合成图，非桌面截图，且不含真实账号数据。
7. 交互改进 v0.2 的**真实托盘点击 / 失焦 / 右键菜单 / 多屏 / 键盘**仍人工未测（T-007 承接）；
    自动用例（含 v0.5 生产 Timer 注入测试）不等于真人交互验收。
8. 身份 scope 指纹仅内存；真实身份 A→B 切换未构造，由合成用例覆盖逻辑。

## v0.5（UX013）T032 主代理独立验证

日期：2026-10-03。采用业务 `v0.1` + 交互 `v0.5 UX013` + 额度缓存格式 1 + floating 设置格式 1。**实际执行者为主代理；本轮文档收尾执行者 tl-openai/gpt-6-luna 仅记录主代理提供的事实，未重跑验证。** 主代理已独立审阅并接受 T032 实现；本章仅覆盖代码与自动检查验收，不代表系统交付或 T007 人工验收。

环境：Windows 10.0.26200.0；PowerShell 5.1；C# 5，Framework64 `v4.0.30319` csc / .NET Framework 4.8。

主代理本轮实际命令与结果：

- `build.ps1 -OutputDir D:/ark_left/bin-v05`：**3 个 exe 构建成功，无告警**。
- 隔离 `ARK_LEFT_STATE_DIR=D:/ark_left/bin-v05/t032-main-state` 后执行 `bin-v05/ark_left-tests.exe`：**passed 532 / failed 0，退出 0**。包含实际 10 秒生产 WinForms Timer 注入 query 测试；使用合成离线数据，不调用 CLI。
- `verify-interaction.ps1 -OutputDir D:/ark_left/bin-v05`：**8 项 ok，退出 0**，suffix `43a42a5e`。
- 隔离 `ARK_LEFT_STATE_DIR=D:/ark_left/bin-v05/t032-main-smoke-state` 与 `ARK_LEFT_INSTANCE_SUFFIX=t032-main-smoke` 执行 `bin-v05/ark_left.exe --smoke-test`：**exit 0，未创建 state**。
- `bin-v05/ark_left.exe` SHA256：`BE0F0CD426D2D7171BC96D926ED325BC3DF56D60B6451E81694A25DB0967A852`。`bin-v05/t032-sha256.csv` 计划由主代理在本次文档编辑完成后生成；**此处记录时尚未生成，未验证**。
- 正式 `bin/` 仍 v0.2，`start.cmd` 未改，v0.5 候选 `bin-v05/` 未部署。无 CLI / 账号 / 网络查询。

覆盖简表：

| UX013 / 保持规则 | 主代理独立证据 | 边界 |
| --- | --- | --- |
| 圆圈或详情任一可见 10 秒；均隐藏 5 分钟；设置单独可见不提速 | `PollIntervalUX013Cases` 与真实生产 WinForms Timer 注入查询测试 | 自动验证通过；T007 真实桌面、多屏、auth 与真人交互仍未测 |
| 显隐只改变间隔，不立即查询；相同间隔不重置 countdown | `UpdatePollInterval` 生产路径及 UX013 单测 | 自动验证通过 |
| 启动查询、single-flight、缓存与数据语义不变 | 全量 532 项离线测试及既有用例 | 无真实 CLI / 账号 / 网络验证 |

结论：**T032 done，仅表示实现与独立自动检查验收通过**；T007 人工验收仍 pending，**系统 Gate 未通过，非系统交付**。历史 T031 的 518/518 与 `bin-v04` 清单保持不变。

## T038 全局 GLM 工具链配置验证（2026-10-04，短记录）

范围：**项目外、用户授权**的全局 opencode 配置；不改本项目业务与源码。真实执行会话 `ses_efd242e48ffeBFydOCRFGdF6R9`，范围内修复 **1/2**、逻辑累计 **1**。
环境：Windows / PowerShell 5.1 / OpenCode 1.18.18。

- 修复原因：GLM Responses 映射返回空文本；改为**仅 GLM** 的 per-model SDK 走 `chat/completions`（npm `@ai-sdk/openai-compatible`）。实际后端 `tl-openai/glm-5.3-flash`；agent `glm-implementer` 配置 tool alias `tl-openai/glm-5.3-flash-gpt-patch`，由 `glm-patch-routing.ts` 仅重写请求 model 为实际 GLM，支持 `apply_patch`。
- 主代理独立离线桩（2026-10-04）：`node %USERPROFILE%\AppData\Local\Temp\opencode\verify-glm-agent.cjs` → **6/6**，覆盖 JSON 模型字段、GLM per-model SDK、agent frontmatter、routing source 与 dynamic stub remap；`node import` warning 属 module 类型提示，**未改 package**。
- 主代理真实模型图片核验：

```powershell
# cwd: %USERPROFILE%\AppData\Local\Temp\opencode
opencode run --model tl-openai/glm-5.3-flash-gpt-patch --format json `
  --title "GLM final image verification" `
  --file "D:\ark_left\bin-v05\preview-floating-100.png" `
  -- "Describe the attached image in one sentence and state the large percentage. Do not use tools or edit any file."
```

  exit 0，会话 `ses_efd1c6d0effez3UgZPEzoHmWtM`，准确读 `100%` / `Agent Plan` / `5小时`。

边界（显式未验证 / 不计通过）：

- 此前一次 `--agent` CLI fallback 走默认代理，**不计** GLM 证据；另一次 GLM 空文本**不计**通过。
- **真实图片模型证据 ≠ GLM 子代理真实代码写入**；当前父会话 tool 未注册 GLM，重启生效；**不声称当前父会话已用 GLM 实施源码**。
- 实际 / alias 模型 context `220000` / output `30000`、`attachment:true`、`tool_call:true`、modalities input `text,image` / output `text`；**仅配置上限，未做极限容量压力测**。
- 版本标识：`bin-validation/t038-sha256.csv` 将由**主代理在本轮文档完成后生成**；**记录时尚未生成，不提前声称已生成**。
- **系统交付 Gate 未通过，非系统交付。**

## T035 设计接受 / T036、T037 规范验收（2026-10-04，短记录）

- **T035 设计接受**：主代理 2026-10-04 独立审阅接受设计；**产品实施仍 `pending`**。
- **T036 / T037 规范验收**：`done`，**仅指设计与规则经主代理独立审阅接受，非产品交付**。
- 实际执行者：主代理独立局部审阅（读 `docs/development-plan.md` 1–105、`AGENTS.md` 29–105、`docs/tasks.md` 58–65、`docs/README.md` 15–28、本文件 524–547）；设计 writer 会话 `ses_efd1a4496ffeSEkR3nC2h0JO8e`（本轮第 1 轮返修 1/2、逻辑累计 1）执行程序化相对链接与存在性检查。
- **未重跑应用测试**（离线 / 构建 / 单元 / smoke 均未执行）。**T035 产品 pending、T034 未验收、T007 pending**。相对链接检查正常。

## T039 子代理上下文控制规范落盘（2026-10-04，纯规范短记录）

- **任务**：T-039 纯文档规范落盘；目标为子代理上下文防膨胀策略，**不实现自动 token 监控**，不改 global config / 源码 / bin，不跑应用 / CLI / model，不部署。
- **实际写入**：`docs/development-plan.md` 新增 **§9 子代理上下文控制**（唯一详细执行规范）；`AGENTS.md` 会话段短引用；`docs/README.md` 索引与按需读取处短链接；`docs/tasks.md` 会话生命周期措辞与模板字段、任务表 T-039 行；本记录。
- **实际执行检查（ds41-writer）**：最小 `apply_patch` 落盘；程序化相对链接 / 章节编号 / 表格列数完整性检查；Windows / PowerShell（主代理已知 5.1）环境。**未实现自动监控、未跑应用测试**。
- **子代理必读窗口摘要**：沿用本任务首次交接返回范围（AGENTS 80–97/37–49、development-plan 63–90/103–108、tasks 50–65/488–511/525–556、README 20–28/88–92/105–116、verification 551–556；修正轮仅读 development-plan 112/119、tasks 65、verification 558–564）。
- **未测 / 风险**：自动按 token 中止与实时监控当前**不存在**；预算数字为项目操作策略值，非模型服务端硬上限、非已触发实际用量。**GLM 示例**：`C=220000` / `O=30000` / `B=30000`，预警 140k / 交接 160k（限 GLM）；**本轮 DS 后端 `deepseek-v4.1-flash`** 来源为主代理核实全局配置 `opencode.json` 272–287：`C=250000` / `O=30000`，主代理本轮 `B=30000`、预警 170k / 交接 190k（**不套 GLM 140/160**）；当前会话完整输入占用对父代理**不可见**，不以计费累计虚构余量，仅可观察信号判断。SHA256 `bin-validation/t039-sha256.csv` 由主代理文档收尾后生成，**本记录时未生成**。
- **状态**：规范阶段审查已完成、主代理独立审阅接受（窗口 development-plan 103–123 / AGENTS 90–106 / README 88–94 / tasks 63–68、500–513、531–560 / verification 558–564；**修正 1/2、逻辑累计 1**）；执行会话 `ses_efd0fe82fffez7TOf7EWUyl4ay`。T034 / T035 未验收状态与本文件旧记录不变。

## T048 主代理 v0.12 独立验证记录（2026-10-04）

范围：业务 `v0.1`（R001–R004）不变；交互 `v0.12 UX020`；额度缓存格式 1 / floating 选择格式 1 / floating 偏好格式 2（只读迁移严格格式 1）。
**实际执行者为主代理**；T048 文档收尾由 ds41-writer 执行，仅记录主代理提供的事实，**未重跑验证**、未改源码 / 脚本 / 二进制。

环境：`WinNT 10.0.26200.0`（Windows 11）；PowerShell `5.1.26100.9444`；CLR `4.0.30319.42000`；
项目 **.NET Framework 4.8**，`csc` 64 位 `v4.0.30319`（CLR 版本不等于 Framework 版本）；**无 git**。

主代理本轮实际命令与结果：

- 构建：`powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir D:/ark_left/bin-v12` → 3 个 exe 构建成功。
- 最终隔离单测：`ARK_LEFT_STATE_DIR=%USERPROFILE%\AppData\Local\Temp\opencode\arkleft-main-v12-final` 后执行 `D:\ark_left\bin-v12\ark_left-tests.exe` → **passed 963 / failed 0**。
- 隔离冒烟：`--smoke-test` **exit 0**（state dir `arkleft-main-v12-final-smoke`）。
- 升级与本地运行（授权内）：`powershell -NoProfile -ExecutionPolicy Bypass -File launch.ps1 -OutputDir bin-v12 -PreviousProcessId 15796`（实际调用参数带空格）→ 旧实例 `CLOSED pid 15796 exit code 0`，`LAUNCHED pid 9160 path D:\ark_left\bin-v12\ark_left.exe`。
  `2026-10-04T07:26:59+08:00` `Get-Process 9160` `Responding=True`；随后 `EnumWindows` 按 PID 只读验证 1 个可见窗；`Get-Process ark_left` 只剩 9160 新版。`MainWindowHandle=0` 对 tool window 可为正常情况，**不凭此标记不可见**。
- 正式 `bin` 未覆盖、历史候选未覆盖；系统 Gate / T007 人工仍 pending。

T041–T047 主代理独立验收结果（摘要）：

| 任务 | 需求 | 构建 / 单测 | 其它 |
| --- | --- | --- | --- |
| T041 | v0.6 UX014 | bin-v07 build 3 exe；**538/0** | smoke 0；8 项交互通过 |
| T042 | v0.7 UX015 | bin-v07；**668/0** | smoke 0；8 项交互通过 |
| T043 | v0.8 UX016 | bin-v08；**732/0** | smoke 0；8 项交互（suffix d631e69d） |
| T044 | v0.9 UX017 | bin-v09；**820/0** | smoke 0；8 项交互（suffix d0d2af54） |
| T045 | v0.10 UX018 | bin-v10 build 3 exe；820/0 | smoke 0；8 项交互；launcher **20 项 ok**（复核 suffix 446bc758） |
| T046 | v0.11 UX019 | bin-v11；**906/0** | smoke 0；8 项交互（suffix 19ef96de）；launcher 20 项（suffix 8bfef04b） |
| T047 | v0.12 UX020 | bin-v12 build 3 exe；首次 **959/0**、返修后最终 **963/0** | smoke 0；8 项交互（suffix 4562c4f9）；launcher（suffix 006feb38） |

版本标识：`bin-v12\ark_left.exe` FileVersion / ProductVersion **0.12.0.0**，
SHA256 `C5114937C68C1FCCE4204C79426772DB80849A99D6B7C533896DB9C33767607E`。
`bin-v12/t048-sha256.csv` 已由主代理**实际生成**（覆盖 **48 文件**：`src` 14 / `tests` 3 /
`build.ps1`+`test.ps1`+`check.ps1`+`launch.ps1`+`start.cmd` 5 / `docs` 12 / `bin` exe-ico 4 /
preview 10）；本次最终文档落盘后由主代理**刷新**以捕获最终文档，**不写未来已刷新**。
T048 纯文档收尾已由主代理接受，执行句柄 `ses_efbe4d1dcffeLACoYeQUlkmPRk`，返修 2/2、逻辑累计 2。

边界（显式未验证 / 不计通过）：

- 隔离脚本 `verify-interaction.ps1` / `verify-launcher.ps1` 在**最后小修之后未重复执行**（相关脚本与路径语义未变），
  **不伪称最终重新跑过 8 / 20 项**；表中 8 / 20 项为该任务既有主代理证据。
- T048 **未独立运行 `check.ps1`**、**未核验新真实账号 / 联网数据**；正常启动会后台调用本机已有 ArkCLI，
  **不能声称本轮绝无真实 CLI 调用**；**启动存活不等于真实额度正确**。
- **真实剪贴板 / 托盘 balloon / 视觉 / 多屏真人未测（T007 pending）**；系统 Gate 未通过，非系统交付。

## T050 GLM v0.13 自测记录（2026-10-04，writer 自测历史；T050 已由主代理独立验收接受）

**实际执行者为实施子代理 GLM `tl-openai/glm-5.3-flash`（T050 本会话，阶段1先登记 v0.13 基线 / 契约 / T050 行后再改源码）**；
以下为该任务**实际执行的隔离自动检查**，**非主代理独立验收**；T050 最终由**主代理 2026-10-04 独立验收接受**
（重跑证据见文末「T051 主代理 v0.13 独立验收记录」），本节仅作 writer 自测历史保留。
真实 PID 9160（`bin-v12\ark_left.exe`）全程未触碰（verify-launcher 运行前后各核实一次进程存活与路径，均 `9160 D:\ark_left\bin-v12\ark_left.exe`）；
真实启动 / 升级由主代理执行；真实剪贴板 / 托盘 / 多屏人工仍未测（T007 pending 范围不变）。

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 构建 | `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir D:/ark_left/bin-v13` | 通过：`ark_left.exe` / `ark_left-check.exe` / `ark_left-tests.exe` 3 exe 无告警 |
| 隔离单测 | `ARK_LEFT_STATE_DIR=%USERPROFILE%\AppData\Local\Temp\opencode\arkleft-t050-v13-tests` 后运行 `bin-v13\ark_left-tests.exe`（另以 `…smoke2` 目录复跑一次） | **passed 1021 / failed 0，退出 0**（963 基线 + 新增 UX021 用例） |
| 隔离 smoke | `ARK_LEFT_STATE_DIR=…arkleft-t050-v13-smoke` 后 `bin-v13\ark_left.exe --smoke-test`（GUI exe，经 `Start-Process -Wait -PassThru` 取回退出码） | 退出码 0 |
| 交互集成 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-interaction.ps1 -OutputDir D:/ark_left/bin-v13` | **8 项 ok**（suffix f867a354） |
| 启动器 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-launcher.ps1 -OutputDir D:/ark_left/bin-v13` | **20 项全部 ok，退出 0**（suffix 90ac039e；脚本所有 launch 经 `-PreviousProcessId` 限定自建 PID） |

版本标识（**返修1 重建后为下表「返修1」行的当前值**；首轮值随任务过程记录保留于下文）：

### T050 返修1（同会话，本会话返修 1/2、逻辑累计 1）

主代理审读发现：`DetailsShortcutUX021Cases` 的 guard 分支与真实 modal 分支未注入
`ClipboardSetForTest`、仅断言 zeroQuery——若未来 guard 回归，Ctrl+C 测试会实际写用户剪贴板而
zeroQuery 仍可能 passed。返修仅动测试（**production 实现未改**）：guard 三个分支（hidden /
menuOpen / dialogFlag / unfocused，其中 dialogFlag 原已派发 Ctrl+C）补注入 recorder 并对拒绝的
显式 Ctrl+C 断言 0 copy；真实 modal 分支对 owner details 注入 recorder，owner Ctrl+C / Ctrl+R
派发断言 `clips==0` 且 modal 返回后仍 0。新增 9 断言，未新增功能、不铺 guard 组合矩阵。

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 重建 | `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir D:/ark_left/bin-v13` | 通过：3 exe 无告警 |
| 隔离单测（复跑） | 同上隔离 env 运行 `bin-v13\ark_left-tests.exe` | **passed 1030 / failed 0，退出 0**（首轮 1021 + 返修1 新增 9） |
| 隔离 smoke（复跑） | `Start-Process -Wait` 取回退出码 | 退出码 0 |

**verify-interaction / verify-launcher 未重跑**（测试改动不触及两脚本覆盖的路径；8 / 20 项留待主代理独立执行）。
真实 PID 9160（`bin-v12\ark_left.exe`）复测后仍存活未触碰。

返修1 后版本标识：`bin-v13\ark_left.exe` FileVersion / ProductVersion **0.13.0.0**，
SHA256 `8CBEF748793F9949420DA62ECB600BD7B766FF1065E7352942A48781BF32550B`；
`bin-v13\ark_left-tests.exe` SHA256 `565AB0D31EA863CA10F07090B08022F0662668BBBF796D6F57EC868147BDFC9C`。
（首轮值：`ark_left.exe` `2339F9D746889456475F44C7D7A5EF2B520B0EB1F761A3D7025ADD94D295D255`、
`ark_left-tests.exe` `5491474A8E56499F5E951435C021BFB247549D879FA363E3020B0EB054431498`。）
`launch.ps1` 默认 `-OutputDir bin-v13`、旧目录清单新增 `bin-v12`；`start.cmd` → `bin-v13`；`src/Program.cs` → 0.13.0.0。

证据边界（显式未测 / 不冒充）：

- UX021 快捷键测试经**反射调用 protected `Control.ProcessCmdKey`**（含"聚焦子控件 → 父链 → Form override"真实派发路径；
  实施前以离线探针窗体证实该派发可达，探针即关、无输入注入）；**未做任何真实键盘输入注入**（按任务约束禁用 SendKeys / 全局热键 /
  真实桌面注入）；真实手感 / 视觉属 T007 人工范围。
- 真实剪贴板**未触碰**（单测注入 recorder）；真实 CLI / 联网 / `check.ps1` 未执行；**未更新 PID 9160，未覆盖正式 `bin` 与 `bin-v12`**；
  `bin-v12/t048-sha256.csv` 为 T048 生成时的**历史快照**（48 文件 manifest），后续任务 docs 变化**不刷新该旧 manifest**。
- T050 自测 ≠ 主代理独立验收；T050 已由主代理 2026-10-04 独立验收接受（见下节），本节自测值仅作历史保留。

## T051 主代理 v0.13 独立验收记录（2026-10-04，T050 已接受）

**实际执行者与验收者为主代理**；以下为主代理在 T050 最后测试修订后**实际重跑**的证据，
**不是**仅复用 GLM 自测，也**不是**本 writer 重跑。T051 本 writer 仅做纯文档最小 `apply_patch`，
**未改源码 / 脚本 / 二进制，未运行应用 / CLI / 网络 / 真实进程**。

### 构建与自动检查（主代理实际执行）

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 构建 | `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir D:/ark_left/bin-v13` | 成功 3 exe |
| 隔离单测 | `ARK_LEFT_STATE_DIR=%USERPROFILE%\AppData\Local\Temp\opencode\arkleft-main-v13-final-tests` 后运行 `bin-v13\ark_left-tests.exe` | **passed 1030 / failed 0** |
| 隔离 smoke | 隔离 state `…arkleft-main-v13-final-smoke` 后运行 `--smoke-test` | **exit 0** |
| 交互集成 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-interaction.ps1 -OutputDir D:/ark_left/bin-v13` | **8 项全 pass**（suffix 9d4a1d0b） |
| 启动器 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-launcher.ps1 -OutputDir D:/ark_left/bin-v13` | **20 项全 pass**（suffix d3df298f） |

### 源码 artifact 标识（主代理最终重建）

- `bin-v13\ark_left.exe` FileVersion / ProductVersion **0.13.0.0**，
  SHA256 `EE871EF245E6785305B2EDC7B604A5D7E3209864FE38A1EA2B10F6D74842ADE3`。
- `bin-v13\ark_left-tests.exe` SHA256 `52EABABCB2AE5B9203077DE432CF687FB9BADBDAC0EE20B2959275B67A02E8DA`。
（此前 GLM T050 自测 / 返修构建 hash 见上节，保留为历史；主代理重建后以本节为准。）

### 授权内真实本地升级（主代理执行，非 writer）

- 命令：`powershell -NoProfile -ExecutionPolicy Bypass -File launch.ps1 -OutputDir bin-v13 -PreviousProcessId 9160`。
- 结果：旧进程 **CLOSED**（pid 9160、exit code 0）；**LAUNCHED** pid **15260** `D:\ark_left\bin-v13\ark_left.exe`。
- 2026-10-04T08:37:09+08:00 只读核实：`Get-Process ark_left` 仅剩 pid 15260（新版）、`Responding=True`；
  `EnumWindows` 按 PID 15260 只读确认 **1 个 visible window**。
- 未覆盖正式 `bin` 与历史候选 `bin-v04`–`bin-v12`；旧实例正常 exit。

### 证据边界（显式未测，不冒充）

- **不伪造**真人键盘输入 / 真实剪贴板 / 真实托盘 / 多屏手感的独立人工验收；真实额度正确性未据此证明。
- 生产启动会后台查询本机已有 CLI；writer **未自行 run app / query**，本节命令 / 结果为**主代理提供的事实**。
- 环境沿 T048：Windows NT 10.0.26200.0、PowerShell 5.1.26100.9444、CLR 4.0.30319.42000 / .NET Framework 4.8、
  `csc.exe` 64-bit v4.0.30319。
- **T007 人工验收仍 pending，系统 Gate 未通过**；T051 为纯文档收尾，不构成系统交付。
- `bin-v13/t051-sha256.csv` 已由主代理实际生成并校验：覆盖 **48 文件、0 mismatch**；清单记录**生成时**各文件 SHA256，后续变更需重新生成（主代理将在本次最后 doc 变更后 refresh 同 manifest 以捕获最终内容）。

### T051 收尾接受

- 主代理 2026-10-04 已审阅全部相关状态窗口与关键事实，**接受本纯文档收尾（非系统 Gate）**；相对 links 检查 **0 broken**。
- 执行句柄：**真实会话 `ses_efba75eecffeKK16MP32JytHNq`**；本轮**返修 1/2、逻辑累计 1**。
- 本次之后主代理将 refresh 同一 `bin-v13/t051-sha256.csv` 以捕获最终文档内容（该最终 refresh **尚未发生**，不在本文伪写）。
- 真实 v0.13 运行事实保持：PID 15260 存活、Responding、1 visible window、后台查询事实；**T007 人工验收仍 pending**。

## T053 / T054 有界再委派规范落盘（2026-10-04，纯规范短记录；**主代理已独立审阅接受：仅规范 / 静态配置验收**）

- **范围（项目侧 T054）**：`AGENTS.md`（角色 / 文件写入 / 通信 / 会话条款短引用）、`docs/development-plan.md`（§9 条 1 措辞 + 新增 **§10** 唯一详细规范）、`docs/tasks.md`（brief 模板字段与文末 T053/T054 记录）、`docs/README.md`（§1 表与 §5 短引用）、`docs/decisions.md`（Q-007 流程决定）、本文件。**不改业务 / UI / 契约 / 源码，不跑应用。**
- **范围（全局侧 T053，六全局文件）**：`%USERPROFILE%/.config/opencode/file-writing-policy.md` + 4 个 agent definition + 外部框架 `%USERPROFILE%\multi-agent-development-framework.md` **§7 / §9.2**；**本次未改 `opencode.json`**。
- **实际执行检查（ds41-writer，仅项目 6 文件）**：最小 `apply_patch` 局部更新；`T053` / `T054` / `Q-007` 编号经主代理 grep 确认此前未使用；Windows / PowerShell 环境。**未执行**真实嵌套委派或 runtime 监控检查。
- **检查证据（主代理实际执行，仅录事实，非 writer 自测）**：2026-10-04，Windows PowerShell **5.1** + `opencode` **1.18.18**。主代理执行 `opencode --version` 与 **`opencode agent list`**，均退出成功（完整输出 `%USERPROFILE%/.local/share/opencode/tool-output/tool_104c891ad001lnAVznfKkPlahA`）；以 **`ConvertFrom-Json`** 解析各 agent 权限，断言四个 writer 各 **7 条 `task` 规则、首条 `'*': deny`、后续 6 指定 allow，六个 target 均已注册、last matching action 均 allow**，输出 **`4/4 permission checks passed; no model requests made`**。初次 **Node 管道**验证因 **Windows 引号**失败（`subagent` 误作命令），改用**纯 PowerShell** 后全绿；**未伪造初次通过**。**model 未改，未发任何 model / 业务调用**。T053 真实 session `ses_efb39863effeq03HLF4D1hI5O3`（**返修 1/2、逻辑累计 1**）、T054 真实 session `ses_efb3984f7ffeovDIoE717zejZp`（**返修 2/2、逻辑累计 2**，第 2 轮为收尾、非新任务、未清零）；均 ds41-writer。
- **显式未测**：**真实嵌套委派运行未测**，**不声称嵌套成功**；配置启动加载需**退出重启**，当前运行会话为旧加载配置。**本次为规范 / 静态配置验收，非真实嵌套运行或产品交付**。
- **生成证据计划**：`bin-validation/t053-t054-sha256.csv`（覆盖本轮 **12 个**修改文件）由**主代理在最终收尾后生成**；**本 writer 记录时尚未生成**，实际结果以产物与主代理最终核对为准。
- **边界**：T052 / UI / T007 / 系统交付 Gate 状态不变。

## T052 主代理 UX022 接管独立验收记录（2026-10-04，T052-DOC 纯文档补记）

**UX022 代码由实施子代理编写（见下方会话事实），主代理负责审阅与独立复跑，并作为最终验收者**；以下证据按行注明实际执行者，
**不是**仅复用子代理自测，也**不是**本 writer 重跑。本节由 T052-DOC（ds41-writer，纯文档）最小 `apply_patch` 补记，
**未改源码 / 脚本 / 二进制，未运行应用 / 测试 / CLI / 网络 / 真实进程**；用户本轮已授权本次接管验收。

**实现会话事实（真实句柄，不编造）**：启动接续 GLM `tl-openai/glm-5.3-flash`
（真实会话 `ses_efb1e82ceffeUTT35h1tuqx2u2`，负责 `launch.ps1` 默认 / 白名单与 `start.cmd`）；
DS4.1 `tl-openai/deepseek-v4.1-flash`（真实会话 `ses_efb187030ffepFLzeUgbDMfIXT`，负责
`src/TrayApp.cs` 与 `tests/QuotaTests.cs`，首次交付 0/2；**原逻辑累计返修轮数无法可靠确认，不伪称清零**）。
旧实施会话句柄未知，不编造。

### 构建与自动检查（注明执行者）

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 首次构建（主代理执行） | `build.ps1 -OutputDir bin-v14` | 通过 |
| 首次隔离单测（主代理执行） | `bin-v14\ark_left-tests.exe` | **1079 / 10**（10 失败，触发 DS4.1 修复） |
| 修复后构建（DS4.1 执行） | `build.ps1 -OutputDir bin-v14` | 3 exe |
| 修复后隔离单测（DS4.1 执行） | `bin-v14\ark_left-tests.exe` | **passed 1088 / failed 0** |
| 隔离 smoke（DS4.1 执行） | `--smoke-test` | exit 0 |
| 主代理独立复跑单测 | `ARK_LEFT_STATE_DIR=D:\ark_left\bin-v14\review-ux022-state` 后运行 `bin-v14\ark_left-tests.exe`（suffix `review-ux022-main`） | **passed 1088 / failed 0** |
| 主代理 smoke | `Start-Process .\bin-v14\ark_left.exe -ArgumentList '--smoke-test' -Wait -PassThru`（state `review-ux022-smoke` / suffix `review-ux022-smoke`） | **ExitCode 0** |
| 交互集成 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-interaction.ps1 -OutputDir bin-v14` | **8 项全 pass**（suffix 03102674） |
| 启动器 | `powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-launcher.ps1 -OutputDir bin-v14` | **20 项全 pass**（suffix 9015f7c6） |

### artifact / 源码 SHA256（主代理核实，直接记录，不另建 manifest）

| 文件 | SHA256 |
| --- | --- |
| `src/TrayApp.cs` | `EE4A41F8FD99C845A2625FD795056A0CDD0C144B2EC03222EAE6DADC12704D54` |
| `tests/QuotaTests.cs` | `FC2DEE100D49CBB9531D2DAA78B149B88DBE563FF5A57B6B737C11A13BBA976A` |
| `bin-v14/ark_left.exe` | `8484BB2BD97B3636090016BCA5188AA861AD6B6EE59ADE0F2465EDD5B2B77F36` |
| `bin-v14/ark_left-tests.exe` | `73D965F7838FDEC984523C281D7465A6E049F1227ECCBE6F42E7DF1B2A990D49` |
| `launch.ps1` | `63D815C15201A43639030D2932878B2F4DF56903A9E2A63374E7092E43178D84` |
| `start.cmd` | `3B4D0CF34F1A448AD3EA48234C608E0BF6B47046B5837FF420F6C0927A0EACDB` |

### 合成预览（非人工点击）

- 主代理读取 `bin-v14` 的 `preview-intro.png`，确认合成卡片预览**无 header / footer / 外围留白**；
  该图为 smoke `DrawToBitmap` 合成图，**非真实人工点击 / 非桌面截图**。

### 授权内真实本地升级（主代理执行，非 writer）

- 命令：`powershell -NoProfile -ExecutionPolicy Bypass -File launch.ps1 -OutputDir bin-v14 -PreviousProcessId 2268`。
- 结果：旧进程 **CLOSED**（pid 2268、exit code 0）；**LAUNCHED** pid **10600** `D:\ark_left\bin-v14\ark_left.exe`。
- `src/Program.cs` → 0.14.0.0；`launch.ps1` 默认 `bin-v14`、旧目录白名单新增 `bin-v13`；`start.cmd` → `bin-v14`；
  未覆盖正式 `bin` 与历史候选。

### 证据边界（显式未测，不冒充）

- **不伪造**真实剪贴板 / 多屏 / 高 DPI / 人工托盘点击的独立人工验收；真实额度正确性未据此证明。
- 环境：Windows / .NET Framework，`csc.exe` 64-bit v4.0.30319（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319`）。
- **T007 人工验收仍 pending，系统 Gate 未通过**；本次为 UX022 代码与自动测试验收，非系统交付。
- 主代理验收接受本次 UX022 代码与自动测试；**不宣称**真实剪贴板 / 多屏 DPI / 人工托盘通过。
