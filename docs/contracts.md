# 接口契约（contracts）

版本：**v0.1**（`usage plan` 解析契约）；**v0.3 新增「持久快照契约」**（见下方专节，格式冻结）；**v0.4 新增「悬浮窗选择与窗口契约」**（见下方专节）；**v0.14 新增「卡片弹窗（card-only）契约」**（实施中，T052，待主代理验收，见下方专节）；**v0.13 新增「详情快捷键契约」**（已实施，主代理 2026-10-04 独立验收接受 T050）；**v0.11 新增「复制摘要契约」**；**v0.12 新增「悬浮窗归位契约」**（见下方专节）；**v0.10 新增「本地启动入口与旧实例平滑替换契约」**（见下方专节）
状态：**v0.1 已由真实脱敏查询确认**（2026-10-03）。字段名、类型与命令已在本项目内实测，
`viewer` 身份字段不落盘。以下数据结构与数值约定与实现一致。

> **v0.3 补充**：交互层 v0.3（UX011）引入**跨重启本地快照**。本文件新增
> §“持久快照契约（v0.3 冻结）”，定义缓存文件格式、语义完整性与安全约束。
> 该契约由主代理授权技术决定（**非用户指定值**），供 T-025 / T-026 实现与 T-027 验证；
> `usage plan` 的**上游解析契约本身不变**（仍 v0.1）。

> 本文件的命令与字段已通过 `ark_left-check.exe` 对真实本机 ArkCLI 查询验证；
> 文中不包含任何真实账号 ID / 凭据 / viewer 样例。

## 卡片弹窗契约（v0.14 UX022，T052 实施中，未验收）

> 用户 2026-10-04 明确："左键弹窗应只显示里面那 card 就好"。本契约登记实施前基线；
> **未经主代理验收前不伪称已接受**。

- **呈现**：详情弹窗只呈现卡片（card-only）：无标题 / 刷新 / 设置 / 关闭 header，无更新
  时间 / 复制按钮 footer，无矩形外框与外围 padding，无固定 420dp 最小高度；单 card 时
  窗口贴合该 card 实测尺寸并以同 card DPI 圆角的 Form Region 呈现；多 card 竖向 stack
  （10dp 间距仅 cards 之间、无尾部空白），超过 min(560dp, 工作区-16dp) 才出现纵向滚动，
  滚动容器不添加外围框。
- **尺寸与定位**：逻辑宽度 406dp（clamp 工作区，100/150/200% DPI 与窄工作区无横向
  overflow）；高度按真实 layout 两 pass 测量（全宽 measure → 溢出才扣真实滚动条宽重排），
  不做无限 resize 循环、不以周期 count 猜测滚动；初次打开先 final size 再 PrepareDetails；
  可见数据小 ↔ 大变化按当前圆圈重新定位（不跳鼠标屏）；相同语义 / 仅 fetchedAt 变化不改
  尺寸、不重建 card / children、不 reset focus / scroll。
- **操作面**：CardPanel 显式 Selectable + TabStop（真正可聚焦），ShowPanel 激活后焦点落在
  card / 首个可聚焦内容，已持有焦点不强制 reset；详情本地右键菜单仅「刷新 / 复制摘要 /
  设置 / 关闭」四个 action，分别复用既有刷新（singleflight）/ 复制（快照 formatter、固定
  安全文案）/ 设置（单实例 modal、零 query）/ 关闭（hide）handlers；打开菜单零 query 且
  抑制失焦隐藏，复制仅快照时启用；圆圈 / 托盘共享 8 菜单项不变。
- **反馈与状态**：复制成功 / 失败以 card 上可见短 Tooltip（约 2 秒，非阻塞）反馈，不写
  raw 上游错误、不覆盖错误状态；footer 更新时间移至 card tooltip（含服务端数据更新后缀）；
  stale / 缓存 / 身份未确认状态在首个 card 内紧凑可见（仅必要时显示、原位更新、不重建）。
- **不变量**：业务 v0.1（R001–R004）、`usage plan` 契约、10s / 5min 轮询、额度缓存格式 1、
  floating 选择格式 1 / 偏好格式 2、身份 scope 规则、打开零 query、Esc / 失焦 / WM_CLOSE
  hide 语义、Ctrl+R / Ctrl+C 精确组合（UX021）均不变；不加筛选 / 只显示选中套餐等业务变化；
  无全局热键 / SendKeys / 输入注入。

## 技术栈（已定稿）

- 方案：Windows **C# 5 + WinForms + `NotifyIcon`（托盘图标）+ 轻量浮窗**。
- 无 .NET SDK、无 NuGet 依赖；由 `build.ps1` 调用 .NET Framework 4.8 的
  `csc.exe`（`Framework64\v4.0.30319`，可回退 `Framework32`）编译。
- 已在本机验证：无 dotnet SDK，存在上述 `csc.exe`。

## 数据来源（已实测）

- **复用本机已有的 ArkCLI 登录身份**，不在本插件内存储 / 写入凭据。
- 读取流程已实测，带认证闸门：
  1. `arkcli auth status --format json`；仅当进程退出码为 0 且顶层 `logged_in=true`
     才继续；`logged_in=false` → 未登录；缺 `logged_in` / 非布尔 → 格式错误；
     非零退出 / 启动失败 → 失败；超时 / 取消 → 对应状态。
  2. 通过后 `arkcli usage plan --format json`；非零退出不得当作成功。
- **仅支持原生 `arkcli.exe`**（不通过 PowerShell 托管 `.ps1` shim，避免 shim 孙进程
  持有 stdout/stderr 管道导致超时无法收敛）。CLI 定位：
  `ARK_LEFT_CLI`（必须是存在的绝对 `.exe` 路径）→ PATH 中 `arkcli.exe` →
  npm shim 目录下 `node_modules/@volcengine/ark-cli/bin/arkcli-windows-amd64.exe`。
  两者都缺失时提示安装 CLI 或设置 `ARK_LEFT_CLI`，不静默失败。
- 子进程 `UseShellExecute=false`、`CreateNoWindow=true`、UTF8；stdout/stderr 用
  `ReadToEndAsync` 异步读取，进程退出等待有界；**每命令** 30s 超时（`auth` + `usage`
  顺序执行，故总耗时可能更长），超时 / 取消后 kill。`Dispose` 后不再启动任何新进程。

## 身份与 scope（真实 schema，已由主代理上轮验证）

`arkcli auth status --format json`（真实）：

```
{ "logged_in": true,
  "active_profile": { "name", "type", "owner_trn", "region", "project" } }
```

`usage plan --format json` 的 `viewer`（真实）：

```
{ "account_id", "user_id", "profile", "tenant", "region", "project_name", "is_root" }
```

- **注意**：`viewer.profile` 是**字符串**，不是嵌套 `viewer.profile.name`。
  不要假设 auth 的 `active_profile` 嵌在 `viewer` 下。
- 内存 scope 指纹（**仅内存，不落盘、不展示**）：`SHA256(owner_trn + "\n" + name
  + "\n" + region + "\n" + project)`，取自 auth `active_profile`。任一必要字段缺失
  ⇒ scope **unknown** ⇒ 不复用旧缓存。
- usage 侧校验：`viewer.profile` 与 auth `profile name` 不一致、或
  `viewer.account_id` 与 `owner_trn` 的账户部分不一致 ⇒ 判定**身份变化**，
  清空旧缓存并提示重试。**缺失字段一律不补 0、不编造 ID**，只报 unknown。
- 展示只用友好 `active_profile.type` 映射 + 区域 + 长度受限的自定义 profile name；
  **不展示 `owner_trn` / `account_id` / 用户名**。
- 测试 fixture 一律为合成匿名数据，**不写真实 viewer / 账号**。
- **来源区分**：本节 schema 来自主代理的**真实脱敏查询**；`tests/` 中的相应 JSON 为
  **合成 fixture**，仅用于逻辑验证。

## 数据结构（已实测）

`usage plan` 的 JSON 顶层为 `viewer` + `items`（已由真实查询确认）。

- `viewer`：当前身份摘要（如认证方式、用户 / 账号标识、profile、region 等）。
  **本插件不得缓存或输出其中的敏感身份标识。**
- `items[]`：
  - `product`：`agent-plan` / `coding-plan` / `agent-plan-team` / `coding-plan-team`；
    （实测 `agent-plan`；其余按产品命名约定解析，展示层均支持）
  - `edition`：如 `personal`（实测存在）；`tier`：如 `medium`（实测存在）；
  - `subscribed`：布尔，是否有效订阅；
  - `periods[]`：周期桶数组；
  - `error`：该桶失败原因（桶级错误隔离，一桶失败不挡其它桶）。
- `periods[]`：
  - `label`：周期标识（如 `5h` / `weekly` / `monthly` / `session`）；
  - `used` / `total`：**Coding Plan 常缺省**（`omitempty`），Agent Plan 可能是绝对值；
  - `percent`：**已用百分比**（0–100）；**不是剩余百分比**；
  - `reset_at`：重置时间，RFC3339 北京时间（UTC+08:00）；
  - `updated_at`：**服务端数据更新时间**（仅 CodingPlan，可能缺失）；实测位于
    **item 级**而非 period 级，实现按 item 解析并展示。
- 桶级 / 条目级 `error`：**固定安全文案，不展示上游原始错误文本**
  （避免错误信息中夹带 token / 凭据）。桶级错误不当作"未订阅"。
- 条目错误但仍有可用 `periods` 时，展示可用部分并在卡片内标注错误
  （`PeriodErrorPresent` / 条目 `error` 均使整体状态为部分失败）。

数值约定（关键）：

- `used` / `total` 的**单位尚未验证**：不得未经确认就一律称为 "Token"，界面写作"额度"。
- 数值 `0` 与"字段缺失"必须区分，缺失 / 非法不得补 0，标为未知。
- 数值解析接受 `int` / `long` / `double` / `decimal` / `float` 等；超范围 epoch 不崩溃。
- **剩余百分比主公式**：`percent`（已用百分比）有效时，剩余百分比 = `clamp(100 - percent, 0, 100)`。
- **备用公式**：仅当 `used >= 0` 且 `total > 0` 且两者均为有限数（finite）时，
  剩余百分比 = `clamp((total - used) / total * 100, 0, 100)`。
- **`total = 0` 时百分比为未知**，不计算、不显示 0。
- 计算越界（被 clamp 截断）时需提示原始数据异常。
- 剩余绝对量 = `max(total - used, 0)`，**仅当 `used` 与 `total` 有效且 `total - used` 有意义**。
- 桶级 `error` 优先按"桶错误"处理，**不得当作未订阅**。
- **三个时间必须区分**：
  - `reset_at`：重置时间（服务端返回）；
  - `updated_at`：服务端数据更新时间（可能缺失）；
  - `fetched_at`：本地本次查询时间。

## 接口清单（已实测）

| 接口名称 | 版本 | 提供方 | 消费方 | 状态 |
| --- | --- | --- | --- | --- |
| ArkCLI 认证状态查询（`arkcli auth status --format json`） | 实测 | 本机 ArkCLI | 本插件适配器 | 已用真实查询验证 |
| ArkCLI 套餐额度查询（`arkcli usage plan --format json`） | 实测 | 本机 ArkCLI | 本插件适配器 | 已用真实查询验证 |

## 数据模型（已实测）

| 实体 | 字段 | 类型 | 单位/精度 | 可空 | 说明 |
| --- | --- | --- | --- | --- | --- |
| Period | label | string | — | 否 | 周期标识 |
| Period | used | number | **未证实**（不得默认 Token，展示为"额度"） | 是 | 可缺省 |
| Period | total | number | **未证实** | 是 | 可缺省 |
| Period | percent | number | %（0–100，**已用**） | 是 | 已用百分比，非剩余 |
| Period | reset_at | string | RFC3339（UTC+08:00） | 是 | 重置时间（服务端） |
| Period | updated_at | number(string) | epoch ms 或字符串 | 是 | 服务端数据更新时间（可能缺失） |
| Item | product | string | — | 否 | 四类之一 |
| Item | edition / tier | string | — | 是 | 实测存在 |
| Item | subscribed | bool | — | 否 | 是否订阅 |
| Item | error | string | — | 是 | 桶级错误 |
| 采集 | fetched_at | string | 本地时间 | 否 | 本地本次查询时间 |
| 派生 | remaining_percent | number | %（0–100） | 是 | 剩余百分比，由 percent 或 used/total 计算 |

## 工程约束（已实现）

- 异步查询（`await`），避免阻塞界面。
- **每命令**超时 30s，超时 kill 子进程（`auth` + `usage` 顺序，总耗时可能更长）。
- 防止重复请求（刷新期间防重入）。
- 取消 / 退出时终止活动子进程并释放资源。
- 不持久化敏感 `viewer` 身份标识；测试使用合成匿名 fixtures。
- 无跨账号的持久化缓存（无磁盘缓存）；内存清空边界见
  `requirements/implemented-behavior.md` 第 5、11 节（并非每次打开都无条件清空）。

> **v0.3 变更提示**：上一条“无磁盘缓存”已被交互层 v0.3 覆盖为**跨重启本地快照**
> （见下节）。其余工程约束不变。

## 持久快照契约（v0.3 冻结）

状态：**已冻结（格式版本 1，未实现）**。定义跨重启本地快照文件的字段、语义与安全约束。
实现归属 T-025；验证归属 T-027。**格式版本变化必须先改本文件并提升版本。**

### 存储与安全

- **位置**：现有 state dir（`Marker.StateDir()`；`ARK_LEFT_STATE_DIR` 可隔离，
  默认 `%LOCALAPPDATA%\ark_left`）；文件名 `quota-cache.dat`。
- **加密**：Windows 用户 DPAPI（`CryptProtectData` CurrentUser）加密整份 blob；
  不解开 / 跨用户无法读取。
- **原子写入**：写临时文件 → 原子替换（`File.Replace`，回退 `Copy+Delete`）；
  **任意写入失败保留旧有效文件，不降级覆盖**。
- **隐私**：**不保存凭据、不保存原始身份 ID**（无 `owner_trn` / `account_id` / 用户名）；
  只保存**不可逆 scope 指纹**（SHA256）用于归属校验。
- **拒绝**：格式版本不符 / JSON 结构损坏 / DPAPI 解不开 → 返回 `null`，
  按“无缓存”处理；**不得**以损坏数据渲染或回退为假值。

### 语义完整性（缓存必须保存）

快照必须保存**展示所需的完整语义**，否则按损坏拒绝或降级为“未知”，不得补 0：

- **订阅 known 状态**：产品是否已订阅（`Subscribed` 与 `SubscribedKnown` 区分）。
- **产品级 / 周期级错误**：条目错误与桶错误分别保存（错误 ≠ 未订阅）。
- **数值 known 标志**：`PercentKnown` / `UsedKnown` / `TotalKnown` / `AmountKnown`
  及对应数值；缺失 / 非法保持未知。
- **时间**：`fetched_at`（本地采集时间）、`updated_at`（服务端更新时间，可缺）；
  `reset_at`（重置时间，可缺）。

### 只持久化范围（scope 门控）

- 仅当 scope 判定 **Same** 时，持久化**有效成功 / 部分成功 / NoSubscription** 三类结果。
- **未知 scope 不写缓存**；失败 / 取消 / 超时 / 未登录 / 缺 CLI **不写缓存**。

### 身份与清 / 留规则（展示层契约）

- **加载快照作为“上次历史”展示**，直到后台确认。
- **普通认证 / 网络失败**：保持历史及原时间，不伪认最新。
- **立即清旧内存 / 磁盘**（可在身份确认阶段清，**唯一安全例外，不显示过程态**）：
  明确 `NotLoggedIn`；认证确定新 scope（与已确认不同）；usage `Mismatch`。
- **成功新身份结果再替换**展示与缓存。
- **Unknown 不证明不同身份**：可保留历史，但**不把新的 Unknown 结果覆盖 / 持久化
  已有确认数据**；无历史时 Unknown 显示受限当前结果且不缓存。

### 时间与非法日期

- **不从非法日期推“现在”**：`fetched_at` / `updated_at` / `reset_at` 缺失或非法时，
  **不得**回退为 `DateTime.Now` 假装新鲜；应标为未知或保留原时间语义。

### 当前实现状态（如实）

- 本节更新当前实现；上方冻结时“未实现”为历史状态，格式1现已接入。
  原子替换失败保旧、不采用上方旧 Copy+Delete 回退，依据用户 T025 brief 明确授权（见 tasks.md T025），格式版本不变。
- T025 已修复 T023 草稿缺口，完整保存订阅 known、产品 / 周期错误、数值 known 与时间；
  严格结构 / 日期 / DPAPI 校验，非法日期不推现在，Replace 失败保旧，启动 Load、成功 Save、身份边界 Clear 已接入。
- `PanelModel.CurrentView` / `BeginQuery` / `CommitOutcome` / `ShowSnapshot(snap,fingerprint)` 供 form 消费；
  `PanelView.FromCache` / `NewData` / `Persist` 与 SnapshotController 门控不变。T025 数据控制部分主代理复核通过。
- T026 form 比较所有产品 / 周期显示语义，忽略 fetchedAt，不同数据一次替换、相同数据仅更新 metadata / time / note；
  T026 实现与 T027 主代理独立自动验证通过（**368/368**，历史 245 项保留），证据见 [`T027 主代理独立验证`](verification.md#v03ux011t027-主代理独立验证)（「v0.3（UX011）T027 主代理独立验证」）；T007 人工验收 `pending`，系统 Gate 未通过。
  `build.ps1` / `tests/verify-interaction.ps1` 已支持绝对 `-OutputDir`（路径 correctness 修复，格式版本不变）。
  T023 中止缺口历史见 `tasks.md`，不重写历史证据。

## 悬浮窗选择与窗口契约（v0.4）

状态：**已冻结（v0.4，T-030 done，主代理 T-031 独立自动验证通过；Q-006 CLOSED）**。
定义悬浮圆圈的显示目标选择与窗口交互；**不新增业务数据范围**，业务基线仍 `v0.1`（R001–R004），
额度缓存格式仍**格式 1**（见上节），悬浮设置格式**格式 1**（本专节）。
T030 实施与 T031 主代理独立自动验证通过（`bin-v04` build 无告警 / **518/518** / smoke exit 0 /
交互 8 项 ok），证据见 [`T031 主代理独立验证`](verification.md#v04ux012t031-主代理独立验证)；
T007 人工验收 `pending`，系统 Gate 未通过。本契约由主代理授权工程决定（**非用户逐项指定值**），
依据 [`requirements/interaction-improvements.md`](requirements/interaction-improvements.md)
UX012（Q-006 已 CLOSED）。**格式版本变化必须先改本文件并提升版本。**

### 选择存储（独立于额度缓存）

- **位置**：现有 state dir（`Marker.StateDir()`；`ARK_LEFT_STATE_DIR` 可隔离，
  默认 `%LOCALAPPDATA%\ark_left`）；文件名 `floating-settings.json`。
- **格式（版本 1）**：仅 `{ "Version": 1, "ProductKey": "<Product|Edition|Tier>",
  "PeriodLabel": "<label>" }`。**只存 product 稳定 key + period label**；
  **无身份、凭据、额度数值**。此设置**不是额度缓存**，不写入 / 不影响 `quota-cache.dat`。
- **product 稳定 key**：`Product + "|" + Edition + "|" + Tier`（缺失段写空），
  使不同版本 / 档位套餐不冲突；与身份 scope 无关。
- **严格校验**：版本必须为 1；`ProductKey` / `PeriodLabel` 必须为非空字符串且长度
  分别受限（≤128 / ≤64）；**对象字段数必须恰为 3（Version / ProductKey / PeriodLabel）**，
  **多余 / 未知字段**、缺失字段、类型不符、结构损坏、版本不符 → 返回 `null`，
  **按“未配置”处理**，不妨碍程序运行。**损坏不得回退为假选择**。
- **原子写入**：写临时文件 → 原子替换（`File.Replace`，无旧文件时 `Move`）；
  **任意写入失败保留旧有效文件**；用户保存写盘失败必须**短提示，不假装记忆成功**。
- **仅用户保存才存档**：默认选择为运行时计算，**不持久化**；只有设置弹窗“保存”
  成功才写入本文件。

### 显示目标选择语义（Q-006 落实）

- **候选集**：按 `usage plan` 返回的**产品 / 周期原顺序**枚举；每项标注
  **已知百分比 / 未知 / 错误 / 未订阅 / 订阅未知 / 暂无数据**。
  **非订阅 / 产品失败 / 订阅未知 / 周期错误**项**不得**伪造为可信数值项。
- **默认选择（仅尚未配置时）**：取**第一个可展示的已知有效百分比周期**
  （`PercentKnown` 且 `[0,100]` 且产品为已知有效订阅）。**不取全局最低、不求和 / 平均**。
  若全部未知，取**首个可展示条目**并显示**未知**。
- **已选目标缺失**：显示**“暂无数据”并保留选择**，供用户在设置中重选；**不暗换**。

### 圆圈数值口径（不假 0）

- 仅当所选周期 `PercentKnown`、数值**有限**且在 `[0,100]`、且产品为**已知有效订阅**
  时才显示水波百分比；**已知 0 => “0%”、已知 100 => “100%”**。
- 未知 / 产品失败 / 订阅未知 / 未订阅 / 周期错误 => **“剩余未知”**（中性、无水波）；
  缺失（已选目标不存在）= **“暂无数据”**；**不得**渲染为 0。

### 窗口交互契约

- **圆圈窗口**：WinForms 圆形窗体（自绘），**置顶、无边框、不进任务栏**；
  **椭圆 Region** 使椭圆外的点击真正穿透（非方框）。初始定位在当前屏**右下工作区**
  边距约 16dp、直径约 136dp，按 DPI 缩放；拖动约束在**所在屏工作区内**（支持负坐标多屏）。
- **详情复用**：左键圆圈（或 Enter / Space）打开**既有 `PopupForm`**，**零查询**；
  不改 UX011 固定尺寸。**布局 fallback 规则**：优先把详情放在圆圈**左侧**
  （不覆盖圆圈）；左侧空间不足时依次尝试**右侧 / 上方 / 下方**；**均无法容纳时
  暂时隐藏圆圈**，详情关闭后再恢复。任何情况下详情都**不得覆盖圆圈**。
  拖动超过阈值**不触发**详情。
- **右键按钮组**：托盘菜单与圆圈 `ContextMenuStrip` **同一组**，含
  **查看全部额度 / 设置 / 显示或隐藏悬浮窗 / 退出**；动作均**零查询**。
- **设置弹窗**：`ComboBox` 列出全部候选及状态，含**保存 / 取消**；保存成功后
  **立即展示已有数据、不查询**；写盘失败则短提示且不应用。
- **生命周期**：圆圈**失焦不隐藏**；`Esc` / `WM_CLOSE` **隐藏圆圈、不退出**；
  拖动位置**仅本会话有效、不保存**（重开不跳回默认）；**退出项统一释放**窗口 /
  设置 / 菜单 / 计时器后退出。系统关闭只隐藏。**恢复规则**：仅当圆圈是
  “因详情布局而无空间”被暂隐藏时才在详情关闭时恢复；用户**显式**隐藏的圆圈
  **不得**被详情关闭错误恢复。

## UX014 视觉呈现契约（v0.6）

状态：**T041 视觉 / 布局收尾（候选，主代理审阅待执行）；仅冻结视觉与布局表现，不变更数据、窗口行为或交互时序**。业务基线为 `v0.1`，额度缓存与悬浮设置格式仍为格式 1。
依据 [`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX014。

- 详情窗口外部固定尺寸及多屏 / DPI 定位语义不变；内容继续完整滚动展示，页脚刷新和状态信息不移除。
- 详情采用瓷白卡片、轻边框与海军蓝 / 青绿强调；百分比作为主要信息清晰对齐，周期及数量 / 重置信息仍可见。
- 悬浮圆圈继续保持约 136dp 椭圆 Region、点击穿透、拖动与键盘行为；未知不得显示成 0，水位仍以剩余百分比表示。
- 原生键盘可访问控件及其 `AccessibleName`、Tab 顺序、按钮语义和菜单项 / 处理器保持；同语义刷新不得重建控件或丢失焦点 / 滚动位置。
- 设置弹窗继续使用可键盘选择的原生 `ComboBoxStyle.DropDownList`，完整显示候选状态；保存失败不关闭窗口，取消 / Esc / Alt+F4 语义不变。
- 可共用小型样式 helper；不得新增依赖、字体资产或 OS 特定框架依赖。高 DPI 与长文案不得裁切关键百分比 / 周期信息。

## UX015 设置入口与设置弹窗交互契约（v0.7）

状态：**已实施并经主代理 2026-10-04 独立验收（T042 done，候选 bin-v07）**。仅冻结交互语义，不改数据、格式或轮询契约。依据 [`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX015。

- 详情头部保留**常驻、可聚焦**的设置入口；共享菜单与详情打开同一单实例设置 modal，打开 / 保存 / 取消全程**零查询**。
- `Esc` / 取消 / `Alt+F4` **不保存**；保存失败保持弹窗打开并显示 inline 可读错误。
- 设置弹窗期间详情**不因失焦被隐藏**；关闭设置回到原详情并保留焦点 / 滚动；从圆圈 / 托盘打开设置**不强制打开详情**。
- Tab 顺序合理；空态禁用保存；长候选文案经下拉宽度 / tooltip 完整可读。
- 100 / 150 / 200% DPI 与可见窄屏下控件不重叠、不裁切。

## UX016 位置锁定与悬浮偏好契约（v0.8）

状态：**已实施，T043 done，主代理 2026-10-04 审读通过（build bin-v08 3 exe、
732 单测 0 失败、verify-interaction 8 项 ok）**。仅新增轻量偏好文件与
位置锁定交互语义，不改数据、轮询或既有设置 / 缓存契约。依据
[`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX016。

> v0.9 起 `floating-preferences.json` 升级为格式 2（见 UX017 契约）；
> 上文「格式 1」保留为历史定义，且仅作为读取迁移入口。

- **共享菜单项**：圆圈右键菜单与托盘菜单在「查看全部额度 / 设置」之后新增 checkable
  「锁定位置」，共用 FloatingQuotaForm 的同一状态与处理器，勾选状态实时同步；默认解锁。
- **锁定行为**：仅阻止鼠标拖动移动圆圈；普通左键 / `Enter` / `Space` 详情、右键菜单、
  显示 / 隐藏不受影响；锁定时超过拖动阈值的手势不打开详情，阈值内单击仍有效；
  切换时释放 Capture 并重置进行中手势。
- **偏好文件**：`floating-preferences.json` 格式 1，仅 `Version`（int）与
  `PositionLocked`（bool），identity-free、不含坐标 / 身份 / 额度；严格字段数、
  类型与版本校验，并设明确文件大小上限 `MaxFileBytes = 4096` 字节（读取前检查，
  超限视为损坏）；损坏 / 未知字段 / 错版本 / 超限一律回默认（解锁）；与
  `floating-settings.json`（格式 1 / DPAPI）互相独立。
- **原子写**：temp + `File.Replace`（无旧文件时 `File.Move`，参考 FloatingSettingsStore）；
  写失败不得破坏既有有效文件。
- **失败提示**：保存失败保持旧状态与菜单勾选，不回显异常原文，不声称已保存。
  提示分两路：circle 可见时仅给短可读 circle tooltip status；circle 不可见时由
  托盘 `NotifyIcon.ShowBalloonTip` 给短文案（固定文本，非 raw 异常）。成功切换
  不弹任何托盘通知；offline 注入模式不注册 NotifyIcon，仅以可观测的「通知意图」
  计数暴露给测试；不强制打开 circle / 详情，不改轮询 / 查询契约。
- **零副作用**：启动 / smoke 默认零写入；加载 / 保存可注入；quota / auth 缓存、
  轮询间隔、单实例契约不变。

## UX017 减少动画与悬浮偏好格式 2 契约（v0.9）

状态：**已实施，T044 done，主代理 2026-10-04 独立验收接受（build bin-v09 3 exe 无告警、
820 单测 0 失败、verify-interaction 8 项 ok）**。仅新增「减少动画」偏好与
格式 2 迁移语义，不改数据、轮询或既有设置 / 缓存契约。依据
[`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX017。

- **共享菜单项**：圆圈右键菜单与托盘菜单在「锁定位置」之后新增 checkable
  「减少动画」，共用 FloatingQuotaForm 的同一状态与处理器，勾选状态实时同步；
  默认 `false`。
- **动画语义**：开启即停止水波动画 Timer 并立即重绘静态波面；剩余比例 / 百分比与
  0 / 100 / 未知显示不变；关闭仅在圆圈可见且已知 `0 < percent < 100` 时重启动画；
  隐藏 / 端点 / 未知不启动；退出 disposed 后不再启动。后台轮询 10s / 5min、
  打开零查询与单实例契约不变。
- **偏好文件格式 2**：`floating-preferences.json` 字段为 `Version`（int，= 2）、
  `PositionLocked`（bool）、`ReduceMotion`（bool）；严格字段数（=3）、类型与版本
  校验，沿用 `MaxFileBytes = 4096` 字节（读取前检查，超限视为损坏）；extra /
  wrongtype / 错版本 / 超限一律回默认，不做容错兼容。
- **格式 1 迁移（只读）**：读取严格格式 1（`Version`=1 + `PositionLocked`，字段数 =2）
  迁移为**内存**格式 2 且 `ReduceMotion=false`；加载不写盘，后续任一偏好保存写
  格式 2 并同时携带两个 flag（减少动画开关互不覆盖锁定）。**迁移理由**：新增用户
  偏好需兼容已落盘的格式 1，只读迁移避免升级即写盘；文件仍 identity-free，
  无凭据、无位置坐标。`floating-settings.json`（格式 1，**明文 JSON、无 DPAPI 保护**，
  只存产品 / 周期标识）与 quota 缓存（格式 1）均不变。
- **同值与失败**：同值不写盘；保存失败保持全部旧偏好与两菜单勾选；提示分两路
  （circle 可见 tooltip / circle 不可见托盘短固定文案），文案区分字段——减少动画
  失败为「动画设置未保存」/「减少动画设置未保存」，不得误称锁定保存失败。
  成功切换不新增托盘通知。
- **原子写**：沿用 temp + `File.Replace`（无旧文件时 `File.Move`）；写失败不得
  破坏既有有效文件。

## 复制摘要与剪贴板契约（v0.11）

状态：**已实施，主代理 2026-10-04 独立验收接受 T046（bin-v11 build 3 exe、906/0、
smoke 0、交互 8 项 ok、launcher 20 项 ok）**。仅新增详情 footer 的复制摘要交互，
不改数据、轮询、设置 / 锁定 / 减少动画或既有菜单契约；**不新增查询**。依据
[`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX019。

- **按钮**：详情面板 footer 右侧一个**可聚焦** Button「复制摘要」（`TabStop=true`、
  有 `AccessibleName` 与 tooltip），与左侧两行文字（最后更新时间 / 状态 note）**不重叠**；
  100 / 150 / 200% 及窄屏（含 2x）下按钮 label 与文字列均不得越界 / 交叉。
- **内容**：仅在**存在快照**（`v.Data != null`）时启用；点击复制**当前已渲染的全部额度**
  （周期标签、剩余百分比、可用剩余额度、重置时间）与最后成功更新时间；**不含**身份 /
  profile / 凭据 / scope 指纹。
- **安全边界（formatter 为独立信任边界）**：纯 formatter **不得转写**上游任何
  `Error` / `Message` / `UnknownNote`，全部改用**固定安全文案**；不依赖 parser sanitize。
- **诚实数据**：未知绝不写作 0；`0` / `100` / `<1%` 沿用 bar 文案；`total=0` / `cache` /
  `stale` / `identityUnknown` 各自有明确标记；`null` product / period 安全跳过。
- **触发与副作用**：**只有用户 click** 才写剪贴板；自动刷新 / 打开 / 渲染 / 状态变化均不写；
  无 snapshot（含 identity 变化清空）时按钮禁用且不写；点击**不触发查询**，焦点 / scroll /
  卡片不被重建。
- **反馈**：成功在按钮上短 inline 显示「已复制」（约 2s 后恢复），失败固定「复制失败」且可重试；
  不覆盖 footer 错误 / 更新时间文字，不弹阻塞对话框。

> **v0.13 覆盖说明（UX021，T050）**：上方「触发与副作用」中「**只有用户 click**
> 才写剪贴板」自 v0.13 起被增量覆盖为「**显式 click 或详情内有效本地 Ctrl+C**」
> 二选一；自动打开 / 刷新 / 渲染仍零 copy。其余复制内容 / 安全边界 / 诚实数据 /
> 反馈语义不变，见下方「详情快捷键契约（v0.13）」。

## 详情快捷键契约（v0.13）

状态：**已实施，主代理 2026-10-04 独立验收接受 T050（GLM `tl-openai/glm-5.3-flash` 执行）**。
仅新增详情面板（PopupForm）两个本地快捷键；不改数据、轮询、查询结构、设置 /
锁定 / 减少动画 / 归位或既有菜单契约；不新增 query-source。依据
[`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX021。

- **Ctrl+R**：精确 `Control|R`，复用既有刷新按钮 Click 路径（`OnRefreshRequested`）；
  single-flight 语义不变——已有 pending 查询时**不排队、不重复触发**；这是
  显式手动查询，不是新增后台轮询。
- **Ctrl+C**：精确 `Control|C`，复用既有「复制摘要」按钮 Click 路径
  （`OnCopySummaryClicked`）：当前快照的纯 formatter、固定安全文案、安全身份边界
  （不含身份 / profile / 凭据 / scope 指纹、不转写上游原文）、inline 反馈均不变；
  无 snapshot 时**不写剪贴板但消费该键**，不触发查询。
- **生效条件**：仅当详情 `Visible && Enabled && ContainsFocus && !IsDisposed &&
  !Disposing` 且 `!_dialogOpen && !_menuOpen` 时拦截；隐藏 / 未聚焦 / 设置 modal
  打开 / 菜单打开一律走 base 处理，无本动作、不打开任何窗口。
- **键识别边界**：仅识别 `Control|R` / `Control|C` 两个精确组合；Ctrl+Shift /
  Ctrl+Alt 等修饰组合走 base；不改变 Esc 既有行为；不使用全局热键 / SendKeys /
  输入注入真实桌面；设置 ComboBox 本地按键归其自身窗口。
- **Tooltip**：刷新 tooltip 追加 Ctrl+R、复制 tooltip 追加 Ctrl+C；按钮 label /
  `AccessibleName` / footer 布局不变。
- **测试边界**：测试经真实有焦点子控件向表单派发（反射调用实际 Button 的
  protected `Control.ProcessCmdKey`，沿正常父链派发 keyData），不使用 SendKeys /
  全局热键；无 production testhook。

## 悬浮窗归位契约（v0.12）

状态：**已实施，主代理 2026-10-04 独立验收接受 T047（bin-v12 首次 959/0、返修后最终 963/0、
smoke 0）**。仅新增显式归位交互，
不改数据、轮询、设置 / 锁定 / 减少动画或既有菜单契约。依据
[`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX020。

- **共享菜单项**：托盘与圆圈右键共享菜单在「减少动画」之后新增「悬浮窗归位」，
  单一处理器在 TrayApp；菜单项数 7→8，「隐藏悬浮窗」toggle 索引 4→5（有意收紧）。
- **归位目标**：当前圆圈所在屏工作区右下默认安全 margin（复用既有
  `InitialBounds` / `ClampTo` 规则）；圆圈尚未定位时取 primary 屏；尺寸用该屏
  DPI 缩放的 136dp 逻辑直径；归位后圆圈显示。显式归位为强制路径，不走
  「仅首次定位」分支、不忽略指定 work area。
- **手势与锁**：归位先重置进行中拖动手势并释放 Capture（后续 mousemove 不拖回、
  不误开详情）；位置锁定不阻止显式归位。
- **详情协调**：详情可见时先走既有 `HidePanel`（其 VisibleChanged 已触发
  `RestoreAfterDetails`），处理器再显式调用 `RestoreAfterDetails` 清 stale 标志，
  然后归位；`_autoHiddenForDetails` 被清，之后详情关闭不会把圆圈拉回旧位置；
  归位不自动打开详情。
- **设置 modal**：`SettingsModalOpen` 为真时处理器原样 return——不移动 owner、
  不关闭 modal（真实用户在 modal 之上本就打不开菜单，程序化点击同等安全）。
- **零副作用**：零查询；不改 10s / 5min 轮询结构（可见性变化走既有事件把间隔
  调整为 10000ms）；不写偏好 / 坐标（两 prefs 零写入）；不改锁定 / 减少动画 /
  额度选择 / marker。位置仍不跨会话持久。
- **测试边界**：生产处理器与测试共用归位核心；离线测试注入 work area / scale，
  不依赖真实 Screen 几何。

## 本地启动入口与旧实例平滑替换契约（v0.10）

状态：**已实施，主代理 2026-10-04 独立验收接受 T045（launcher 20 项 ok、bin-v10 build 3 exe、
820/0、smoke 0、交互 8 项）**。依据
[`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX018。
纯本地启动 / 交付技术契约：不改任何数据格式、`usage plan` 契约与 UI 行为。

> **当前启动目标（T050 v0.13，主代理 2026-10-04 已独立验收接受）**：`start.cmd` 现调用
> `launch.ps1 -OutputDir bin-v13`，目标 `bin-v13\ark_left.exe --show`；正式 `bin` 仍 v0.2、
> 历史候选未覆盖。下方「入口链」中默认 `-OutputDir bin-v10` 为该契约冻结时的历史值，
> 现作为**历史定义保留**，当前默认目标为 `bin-v13`（主代理 2026-10-04 在授权内本地升级至 `bin-v13`：`-PreviousProcessId 9160` → CLOSED、LAUNCHED pid 15260）。

- **入口链**：`start.cmd` → `launch.ps1`（默认 `-OutputDir bin-v10`）→
  `bin-v10\ark_left.exe --show`；exe 缺失时经 `build.ps1 -OutputDir` 构建到该目录，
  绝不写旧 `bin`。启动脚本不设置 env、不写真实 state / 全局配置。
- **白名单目录**：项目根下一级 `bin`、`bin-release`、`bin-vN`（`-OutputDir` 目标亦须
  命中该模式且在根下）。实例身份三条件同时成立才可被处理：进程名 `ark_left` ∧
  主模块完整路径位于白名单目录 ∧ 文件名 `ark_left.exe`；项目外同名进程绝不
  扫描 / 唤醒 / 终止，绝不用纯名字匹配。
- **关闭协议（旧目录实例）**：`EnumWindows` + `GetWindowThreadProcessId` 定位该 PID
  唯一拥有顶层窗口（含隐藏窗）的 GUI 线程，`PostThreadMessage(WM_QUIT)` 恰一次；
  应用侧 `Application.Run` 返回后执行既有 `Cleanup()`（托盘 / 窗体 / CLI 释放）再
  退出，期望退出码 0。每实例等待上限 10s。超时 / GUI 线程不唯一或找不到 /
  指定 PID 不存在或非 ark_left / 路径不可读或不在白名单 → 简短错误 + 非 0 退出 +
  不启动新 exe。脚本侧绝不 Stop-Process / kill / 覆盖二进制。
- **同版本复用（目标目录实例）**：不关闭；再以 `--show` 启动一次，经既有
  `Local\ark_left_show_event`（含实例后缀）IPC 唤醒既有实例后短进程退出 0，
  原 PID 保持不变。
- **测试参数**：`-PreviousProcessId <pid>` 把候选严格限定为该 PID（仍强制白名单，
  其它 PID 绝不扫描 / 终止，生产默认才全量白名单扫描）；`-NoLaunch` 只请求旧实例
  退出，不构建、不启动；`-OutputDir` 相对根解析或绝对路径。
- **隔离测试约定**：沿用 `ARK_LEFT_STATE_DIR`（临时状态目录，预置 marker / 缓存 /
  选择文件并断言字节不变）+ `ARK_LEFT_INSTANCE_SUFFIX`（每次唯一）+ 不存在的
  `ARK_LEFT_CLI`（离线）；测试内所有 launcher 调用均带 PID 限定，绝不触真实用户
  实例。旧 v05 无 IPC 之外通道，靠线程 `WM_QUIT` 兼容路径，隔离实测退出 0 且
  状态文件保持、窗口清零、单实例互斥体释放。

## 悬浮窗可见性轮询契约（v0.5）

状态：**已冻结（v0.5，T032 主代理独立自动验收通过）**。仅定义 Timer 间隔规则，
**不改任何数据格式**（额度缓存格式 **1**、悬浮设置格式 **1** 均不变），业务基线仍 `v0.1`。
依据 [`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) UX013。
本契约由主代理授权工程决定（**非用户逐项指定值**）。**格式 / 间隔值变化必须先改本文件并提升版本。**

- **base（全隐藏）间隔**：`SnapshotController.PollIntervalMs = 300000`（原值保留；语义为圆圈与详情皆隐藏）。
- **可见间隔**：`SnapshotController.VisiblePollIntervalMs = 10000`。
- **判定**：`圆圈可见 || 详情可见` → 10,000 ms；否则 → 300,000 ms。
- **仅调间隔**：可见性切换只改当前 Timer 的 `Interval`，**不触发立即查询、不重启 / 不新建 timer / 不新增线程**；
  下一 tick 按新间隔生效；目标间隔与当前相同时**不重复赋值**（避免重置计时）。
- **共享 controller**：定时 / 手动 / 启动沿用同一 `SnapshotController` 与既有 single-flight，
  **不排队、不取消在途 query**；等待 data / 焦点 / scroll 不变。
- **设置弹窗**：不计入可见判定，不单独提速。
- **退出**：disposed 后不再重启 timer。

## 契约冻结与并行

- 字段与版本已由真实脱敏查询确认，解析契约作为 **v0.1** 冻结。
- 后续字段变化需先更新本文件并提升版本，再同步实现与测试。

## 约束

- **不虚构真实 API / 字段**：未确认的一律标记"候选 / 未实测 / 待定义"，不写占位假值。
- 本文件不包含任何真实账号 ID / 凭据 / viewer 样例；测试数据均为合成匿名 fixtures。
