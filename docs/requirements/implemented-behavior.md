# 已实现行为（implemented-behavior）

版本：**v0.13 UX021（当前源码，T050 主代理 2026-10-04 独立验收接受）；v0.12 UX020–v0.3 UX011 未被覆盖部分继续有效；下方 v0.2 行为历史保留**
定位：**基于真实源码梳理的“当前实际怎么运行”**，与
[`product-requirements.md`](product-requirements.md)（业务需求 v0.1）和
[`interaction-improvements.md`](interaction-improvements.md)（交互 **v0.13 UX021 当前权威**；v0.12–v0.3 其余继续有效；v0.2 历史）对照。
需求与实现冲突时以需求为准，差异如实登记、本文不改业务。

> **当前状态**：主代理 2026-10-04 独立验收接受 T041–T047（T047 bin-v12 最终 **963/0**、smoke 0）与 **T050**（interaction v0.13 UX021 详情快捷键：bin-v13 实际重建 3 exe、**1030/0**、smoke 0、8 项交互 suffix 9d4a1d0b、launcher 20 项 suffix d3df298f）；正式 `bin` 仍 v0.2，主代理已在授权内本地升级到 `bin-v13`（`launch.ps1 -OutputDir bin-v13 -PreviousProcessId 9160` → pid 15260）；T007 人工验收 `pending`，系统 Gate 未通过。
> 当前源码见下方「v0.6–v0.13 当前实现（UX014–UX021）」、「v0.5 当前轮询实现」、「v0.4 当前实现」与「v0.3 当前实现」；此前未被覆盖部分继续有效；
> 第1–11节为 **v0.2 历史行为记录**，不将历史自动测试或行为回填为当前验收。T023 中止缺口已由 T025 接续修复。

## v0.6–v0.13 当前实现（UX014–UX021）

- **v0.6 UX014 现代视觉 / 布局**：详情与设置采用海军蓝 / 青绿强调、瓷白内容面与低对比边线；悬浮圆圈保持深色底与真实水位。修复 LayoutChrome 明显缺陷：死 `_identity` 标签（从未加入 `Controls`、文本恒空）不再占第二行标题空间；隐藏的 `_cancelBtn`（恒 `Visible=false`）不再预留宽度压缩 title。
- **v0.7 UX015 设置入口与设置 modal**：详情头部常驻、可聚焦设置入口；共享菜单与详情打开同一**单实例** modal，打开 / 保存 / 取消全程**零查询**；`Esc` / 取消 / `Alt+F4` 不保存；保存失败保持弹窗并 inline 可读错误；设置期间详情不因失焦隐藏，关闭回原详情并保留焦点 / 滚动；圆圈 / 托盘打开设置**不强制打开详情**；Tab 顺序合理、空态禁保存、长候选经下拉宽度 / tooltip 完整可读、100 / 150 / 200% DPI 与可见窄屏不重叠裁切。
- **v0.8 UX016 位置锁定**：圆圈右键与托盘共享菜单在「查看全部额度 / 设置」之后新增 checkable「锁定位置」，默认解锁；锁定仅阻止**鼠标拖动**，不阻止左键 / `Enter` / `Space` 详情、右键菜单与显示 / 隐藏；跨启动记忆锁定布尔，新增 identity-free `floating-preferences.json` 格式 1（仅 `Version` int / `PositionLocked` bool，严格字段 / 类型 / 大小校验）。
- **v0.9 UX017 减少动画**：共享菜单在「锁定位置」之后新增 checkable「减少动画」，默认 `false`；开启停止水波动画 Timer 并立即重绘静态波面，关闭仅在圆圈可见且已知 `0 < percent < 100` 时重启；`floating-preferences.json` 升级为**格式 2**（`Version`=2 / `PositionLocked` bool / `ReduceMotion` bool，严格三字段 + `MaxFileBytes=4096`），读取**严格格式 1**（`Version`=1 + `PositionLocked`，字段数=2）只读迁移为内存格式 2 且 `ReduceMotion=false`、加载不写盘，后续任一保存写格式 2 且同时保留另一 flag；同值不写盘；保存失败区分「动画设置未保存」/「减少动画设置未保存」，不误称锁定失败。`floating-settings.json` 格式 1 与 quota 缓存格式 1 不变。
- **v0.10 UX018 本地启动入口与旧实例平滑替换**：`start.cmd` → `launch.ps1`（当前默认 `-OutputDir bin-v13`）→ `bin-v13\ark_left.exe --show`；目标 exe 缺失时经 `build.ps1 -OutputDir` 构建到该目录。**目标目录**可为白名单 `bin` / `bin-release` / `bin-vN` 之一；**旧实例退出仅限显式枚举的 `bin`、`bin-release`、`bin-v04`–`bin-v12`**，并非任意 `bin-vN` 都会被退出；实例身份三条件（进程名 `ark_left` ∧ 主模块完整路径在白名单目录 ∧ 文件名 `ark_left.exe`）同时成立才处理，**先验证目标**再以 `WM_QUIT` **温和退出**（`Application.Run` 退出后应用自身 `Cleanup()`），失败**不 kill、不覆盖二进制、不启动新 exe**；同版本实例经既有单实例 IPC 唤醒复用原 PID。
- **v0.11 UX019 复制当前额度摘要**：详情 footer 右侧一个**可聚焦**「复制摘要」按钮；原语义为**只有用户 click** 才写剪贴板，自动刷新 / 打开 / 渲染 / 状态变化均不写（该「仅 click」触发条件已由下方 v0.13 UX021 增量覆盖为**显式 click 或详情内有效本地 `Ctrl+C`**，复制内容与安全边界不变）。`QuotaSummary` 为**纯 formatter / 独立信任边界**，只复制当前已渲染的全部额度（周期标签、剩余百分比、可用剩余额度、重置时间）与最后成功更新时间，使用**固定安全文案**，**不转写**上游 `Error` / `Message` / `UnknownNote` 原文，不含身份 / profile / 凭据 / scope 指纹。
- **v0.12 UX020 悬浮窗归位**：圆圈右键与托盘共享菜单在「减少动画」之后新增「悬浮窗归位」（共 8 项：查看全部额度 / 设置 / 锁定位置 / 减少动画 / 悬浮窗归位 / 隐藏悬浮窗 / 分隔 / 退出 ark_left）；点击把圆圈移到**当前圆圈所在屏**工作区右下默认安全 margin（圆圈**尚未定位**时取 **primary** 屏），尺寸用**该屏 DPI**缩放的 **136dp**，并显示圆圈；显式归位走强制路径，先重置进行中拖动手势并释放 Capture；详情可见时先 `HidePanel` / `RestoreAfterDetails` 清 `_autoHiddenForDetails` 再归位；锁定**不阻止**归位；设置 modal 打开时 handler 原样返回（不动 owner / 不关 modal）；**零查询、不存坐标、两 prefs 零写入**。
- **v0.13 UX021 详情快捷键（T050，主代理 2026-10-04 已独立验收接受）**：详情 `Visible && Enabled && ContainsFocus && !IsDisposed && !Disposing` 且 `!_dialogOpen && !_menuOpen` 时，`PopupForm.ProcessCmdKey` 识别精确 `Control|R`（复用刷新按钮 Click → `StartRefresh`，single-flight 不排队，显式手动查询而非后台轮询）与精确 `Control|C`（复用复制按钮 Click → `OnCopySummaryClicked`：当前快照纯 formatter / 固定安全文案 / 安全身份边界 / inline 反馈；无 snapshot 不写剪贴板但仍消费该键、不触 query）；Ctrl+Shift / Ctrl+Alt 等其余组合与 `Esc` 走 base；隐藏 / 未聚焦 / 设置 modal / 共享菜单打开时不动作、不开窗；刷新 tooltip「重新查询当前额度（Ctrl+R）」、复制 tooltip「复制当前额度摘要到剪贴板（Ctrl+C）」，按钮 label / `AccessibleName` / footer 布局不变；无全局热键 / SendKeys / 输入注入；UX019「click-only 写剪贴板」被增量覆盖为**显式 click 或有效本地 Ctrl+C**，自动打开 / 刷新 / 渲染仍零 copy。
- **保持不变的当前语义**：圆圈约 **136dp**；可见时每 **10 秒** / 全隐藏每 **5 分钟**轮询、仅设置弹窗可见不提速；全部打开路径**零查询**、single-flight、身份清留与缓存语义不变；业务基线仍 `v0.1`（R001–R004），额度缓存格式 1、floating 选择格式 1、floating 偏好格式 2。

## v0.5 当前轮询实现（T032）

- 圆圈或全部额度详情任一可见时，生产 WinForms Timer 间隔为 **10,000 ms**；两者都隐藏时为 **300,000 ms**。
- 仅设置弹窗可见不提速；切换可见性只调整现有 Timer 间隔，不立即查询；相同间隔不重复赋值，既有 countdown 不重置。
- 首次启动查询、既有 single-flight / 缓存 / 数据语义均不变。实现与自动验证证据见 [`../verification.md`](../verification.md#v05ux013t032-主代理独立验证)。

## v0.4 当前实现（T029 / T030）

- 默认打开入口为**悬浮圆圈**（非托盘面板）：启动首启 / `--show` / IPC 唤起 / 托盘左键均经生产同一入口打开圆圈；
  首启写入 marker 后启动仍静默，不改。
- 圆圈形态：**深蓝底、青色双色轻波**水波圆圈，约 **136dp** 直径，默认置于**当前屏右下 16dp**；
  圈内显示**剩余百分比 + 短产品 / 独立周期字**。
- 水波数值：**0 空、100 满、未知为中性无波**；未知 / 缺失**不显示 0**（沿用 v0.1 数值口径）。
- 窗口：真实 `ellipseRegion` 圆形区域，**方框外可穿透**；**不进入任务栏、置顶**；**失焦常驻**；
  拖动超过阈值后**不打开详情**；同会话位置**不保存**（下次回右下初始位）。
- 交互：左键圆圈（或键盘 Enter / Space）打开**同 `PopupForm`** 的全部套餐 / 周期详情，**零查询**；
  右键圆圈弹出**与托盘一致的按钮组**：查看全部额度 / 设置 / 显示或隐藏悬浮窗 / 退出。
- 设置：`ComboBox` 选**已订阅产品 / 周期**（显示 known% / unknown / error），**保存 / 取消**；
  **保存后立即按已有数据展示、零查询**，失败短提示且不应用。
- 默认选择：按接口顺序取**首个已知有效百分比**，**不取最低、不聚合**；已选目标缺失时保留选项并显示
  “暂无数据”，**不暗换**。
- 设置持久化：`floating-settings.json`（**版本 1**，位于 state dir），仅存
  `ProductKey`（`Product|Edition|Tier`）+ `PeriodLabel`，**无凭据 / 身份 ID**。
- 详情布局：**不覆盖圆圈**，优先左 / 右 / 上 / 下并按工作区 clamp；空间不足**暂 hide circle**、详情关闭恢复；
  **仅自动隐藏可恢复，显式隐藏不复活**。
- 生命周期：圆圈 `Esc` / `WM_CLOSE` **隐藏、程序驻留**；详情关闭回圆圈；**退出释放全部**。
- UX011 保持：缓存 DPAPI、启动后台一次 + 全隐藏时每 5 分钟轮询、single-flight、无过程态、身份驱逐**不变**；可见时的间隔按 v0.5 UX013 执行。

## v0.3 当前实现（T025 / T026）

- `SnapshotController` 启动后台一次，300000ms 轮询（隐藏时继续）；启动 / 手动 / 定时共用 single-flight。
  首启 / `--show` 经 `QueueShow`，第二启动 IPC 经 `OnExternalShow`，菜单 / 托盘均到 `Open()`，只展示。
- `PersistentStateStore` 加载 DPAPI CurrentUser 格式1历史，保留订阅 known、错误、数值 known、时间与 scope 指纹。
  严格损坏 / 日期检查；写临时文件再 Replace，失败保旧。仅 known auth + Same 的成功类保存。
- 明确 NotLoggedIn / 确定新 scope / Mismatch 清内存与磁盘；普通失败 / 取消保历史及原时间。
  Unknown 不替换已有确认历史；无历史受限显示且不保存。认证确定新 scope 可在结果前清空，这是安全例外。
- `BeginQuery` 不改变 CurrentView；控制器等待期不提交 UI。form 无过程态、身份横幅或首启 banner。
  无缓存“暂无数据”，标题固定，刷新 / 关闭常驻。真实刷新按钮保持可点，controller 拒绝在途重入。
- 卡片保留套餐名称、周期剩余百分比 / 条、若有剩余量、重置绝对时间；未知 / 桶错误 / 订阅 known 保持明确语义。
  移除风险摘要、紧张周期、团队说明、重复已用 / 总量、服务端重复时间和秒级倒计时。
  footer 显示基于原 fetchedAt 的“最后更新 yyyy-MM-dd HH:mm”，缓存标“上次数据”，失败短 note 不改原时间。
- `DisplayKey` 比较产品 / 周期展示语义，忽略 fetchedAt 和未展示的 server 时间；相同数据保留所有卡片及子控件引用。
  新语义先构建再在 SuspendLayout 下统一替换并释放旧控件，恢复合理滚动；同尺寸打开不改 Bounds，
  CardPanel 仅实际内部宽度变化时 Reflow，避免高度 / handle / 再打开造成重建。
- 离线测试调用真实生产打开处理器，按入口分别计 query=0；首启 / --show 共用生产队列，第二启动使用同一 IPC 回调。
  隔离进程脚本另验证真实第二实例信号与隐藏 / 静默，未声称真实 Explorer 点击计数或人工视觉验收。
- 主代理独立验证命令结果见 [`T027 主代理独立验证`](../verification.md#v03ux011t027-主代理独立验证)（「v0.3（UX011）T027 主代理独立验证」）；版本清单：`bin-release/t027-sha256.csv`（主代理生成）。

源码依据（相对本目录）：

- [`../../src/Program.cs`](../../src/Program.cs)：入口、`--smoke-test`、`--show`、单实例。
- [`../../src/TrayApp.cs`](../../src/TrayApp.cs)：运行时（托盘 / 菜单 / 静默启动 / 首启标记）、
  `PopupForm`、`HideController`、`PanelPositioner`、`QuotaBar`、`IconArt`、`SyntheticSample`。
- [`../../src/ViewState.cs`](../../src/ViewState.cs)：`PanelModel` 状态机、百分比 / 相对时间 /
  风险周期格式化。
- [`../../src/Identity.cs`](../../src/Identity.cs)：真实 auth/usage 身份解析、内存 scope、友好提示。
- [`../../src/Ipc.cs`](../../src/Ipc.cs)：命名事件 IPC（可隔离测试）。
- [`../../src/QuotaCli.cs`](../../src/QuotaCli.cs)：CLI 发现 / 子进程 / 认证闸门 / 进度 / 判定。
- [`../../src/QuotaParser.cs`](../../src/QuotaParser.cs)、[`../../src/Models.cs`](../../src/Models.cs)：
  JSON 解析与展示名。

> 需要“人在图形界面点击 / 观察”的一律标**人工未测**，不写成已通过。

## 1. 产品形态与运行环境

- Windows 托盘常驻图标 + 轻量浮窗；C# 5 + WinForms，`build.ps1` 调 .NET Framework 4.8
  `csc.exe` 编译（**无 SDK / NuGet**）。
- 应用进程自身不联网；数据来自本机 ArkCLI 子进程。

## 2. 启动、单实例与首启（UX002 / UX010）

- 入口 `Program.Main`：`--smoke-test` 走离线冒烟；否则 `TrayApp.Run`。
- 单实例：命名互斥量 `Local\ark_left_single_instance`。**第二实例不再弹 `MessageBox`**，
  经命名事件 `Local\ark_left_show_event`（`Ipc.Signal`，有限重试）请首实例显示后**静默退出**；
  信号失败返回非零（不伪成功）。正常默认实例名固定；测试可用
  `ARK_LEFT_INSTANCE_SUFFIX` 派生隔离名。
- 用 `ApplicationContext` 静默驻留（不 `Application.Run(form)` 强制显示）；浮窗先建隐藏句柄
  供 `BeginInvoke` / IPC。
- 首启（无标记）显示简短引导并写 `%LOCALAPPDATA%\ark_left\first-run.done`（仅“版本 + 日期”），
  之后启动**静默、不查询**直到打开。`--show` 可强制显示。`ARK_LEFT_STATE_DIR` 隔离状态目录。
- `--smoke-test` **不写用户标记、不碰真实单例 IPC**。

## 3. 托盘交互与隐藏机制（UX001 / UX004 / UX009）

- 左键：切换浮窗（打开即刷新）。`MouseDown` **一次性捕获**按下时的**真实**可见性，
  紧跟的 `Click` 通过 `ConsumeToggle` 消费并重置；无 MouseDown 的下一次 Click 使用
  **当下** `Visible`，避免用上次旧捕获导致“先隐藏后重开”。
- 右键菜单：**“查看额度” + “退出 ark_left”**。
- **单一隐藏机制** `HideController`：失焦事件只**排队**一个 pending，由**一个** WinForms
  `Timer`（约 220ms）统一裁决；真实状态变化（show/hide/重新激活）取消 pending；
  `WM_ACTIVATE(WA_INACTIVE)` 与 `OnDeactivate` 共享同一 pending；托盘 MouseDown 打开激活
  抑制窗口、**取消 pending 并停 hideTimer**；MouseDown 到 Click 之间（长按）即使失焦也
  不排队隐藏，click 后抑制结束仍会检查并隐藏（不会永久卡住）。
  **真实 Explorer 下的点击 / 失焦顺序人工未测**。
- 浮窗**不可拖动**；`PanelPositioner` 固定逻辑宽约 404–440、高约 560（工作区收敛），
  loading 与结果**同尺寸**；`Esc` / “×” / 点击外部隐藏；每次打开按光标所在屏定位。

## 4. 刷新、阶段与慢响应（UX003 / UX007）

- `QueryDetailedAsync` 通过 `IProgress<QueryProgress>` 报告阶段
  `Auth → Usage → Done`；**认证成功**（含 `active_profile` 缺失）以 `AuthConfirmed=true` 回调，
  携带可空身份。**8s** 未完成报一次 `Slow`。
- UI 只允许一次查询；取消从开始即可用；取消完成前不发新请求。控制器用单调 query 代数 +
  “完成后 `_querying=false`”拒绝迟到进度，**迟到的慢提示不会把完成结果改成取消态**；
  主流程按 `QueryOutcome` 判终态，不依赖进度队列先执行。
- 超时仍是**每命令** 30s（`auth`+`usage` 顺序，最坏可能 > 60s）。
- 计时器（15s）**仅改写文本 label**（倒计时 / 新鲜度），不重建卡片、不查询。

## 5. 面板状态机与缓存复用（UX003 / UX008）

- `PanelModel`（纯逻辑，可单测）状态：
  `Loading / ConfirmingIdentity / ShowingCurrent / RefreshingSameScope / StaleError /
  CancelledStale / IdentityChanged / Error`。
- **复用规则（严格）**：`BeginQuery` 一律隐藏旧数据（内部仅保留候选）；
  只有当**本次** auth 确认 `logged_in=true` 且**本次 pending scope 与已确认 scope 相同**
  （`HasReusableData` 校验 `_pendingScopeKnown && _pendingScope.Matches(ConfirmedScope)`）时，
  才在刷新期间显示旧数据，或在取消 / usage 失败时回退旧数据。
  auth 阶段取消 / 失败 / 身份 unknown 一律清空，并清除旧身份提示。
- 终态由共享处理器 `TrayApp.ApplyFinalOutcome` 按最终 `QueryOutcome.AuthConfirmed` 决定，
  **不依赖异步 `Progress` 队列顺序**；auth 阶段取消 / 异常清空，usage 取消仅在本次同 scope 时复用。
- usage 侧 `ScopeVerdict`：
  - `Same`（账户 / profile / 区域 / 项目 / 主子身份全部存在且一致）→ 缓存并显示；
  - `Unknown`（viewer 缺关键字段）→ **只显示本次结果、不缓存**，标“身份未完全确认”；
  - `Mismatch`（账户 / profile / 区域 / 项目 / 主子身份冲突，含子用户 auth 对 `is_root=true`）→
    清空并提示“身份变化，请重试”。
- `NoSubscription` / 空 `items`：`Data` 非空但无卡片时用 `snapshot.Message` 明确显示“未订阅”，
  不留空白面板。
- 未登录 / 缺 CLI / 失败：`AllowRetry` 为真；未登录另给“复制登录命令”，缺 CLI / 失败给
  “打开设置指南”。**不自动登录 / 安装**。

## 6. 身份、scope 与隐私（UX008）

- 真实 `auth status`：`logged_in` + `active_profile{name,type,owner_trn,region,project}`。
  真实 `usage viewer`：`account_id/user_id/profile(string)/tenant/region/project_name/is_root`。
- 内存 scope：`SHA256(owner_trn+name+region+project)`，解析 `owner_trn` 为
  `trn:iam::<account>:root | :user/<id>`（region 槽要求为空、`user/<id>` 的 id 非空）；
  不支持格式 => unknown。**仅内存、不展示、不落盘**；必要字段缺失 => unknown，不复用。
- 展示用 `IdentityDisplay`：`type` 映射（agent-plan / coding-plan / team / platform）+
  区域 + 长度受限 name；**不显示 `owner_trn` / `account_id` / 用户名**。
- 原始 JSON 短暂进内存（`JavaScriptSerializer`），只提取业务字段，不持久化、不展示其余字段。

## 7. 数值与时间规则

- 剩余百分比：`percent`（已用）有效 → `clamp(100-percent,0,100)`；否则 `used/total` 备用公式；
  `total=0` => 未知。数值 `0` 与缺失严格区分。
- 展示：`0` → “已用尽”、`0<v<1` → “<1%”、否则最多一位小数；`≤20%` 标“余量较低”
  （文字 + 颜色，色盲友好；阈值为工程交互选择）。
- 最紧张周期：**每个产品内**取最低有效剩余百分比（忽略未知 / 错误，**不求和**），并显示具体
  周期中文名；产品名不重复“团队版”。
- 时间：`reset_at` 绝对时间 + 相对倒计时（已过重置显示“已到重置时间，请刷新”，不推断恢复）；
  本地新鲜度（`fetched_at`）与服务端更新时间（`updated_at`）分开显示。

## 8. 首次运行引导

- 首启引导为**一条持久可见、可“知道了”关闭**的 banner（本会话），不再只在 loading 瞬间出现；
  不因每次请求被覆盖。

## 9. 进程、超时与清理

- `QuotaCli`：仅原生 `.exe`（`ARK_LEFT_CLI` 绝对路径 → PATH `arkcli.exe` → npm 原生 exe）；
  `UseShellExecute=false`、`CreateNoWindow=true`、UTF8、`ReadToEndAsync`；每命令 30s 超时后 kill；
  `Dispose` 后拒绝新进程；非零退出不伪装成功。
- `ExitApp`：Cancel + `KillActive` + 隐藏图标 + 关闭浮窗 + 退出；`Dispose` 释放 NotifyIcon /
  菜单 / 图标 / CLI / 窗体；`ClearContent` 逐个 `Dispose` 子控件，字体缓存统一释放。

## 10. 验证分级（不互相冒充）

- **明确已实现**：`src/` 中存在对应代码。
- **自动已验证**：构建（无告警）/ 单元测试（**v0.4 当前源码 518 项**；v0.3 为 368 项、
  v0.2 最终收尾轮为 245 项、历史实现轮为 213 项，均保留）/ `--smoke-test` / 隔离交互集成脚本
  `tests/verify-interaction.ps1` 产生的证据（见 [`../verification.md`](../verification.md)）。
  真实脱敏查询由主代理确认 `scope_verdict=Same` / `status=Ok`（T027 / T031 本轮未执行真实查询）。
- **人工未测**：真实托盘 / 圆圈点击 / 失焦 / 右键菜单、多屏 / 高 DPI 视觉、键盘 / 读屏、
  取消 / 重试手感、倒计时走动、真实身份 A→B 切换、记事本 / 剪贴板真实调用。
- **未实现（就历史 v0.2 / 已交付代码而言）**：开机启动、定时 / 自动刷新、告警、独立登录 UI、
  资源包 / 账单、跨平台。**v0.3 目标**（跨重启持久化、每 5 分钟轮询含隐藏、打开零查询、
  等待期无过程态、界面简化）与 **v0.4 目标**（悬浮圆圈、点击查看全部、设置选择）均已由
  T-025 / T-026 / T-029 / T-030 实施、T-027 / T-031 独立自动验证；人工验收仍 T-007。

## 11. 已知限制（如实）

1. 托盘视觉与交互、多屏 / 高 DPI 定位**未人工验证**（T-007 `pending`）。
2. 托盘图标可能被 Windows 收入折叠区（系统行为，不承诺强制可见）。
3. 超时是**每命令 30s**，最坏总耗时可能超过 60s。
4. `used` / `total` 单位未证实，界面写作“额度”，不声称是 Token。
5. 真实未登录 / CLI 缺失环境未构造，由测试模式覆盖。
6. `--smoke-test` 只验证布局边界、子控件边界、固定尺寸与隐藏语义，**不是视觉 / 交互验收**；
   `bin/preview*.png` 为合成窗口自绘合成图，非桌面截图，且不含真实账号数据。
7. 身份 scope 指纹仅内存；真实 A→B 身份切换未构造，由合成用例覆盖逻辑。
8. Coding Plan / Team 仅逻辑与合成测试覆盖，**无真实订阅验证证据**。
9. `--smoke-test` 不写用户 marker、不碰真实单例 IPC；真实单实例信号由隔离实例测试覆盖。
