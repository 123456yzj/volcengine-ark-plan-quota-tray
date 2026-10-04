# ark_left

一个 **Windows 桌面右下角（系统托盘）的火山引擎方舟订阅额度查询小插件**。

常驻托盘图标，并在桌面右下角显示**悬浮圆圈**（水波表现剩余、圈内显示剩余百分比）；
**左键点击圆圈**查看当前身份下火山方舟订阅（Agent Plan / Coding Plan）的
**剩余百分比（为主）/ 剩余量（若有）/ 周期与重置时间**，**右键圆圈**可选设置。
数据来源为**复用本机已登录的 ArkCLI**，插件内不另存凭据。

> 当前状态：业务基线 `v0.1` 不变；交互 **`v0.14 UX022`**（主代理 2026-10-04 独立验收接受 T052；详情卡片化 card-only，无 header / footer / 外框；可见时每 10 秒 / 全隐藏每 5 分钟轮询）、额度缓存格式1、floating 选择格式1、floating 偏好格式2（只读迁移严格格式1）；圆圈 136dp。
> 主代理 2026-10-04 已独立验收接受 T041（**538/0**、smoke 0）、T042（**668/0**）、T043（**732/0**、smoke 0、8 项交互）、T044（**820/0**、smoke 0、8 项交互）、T045（launcher **20 项 ok**）、T046（bin-v11：**906/0**、smoke 0、8 项交互、launcher 20 项 ok）、T047（bin-v12：首次 **959/0**；返修后最终 **963/0**、smoke 0）、T050（bin-v13：主代理在最后测试修订后实际重建 3 exe、**1030/0**、smoke 0、8 项交互 suffix 9d4a1d0b、launcher 20 项 suffix d3df298f）。T048 纯文档收尾已由主代理接受（真实执行句柄 `ses_efbe4d1dcffeLACoYeQUlkmPRk`，返修 2/2、逻辑累计 2）；T051 纯文档收尾已由主代理接受（真实会话 `ses_efba75eecffeKK16MP32JytHNq`，返修 1/2、逻辑累计 1）。
> 历史 v0.13 升级记录：已由主代理在授权内本地升级到 **v0.13**：`start.cmd` → `launch.ps1 -OutputDir bin-v13`；`launch.ps1 -OutputDir bin-v13 -PreviousProcessId 9160` → 旧进程 CLOSED（exit 0），LAUNCHED pid 15260 `D:\ark_left\bin-v13\ark_left.exe`（2026-10-04T08:37:09+08:00 只读核实仅剩新版 PID 15260 Responding、1 visible window）。**正式 `bin` 仍 v0.2、历史候选未覆盖**；v0.12 升级记录标为历史。
> T007 人工验收（真实托盘 / 剪贴板 / 视觉 / 多屏）仍 pending，**系统 Gate 未通过**；`bin-v12/t048-sha256.csv` 为 T048 生成时的**历史快照**（48 文件 manifest、0 mismatch 检查），后续任务文档变化**不自动刷新旧 manifest**；`bin-v13/t051-sha256.csv` 已由主代理实际生成并校验（48 文件、0 mismatch）；清单记录生成时各文件 SHA256，后续变更需重新生成。
> T050（interaction **v0.13 UX021** 详情快捷键 `Ctrl+R` / `Ctrl+C`）**已由主代理独立验收接受**：候选 `bin-v13`（0.13.0.0）由主代理在最后测试修订后实际重建，非仅复用 GLM 自测；`ark_left.exe` SHA256 `EE871EF2…ADE3`、`ark_left-tests.exe` SHA256 `52EABABC…E8DA`。
> **T052（interaction `v0.14 UX022` 详情卡片化 card-only 弹窗）已由主代理 2026-10-04 独立验收接受**：候选 `bin-v14`（0.14.0.0）经 `build.ps1 -OutputDir bin-v14` 构建，先首轮单测 **1079/10**，DS4.1 修复后 **1088/0**、smoke 0；主代理实际复跑 **1088/0**、smoke ExitCode 0、verify-interaction 8 项（suffix 03102674）、verify-launcher 20 项（suffix 9015f7c6）；合成 `preview-intro.png` 确认卡片无 header / footer / 外围留白（非人工点击）；`launch.ps1 -OutputDir bin-v14 -PreviousProcessId 2268` → 旧 CLOSED（exit 0）、LAUNCHED pid 10600。实现：启动接续 GLM 真实会话 `ses_efb1e82ceffeUTT35h1tuqx2u2`（launch 默认 / 白名单、`start.cmd`）、DS4.1 修复真实会话 `ses_efb187030ffepFLzeUgbDMfIXT`（TrayApp / tests）。接受代码与自动测试，真实剪贴板 / 多屏 DPI / 人工托盘仍 pending。
> 本轮未独立运行 `check.ps1`、未核验新真实账号 / 联网数据；启动存活不等于真实额度正确。

## 目录导航

| 路径 | 用途 |
| --- | --- |
| `README.md` | 项目说明、构建 / 启动 / 测试命令（本文件） |
| `AGENTS.md` | 多 Agent 协作规则与写入所有权约定 |
| `docs/requirements/` | **需求基线目录（权威）**：索引、正式需求、交互改进、已实现行为 |
| `docs/requirements/interaction-improvements.md` | **v0.14 UX022 当前权威**（T052 主代理 2026-10-04 已独立验收接受）；v0.13 UX021 / v0.12 UX020 / v0.11 UX019 / v0.10 UX018 / v0.9 UX017 / v0.8 UX016 / v0.7 UX015 / v0.6 UX014 / v0.5 UX013 / v0.4 UX012 / v0.3 UX011 未被覆盖部分继续有效；v0.2 UX001–UX010 历史保留 |
| `docs/setup.md` | 本地设置指南（安装 / 登录 / `ARK_LEFT_CLI` / 托盘折叠处理） |
| `docs/requirements.md` | 兼容入口：仅跳转到 `docs/requirements/`，不再承载正文 |
| `docs/contracts.md` | 技术栈、数据契约与身份 scope（已由真实脱敏查询确认） |
| `docs/tasks.md` | 任务列表与状态跟踪 |
| `docs/decisions.md` | 问题与决定记录（OPEN/DECIDED/CLOSED） |
| `docs/verification.md` | 验证与验收记录 |
| `src/` | 源码（C# 5 / WinForms，无 NuGet 依赖） |
| `tests/` | 无外部依赖的测试（合成匿名 fixtures）；`verify-interaction.ps1` 为隔离交互集成检查 |
| `build.ps1` / `test.ps1` / `check.ps1` / `start.cmd` | 构建 / 测试 / 真实查询 / 启动 |

## 前置条件

- Windows。
- **.NET Framework 4.8**（本机验证的最低版本；系统自带）。**无需 dotnet SDK、无需 NuGet。**
- 本机存在可用的 **ArkCLI 且已登录**（`arkcli auth login volc-sso`）作为数据来源。
- 构建使用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5）；
  缺失时自动回退 32 位 `Framework\v4.0.30319\csc.exe`。

## 构建 / 启动 / 测试（真实命令）

在项目根目录 `D:\ark_left` 执行：

```powershell
# 推荐：构建 v0.14 候选到独立目录（不覆盖正式 bin 与 bin-v13）
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir D:/ark_left/bin-v14

# 推荐：离线运行 v0.14 单元测试（合成匿名 fixtures，不联网；不覆盖正式 bin）
# 在当前 PowerShell 会话内临时设置隔离状态目录，并在 finally 恢复原值
# 直接在当前会话执行以下多行脚本；不要包进 -Command 的双引号字符串，否则父会话会先展开 $env:
$arkLeftPreviousState = $env:ARK_LEFT_STATE_DIR
try {
    $env:ARK_LEFT_STATE_DIR = 'D:/ark_left/bin-v14/t052-local-state'
    & 'D:\ark_left\bin-v14\ark_left-tests.exe'
} finally {
    $env:ARK_LEFT_STATE_DIR = $arkLeftPreviousState
}

# 真实查询一次并输出脱敏摘要（product / 周期 / 剩余 / 重置，不含 viewer / 凭据）
powershell -NoProfile -ExecutionPolicy Bypass -File check.ps1
```

> 历史默认构建命令 `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1`（无 `-OutputDir`）
> 会写正式 `bin\`（v0.2），仅作历史参考，**不是** v0.14 推荐构建 / 验收路径。

> 注：`test.ps1` 未带 `-OutputDir` 时默认构建 / 覆盖正式 `bin`（旧 v0.2 正式目录），
> **不要**把该默认调用当作 v0.14 验收路径；如需脚本化测试请显式指定 `-OutputDir`。

启动应用：

- 双击 `start.cmd`（当前 → `launch.ps1 -OutputDir bin-v14`，按需先构建再启动 `bin-v14\ark_left.exe --show`）；或
- 直接运行 `bin-v14\ark_left.exe`（当前 v0.14 候选；**首次运行**显示悬浮圆圈，此后启动静默驻留、不自动弹出详情）；或
- 运行 `bin-v14\ark_left.exe --show`（**仅强制显示悬浮圆圈**，不强制打开详情，便于验证）。
- 正式 `bin\ark_left.exe` 仍为 v0.2 历史产物，仅历史命令参考。

> 区别：`start.cmd` / `launch.ps1` **始终以 `--show` 启动**（必然显示圆圈）；直接双击
> `bin-v14\ark_left.exe`（无参数）**首次显示圆圈、之后静默**，二者行为不同。

> **当前启动（v0.14 UX022，T052 主代理已独立验收接受）**：`start.cmd` 调用
> `launch.ps1 -OutputDir bin-v14`，目标为 `bin-v14\ark_left.exe --show`；同版实例经单实例
> 唤醒复用，项目旧构建目录实例先验证目标再以 `WM_QUIT` 温和退出，失败不 kill。
> 主代理 2026-10-04 已在授权内实际升级：`launch.ps1 -OutputDir bin-v14 -PreviousProcessId 2268`
> → 旧进程 CLOSED（exit 0）、LAUNCHED pid 10600 `D:\ark_left\bin-v14\ark_left.exe`；
> v0.13 升级记录（`-OutputDir bin-v13 -PreviousProcessId 9160` → pid 15260）保留为历史。
> **正式 `bin\` 仍 v0.2、尚未更新**；v0.3–v0.13 历史候选保留、未覆盖。
> 历史 `bin-v10` 的启动命令示例保留为历史；当前构建：
> `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir D:/ark_left/bin-v14`。
> 若要在本机体验 v0.14，直接双击 `start.cmd`（自动构建缺失目标）或运行
> `bin-v14\ark_left.exe --show`；有旧实例时由 launcher 处理，不要同时启动双实例。
> v0.14 UX022 详情弹窗只显示卡片本身（无 header / footer / 外框 / 外围留白），复制走 card 右键菜单 / 详情内 `Ctrl+C`；
> 全部打开路径**零查询**语义不变。
> T007 人工视觉 / 真实托盘 / 剪贴板仍 pending。

启动策略：**首次运行**会打开悬浮圆圈，
并写入仅含日期 / 版本的 `%LOCALAPPDATA%\ark_left\first-run.done`；
之后启动**静默驻留**，不自动弹出详情。每次启动后台立即查询一次；此后**圆圈或详情任一可见时每 10 秒**
轮询，**两者都隐藏时每 5 分钟**轮询（隐藏仍继续）。
打开圆圈 / 详情只显示已有状态，不触发查询；无缓存时固定“暂无数据”。
重复启动**不会**弹提示框，而是唤起已在运行的窗口并静默退出（仍只有一个托盘图标）。
可用环境变量 `ARK_LEFT_STATE_DIR` 指定隔离的状态目录（测试 / 调试用）。

离线 GUI 冒烟（不联网，显示合成数据并对**布局边界**做断言，约 1.6s 后自动关闭）：

```powershell
bin-v14\ark_left.exe --smoke-test
```

> `--smoke-test` 验证空态与大数据的工作区 / 子控件边界、固定尺寸、隐藏语义，
> 等待 / 相同展示数据（忽略 fetchedAt）/ 失败 / 同尺寸重开不重建卡片与子控件、焦点滚动保持、无冗余文字。
> 写出输出目录内 `preview.png`、`preview-error.png`、`preview-identity.png`、
> `preview-intro.png`（兼容旧文件名，现为无 banner 面板）——均为
> **该合成窗口自身**的合成图（`DrawToBitmap`，不截取桌面），供审查参考。
> **不声称视觉验证**，也不做人工点击或整屏截图；**不写首次运行标记**、**不触碰真实单例 IPC**。
> 正常启动（无参数）不会自动关闭，常驻托盘。
>
> v0.4 冒烟另写出 `preview-floating.png` / `preview-floating-0.png` /
> `preview-floating-100.png` / `preview-floating-unknown.png` / `preview-settings.png`
> （均为合成窗口自绘，非桌面截图 / 非视觉验收）；见 `bin-v04`。
> v0.4 合成预览（**合成图，非桌面截图 / 非视觉验收**）：
> [`bin-v04/preview-floating.png`](bin-v04/preview-floating.png)（悬浮圆圈，剩余水波）、
> [`bin-v04/preview-settings.png`](bin-v04/preview-settings.png)（设置弹窗）。
> v0.12 候选 `bin-v12` 沿用上述合成预览（含 `preview-floating-static.png`），均为合成图，
> 非桌面截图 / 非视觉验收；`bin-v12/t048-sha256.csv` 为 T048 生成时的**历史快照**（48 文件 manifest），
> 后续任务文档变化**不刷新该旧 manifest**（当时「最终文档落盘后刷新」仅指 T048 时点，保留为历史）。

隔离交互集成检查（真实起进程，但使用隔离状态目录 / 实例名与不存在的 `ARK_LEFT_CLI`，
只操作本脚本自己启动的 PID，不碰真实用户实例与 marker；需在有桌面会话的环境运行）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-interaction.ps1 -OutputDir D:/ark_left/bin-v14
```

> 流程：首个实例 `--show` 出现可见窗 → `WM_CLOSE` 断言“隐藏而不退出” → 不带 `--show`
> 的第二实例退出 0 并唤起首窗（仍单实例）→ 停止首实例后以同 marker 启动必须静默不显示窗口
> → 再用 `--show` 第二实例唤起。`finally` 终止本脚本进程、恢复环境、清理隔离目录。

## 使用体验

1. 启动后托盘出现图标，**默认打开入口为右下角悬浮圆圈**（系统可能将托盘图标收入折叠 / 溢出区，属系统行为）。
2. 圆圈为**深色底青绿双色轻波**（v0.6 UX014 起采用海军蓝 / 青绿 / 瓷白视觉），直径约 **136dp**，圈内显示**剩余百分比**与短产品 / 周期字；**左键点击圆圈**（或键盘 Enter / Space）
   打开全部额度详情，**零查询**；详情显示套餐、周期、**剩余百分比 + 进度条**、若有剩余量（单位“额度”）、重置绝对时间、简短最后更新时间；详情 footer 右侧有可聚焦的**「复制摘要」**按钮（仅点击**或详情内 `Ctrl+C`** 时写剪贴板，只复制当前已渲染额度与最后成功更新时间，不复制上游错误 / 身份原文）。
3. **右键点击圆圈**弹出与托盘一致的 8 项共享按钮组：**查看全部额度 / 设置 / 锁定位置 / 减少动画 / 悬浮窗归位 / 隐藏悬浮窗 / 分隔 / 退出 ark_left**。「设置」打开单实例 modal，用 `ComboBox` 选择要显示的套餐 / 周期，`Esc` / 取消 / `Alt+F4` 不保存、保存失败保持弹窗并 inline 提示、关闭后回原详情并保留焦点 / 滚动，打开 / 保存 / 取消全程**零查询**；「锁定位置」只阻止鼠标拖动、不阻止点击查看详情，位置仍本会话有效、不持久化坐标；「减少动画」停止水波动画并重绘静态波面，偏好跨启动记忆（`floating-preferences.json` 格式2，只读迁移严格格式1）；「悬浮窗归位」把圆圈移回**当前所在屏**工作区右下（未定位时取 primary 屏）、按该屏 DPI 的 136dp，锁定不阻止归位、不查询、不保存坐标。**「复制摘要」是详情 footer 的按钮（见上一条），不是菜单项**。
4. 圆圈**可拖动**（未锁定时；位置仅本会话有效、下次启动回右下初始位）；**失焦常驻**；`Esc` / `WM_CLOSE` 隐藏圆圈、程序继续驻留；
   详情关闭回到圆圈；退出释放全部资源。
5. 常驻“刷新”**或详情内 `Ctrl+R`** 可重新查询（single-flight，pending 中不排队）；等待期间标题、按钮、卡片、焦点和滚动保持。
   失败保留上次数据及原时间并给短提示；相同展示语义只更新文字，新数据一次提交。
6. 未登录 / 缺 CLI 显示安全错误说明，可按 `docs/setup.md` 准备后点击常驻“刷新”。
   应用不自动登录或安装。
7. 托盘**右键**菜单与圆圈右键按钮组一致（含**退出 ark_left**）。退出会终止活动查询并清理托盘图标。

## 状态与异常（失败不显示 0）

- **认证闸门**：仅当 `auth status` 退出码为 0 且 `logged_in=true` 才查询额度；
  `logged_in=false` 提示在终端执行 `arkcli auth login volc-sso`；
  缺字段 / 非布尔 → 格式错误；非零退出 / 启动失败 → 失败；超时 / 取消 → 对应提示。
- 未找到 CLI：提示安装 CLI，或设置 `ARK_LEFT_CLI` 指向 `arkcli.exe` 绝对路径。
- 额度查询非零退出**不会**被当作成功或伪装为 0。
- `percent` 为**已用**百分比，剩余 = `clamp(100 - percent, 0, 100)`；
  缺失 / 未知与真实 `0` 严格区分；`total=0` 且无有效 `percent` 时标"未知"。
- 条目 / 桶级错误使用固定安全文案，**不回显上游原始错误**（避免夹带 token）。

## 数据与隐私

- 复用本机 ArkCLI 登录身份，**不直接读取凭据文件、不展示、不持久化任何凭据字段**；
  原始 JSON 会短暂进内存用于提取业务字段，仅提取所需字段、不持久化。
- DPAPI CurrentUser 保护 `quota-cache.dat`，保存额度展示语义、时间与不可逆 scope 指纹；不保存原始身份或凭据。
- 明确未登录 / 确定新 scope / usage Mismatch 清除历史；普通失败保留历史及原时间。
- Unknown 不覆盖已有确认历史，不写缓存；无历史时可受限显示并提示身份未完全确认。
  首次运行标记仅含日期 / 版本，不含身份 / 额度 / 凭据。
- 子进程设置 `ARKCLI_NO_UPDATE_NOTIFIER=1` 防隐式更新；正常运行时不以 AI 身份调用。

## 环境变量

| 变量 | 作用 |
| --- | --- |
| `ARK_LEFT_CLI` | 覆盖 ArkCLI 可执行文件路径；**必须是存在的绝对 `.exe` 路径**（可选） |
| `ARK_LEFT_STATE_DIR` | 覆盖本地状态目录（marker 与 DPAPI 快照），用于测试 / 隔离（可选） |
| `ARK_LEFT_INSTANCE_SUFFIX` | 单实例名后缀，仅供测试隔离（正常为空）；普通用户无需设置 |

CLI 查找顺序：`ARK_LEFT_CLI` → PATH 中 `arkcli.exe` →
npm shim 目录下 `node_modules/@volcengine/ark-cli/bin/arkcli-windows-amd64.exe`。
**仅支持原生 `.exe`**；不通过 PowerShell 托管 `.ps1` shim（避免 shim 孙进程占用管道）。
两者都找不到时提示安装 CLI 或设置 `ARK_LEFT_CLI`。

## 文档维护

- 需求变化先更新 `docs/requirements/`（正式需求见 `product-requirements.md`）并提升版本，
  再同步受影响文档。
- 接口变化先更新 `docs/contracts.md`，并通知受影响代理确认版本。
- 未决问题登记到 `docs/decisions.md`，影响验收前不得标记完成。
- 每项改动在 `docs/tasks.md` 记录状态与证据，在 `docs/verification.md` 记录验收结果。

## 状态图例

- 未定义 / 待选 / 未确定：尚无结论，需用户或授权方输入。
- 草案（draft）：已有内容，但未确认为正式基线。
- pending：已登记，尚未开工。
- blocked_decision：受未决问题阻塞。
- done：有证据且影响验收的问题已关闭。
- 人工未测：逻辑 / 进程层已验证，但未做人工视觉 / 交互验证。

## 当前状态

- 应用代码：**已创建**（`src/`、`tests/`）
- 业务需求：`v0.1`（正式基线，Q-001 已确认关闭）
- 技术栈：**已定稿**（C# 5 + WinForms，csc 编译，无 SDK / NuGet）
- 当前交互：**`v0.14 UX022`（T052 主代理 2026-10-04 独立验收接受）**；v0.13 UX021 / v0.12 UX020 / v0.11 UX019 / v0.10 UX018 / v0.9 UX017 / v0.8 UX016 / v0.7 UX015 / v0.6 UX014 / v0.5 UX013 / v0.4 UX012 / v0.3 UX011 未被覆盖部分继续有效；v0.2（UX001–UX010）历史保留。
- 启动 / 测试命令：**已确定**（见上）；主代理 2026-10-04 在授权内**本地升级至 v0.14**（`start.cmd` → `launch.ps1 -OutputDir bin-v14`；`-PreviousProcessId 2268` → pid 10600）。
- 真实查询：历史已执行（脱敏输出，未落盘身份；主代理确认 `scope_verdict=Same`、`status=Ok`）。本轮 T048 未独立运行 `check.ps1`，未核验新真实账号 / 联网数据；启动存活不等于真实额度正确。
- 自动检查：主代理 2026-10-04 独立验收接受 T041 **538/0**、T042 **668/0**、T043 **732/0**、T044 **820/0**（均 smoke 0 / 8 项交互通过）、T045 launcher **20 项 ok**、T046 **906/0**（smoke 0、8 项交互、launcher 20 项 ok）、T047 首次 **959/0** 与返修后最终 **963/0**（smoke 0）、T050 **1030/0**（smoke 0、8 项交互 suffix 9d4a1d0b、launcher 20 项 suffix d3df298f）、T052 **1088/0**（首轮 1079/10 经 DS4.1 修复，smoke 0、8 项交互 suffix 03102674、launcher 20 项 suffix 9015f7c6）；见 `docs/verification.md`。历史 v0.2 的 213 / 245、v0.3 的 368、v0.4 的 518、v0.5 的 532 记录保留。
- 正式 `bin` 尚未更新（仍 v0.2）、历史候选 `bin-v04`–`bin-v13` 保留未覆盖；v0.14 候选 `bin-v14` 已构建并经 `start.cmd` → `launch.ps1 -OutputDir bin-v14` 完成授权内本地升级（T052 已接受；正式 `bin` 升级仍由主代理执行）。
- 未验证项：托盘视觉交互与真实手感、真实剪贴板 / 托盘 balloon、多屏 / 高 DPI 视觉定位、键盘读屏、其他套餐真人数据（T007 人工验收 `pending`，系统 Gate 未通过，如实标注）。
- 版本标识：`bin-v12\ark_left.exe` FileVersion / ProductVersion **0.12.0.0**（历史），SHA256 `C5114937C68C1FCCE4204C79426772DB80849A99D6B7C533896DB9C33767607E`；`bin-v12/t048-sha256.csv` 为 T048 生成时的**历史快照**（48 文件 manifest），后续任务文档变化**不刷新该旧 manifest**；旧提示「最终文档落盘后由主代理刷新」仅指 T048 当时时点，已保留为历史说明、不再作为当前承诺。
- v0.13 候选标识（历史）：`bin-v13\ark_left.exe` FileVersion / ProductVersion **0.13.0.0**，SHA256 `EE871EF245E6785305B2EDC7B604A5D7E3209864FE38A1EA2B10F6D74842ADE3`；`bin-v13\ark_left-tests.exe` SHA256 `52EABABCB2AE5B9203077DE432CF687FB9BADBDAC0EE20B2959275B67A02E8DA`（主代理 2026-10-04 在最后测试修订后实际重建并验收；曾本地升级 PID 15260）。
- v0.14 候选标识：`bin-v14\ark_left.exe` FileVersion / ProductVersion **0.14.0.0**，SHA256 `8484BB2BD97B3636090016BCA5188AA861AD6B6EE59ADE0F2465EDD5B2B77F36`；`bin-v14\ark_left-tests.exe` SHA256 `73D965F7838FDEC984523C281D7465A6E049F1227ECCBE6F42E7DF1B2A990D49`（主代理 2026-10-04 实际构建并独立验收；已本地升级 PID 10600）。
- `bin-v13/t051-sha256.csv` 已由主代理实际生成并校验：覆盖 **48 文件、0 mismatch**；清单记录**生成时**各文件 SHA256，后续变更需重新生成（主代理将在本次最后 doc 变更后 refresh 同 manifest，以捕获最终内容）。
