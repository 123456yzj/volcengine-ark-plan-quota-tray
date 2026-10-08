# 数据与接口契约

上游解析契约 **v0.2**；额度快照格式 **1**；悬浮选择格式 **1**；悬浮偏好格式 **2**。Agent Plan 个人版生产链路以 [直连契约](direct-agent-plan.md) 为准；下述 CLI auth/viewer 和 items 解析保留为兼容契约。当前交互以 [交互需求](requirements/interaction-improvements.md) 为准。

CLI auth / usage 字段来自 2026-10-03–04 的真实脱敏查询；GetAFPUsage 字段与 SSO / refresh grant 于 2026-10-07 真实验证。其他套餐主要由合成用例覆盖；其验收边界见 [implementation-gaps.md](implementation-gaps.md)，保留的 Runtime 生命周期方案见 [managed-runtime.md](managed-runtime.md)。

## 调用与进程

- Agent Plan 个人版认证为浏览器 Authorization Code + PKCE S256 / 手动粘贴完整 localhost 回调 URL，不启动回调监听；查询为临时 STS 签名的 `GetAFPUsage`，不调用 ArkCLI。具体流程、校验、加密存储和错误分类见 [直连契约](direct-agent-plan.md)。
- CLI 兼容认证闸门为 `auth status --format json`：退出码为 0 且顶层 `logged_in` 为布尔 `true` 才查询额度。其他 ArkCLI 能力不在本次切换范围内。
- 仅调用原生 `.exe`，不托管 `.ps1` shim。当前分支解析顺序为存在的绝对 `ARK_LEFT_CLI` 路径 → PATH / npm 对应架构的原生 exe；无效覆盖明确失败。Managed Runtime 为计划 / 待验收，尚未进入当前分支实现。
- 子进程 `UseShellExecute=false`、`CreateNoWindow=true`、UTF8，异步读取 stdout / stderr，有界等待退出。普通查询每命令 30 秒超时；auth 与 usage 顺序执行，总耗时可能超过 60 秒。
- 超时、取消或退出终止活动子进程；Dispose 后不再启动。非零退出不能当作成功。auth / usage 两阶段持有同一个 Runtime lease 的托管查询流程为计划 / 待验收，尚未进入当前分支实现。
- 应用内“设置 → 重新登录”走直连浏览器 SSO，等待上限 10 分钟，可取消。“设置 → 登出”取消活动登录、查询和续期，清除本项目登录凭据与额度缓存；删除凭据失败明确报告未完成。两项账号操作只放在共享菜单的设置子菜单，具体并发边界见直连契约。

## 身份与 scope

| 来源 | 字段 |
| --- | --- |
| auth 顶层 | `logged_in`（bool）、`active_profile`（object） |
| auth.active_profile | `name`、`type`、`owner_trn`、`region`、`project` |
| usage.viewer | `account_id`、`user_id`、`profile`（string）、`tenant`、`region`、`project_name`、`is_root`（bool） |

`viewer.profile` 是字符串，不是嵌套的 profile 对象。scope 指纹为 UTF8 的 `SHA256(owner_trn + "\n" + name + "\n" + region + "\n" + project)`；任一必要字段缺失或 owner_trn 无法解析时为 Unknown。

owner_trn 只接受 `trn:iam::<account>:root` 或 `trn:iam::<account>:user/<id>`；区域槽为空，账户与子用户 ID 非空。usage 必须精确匹配账户、profile、区域、项目和主 / 子身份；空白字段视为缺失，不截断或补造 ID。

- **Same**：全部定义身份的字段存在且一致；子用户还须有匹配 user_id。
- **Mismatch**：存在具体冲突，包括子用户 auth 对明确的 root viewer。
- **Unknown**：缺字段或无法确认；不证明身份不同。

原始身份只在解析内存中使用，不展示、不落盘；不可逆指纹用于缓存归属并可存入加密快照，不展示。直连会话使用随机登录绑定形成 SHA256 scope，续期保持、重新网页登录更换；其 Result 与发起签名的会话直接绑定，不构造 CLI viewer。测试用合成匿名 fixture。

## usage 数据与数值

顶层为 `viewer` + `items`。字段缺失与合法零值必须区别对待。

| 层级 | 字段 | 语义 |
| --- | --- | --- |
| item | `product` | `agent-plan`、`coding-plan`、`agent-plan-team`、`coding-plan-team`；未知产品不能伪造为已知套餐 |
| item | `edition`、`tier` | 可缺省，参与产品稳定 key |
| item | `subscribed` | bool；缺失 / 非布尔为订阅未知或格式错误 |
| item | `periods` | 周期数组；已订阅但缺失时为错误 |
| item / period | `error` | 条目 / 周期失败；显示固定安全文案，不转写原文 |
| item | `updated_at` | 服务端数据更新时间，可能缺失；Coding Plan 实测位于 item 级 |
| period | `label` | 如 `5h`、`weekly`、`monthly`、`session` |
| period | `used`、`total` | 可缺省，须为有限非负数；单位未证实，界面称“额度” |
| period | `percent` | 已用百分比；数值有效时优先使用 |
| period | `reset_at` | 服务端重置时间，RFC3339；与查询时间分开 |
| 本地快照 | `fetched_at` | 本地本次查询时间，不是服务端更新时间 |

- 有效 `percent` 优先：剩余百分比为 `clamp(100 - percent, 0, 100)`。
- 无有效 `percent` 才换算：有限 `used >= 0` 与 `total > 0` 时，剩余为 `clamp((total - used) / total * 100, 0, 100)`。
- `total=0` 不能用于换算；如果独立 `percent` 有效，仍可显示其剩余百分比。其他缺失 / 非法结果保持未知，不补 0。
- 剩余绝对量仅在有限 `used >= 0`、`total > 0` 时为 `max(total - used, 0)`；差值须有意义。截断越界需提示异常。
- 接受常见数值类型；非法 / 超范围日期不能崩溃或回退为当前时间。
- 周期错误优先，不计算其百分比、不当作未订阅。只有无错误且明确未订阅的产品可以隐藏；有产品错误但仍有可用周期时展示可用部分，整体为部分失败。

## 额度快照格式 1

位置为 `Marker.StateDir()` 下的 `quota-cache.dat`，默认 `%LOCALAPPDATA%\ark_left`，可由 `ARK_LEFT_STATE_DIR` 隔离。

- Windows DPAPI CurrentUser 加密整份 blob；跨用户不可读。不保存凭据、owner_trn、account_id 或用户名，只存 scope 指纹与展示语义。
- 保存订阅 `Subscribed` / `SubscribedKnown`、产品与周期错误状态、数值 known 标志及值、clamp 标志、产品 / 周期标签和查询 / 更新 / 重置时间；未知不能经缓存变成 0。
- 仅 Same 的 `Ok`、`PartialError`、`NoSubscription` 保存。失败、取消、超时、未登录、缺 CLI 或 Unknown 不写缓存。
- 严格检查版本、结构、日期与 DPAPI；无效按无缓存处理。非法日期不推断“现在”。
- 写临时文件后 `File.Replace`，无旧文件时 `Move`；失败保留旧有效文件，不以 Copy+Delete 降级覆盖。

| 情况 | 展示与缓存处理 |
| --- | --- |
| 启动加载历史 | 显示“上次数据”，待后台确认 |
| 普通认证 / 网络失败、超时或取消 | 保留历史与原查询时间，短提示 |
| 明确未登录、auth 确定新 scope、usage Mismatch | 立即清旧内存与磁盘；身份确认阶段允许提前清，不显示过程态 |
| 新身份成功结果 | 替换展示，满足 Same 时持久化 |
| Unknown 且有已确认历史 | 不以新的 Unknown 结果覆盖或持久化已有确认数据 |
| Unknown 且无历史 | 受限显示本次结果，标明身份未确认，不缓存 |

## 悬浮选择格式 1

同一 state dir 下明文 `floating-settings.json`，恰有三个字段：

```json
{ "Version": 1, "ProductKey": "<Product|Edition|Tier>", "PeriodLabel": "<label>" }
```

ProductKey 用 `Product + "|" + Edition + "|" + Tier`，缺失段为空；只存标识，不含身份、凭据、额度或坐标，与额度缓存独立。

Version 必须为整数 1；key / label 必须为非空字符串，长度分别不超过 128 / 64。缺字段、多字段、未知字段、错误类型、损坏或错误版本一律按未配置处理，不造假选择。只有显式用户保存写盘；临时文件 + Replace / Move，失败保旧，不应用失败的保存。

## 悬浮偏好格式 2

同一 state dir 下明文 `floating-preferences.json`，恰有 `Version`（int = 2）、`PositionLocked`（bool）、`ReduceMotion`（bool）。只存两个偏好，不含身份、额度或坐标；读取前检查文件大小不超过 **4096 字节**。

严格格式 1（恰为 Version=1 与 PositionLocked 两字段）只读迁移为内存格式 2，ReduceMotion=false；加载不写盘，后续显式保存写格式 2，同时携带两个 flag。同值不写盘；损坏、多字段、类型 / 版本不符或超限回默认。原子写失败保留旧文件与全部旧偏好，提示对应字段失败。

## 窗口、复制与轮询

窗口尺寸、定位、选择、菜单、锁定、动画、归位、复制与快捷键行为统一见 [交互需求](requirements/interaction-improvements.md)。复制 formatter 独立过滤原始错误和身份信息，不能依赖 parser 已净化。

轮询参数为 `SnapshotController.VisiblePollIntervalMs = 10000`、`PollIntervalMs = 300000`；设置窗口不单独提速，可见性切换只改间隔。启动、定时、手动刷新共用 single-flight，不排队。额度缓存和选择 / 偏好格式互相独立。

## 本地启动与实例替换

- 入口为 `start.cmd` → `launch.ps1`（当前默认 `-OutputDir bin-v16`）→ `bin-v16\ark_left.exe --show`。目标 exe 缺失时由 build.ps1 构建到目标目录。
- 目标必须为项目根下一级 `bin`、`bin-release` 或 `bin-vN`。当前旧实例清单明确枚举 `bin`、`bin-release`、`bin-v04`–`bin-v15`，排除目标本身；不是任意 `bin-vN` 都会被关闭。
- 实例须同时满足进程名 `ark_left`、完整主模块路径在白名单、文件名 `ark_left.exe`。名字只用于发现候选，项目外已确认路径的同名实例跳过；无法核实路径则失败。
- 旧版经 EnumWindows / GetWindowThreadProcessId 定位唯一 GUI 线程（含隐藏窗口），只投递一次 WM_QUIT，应用退出消息循环后自行 Cleanup；每实例等待最多 10 秒。不能定位唯一线程、超时或身份验证失败时非零退出，不启动新 exe；不强杀或覆盖活动二进制。
- 同目标版本不关闭，另启动 `--show` 经 `Local\ark_left_show_event`（含实例后缀）IPC 唤起，原实例继续驻留。
- `-PreviousProcessId` 仅考虑指定 PID，仍验证白名单；`-NoLaunch` 只请求旧实例退出，不构建、不启动。
- 隔离检查使用 `ARK_LEFT_STATE_DIR`、`ARK_LEFT_INSTANCE_SUFFIX` 和离线 CLI 覆盖，launcher 调用限定 PID，核对进程 / 窗口退出、单例释放及状态文件保持。
