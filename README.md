# ark_left

Windows 系统托盘常驻的火山方舟订阅额度查询插件。右下角悬浮圆圈显示剩余百分比，左键打开详情，右键打开设置；详情展示周期、剩余量（若有）和重置时间。

Agent Plan 个人额度使用独立浏览器 SSO / PKCE 登录：浏览器授权后复制完整回调地址，在应用中粘贴并提交。以临时 STS 签名直接调用 GetAFPUsage，不依赖 ArkCLI 查询。refresh token 由 DPAPI CurrentUser 加密保存，STS 自动续期；5 小时、日、周、月窗口均保留。流程与验证见 [直连说明](docs/direct-agent-plan.md)。安装包不包含 ArkCLI，托盘不启动或自动下载组件运行时。

业务基线为 `v0.7`（R001–R005），应用版本为 `v0.23.0`，交互层为 `v0.20 UX028`。v0.23 移除随包 ArkCLI 运行时及组件诊断；v0.22 增加应用更新检测；v0.21.1 修复高 DPI 圆圈文字截断。账号操作位于“设置 → 登出 / 重新登录”；详情隐藏每日 AFP，首行显示订阅类型、更新时间和刷新按钮。T007 人工交互验收仍 pending。实际验证见 [验证摘要](docs/verification.md)。

## 构建与运行

安装包下载：[GitHub Releases](https://github.com/123456yzj/volcengine-ark-plan-quota-tray/releases/latest)。下载 `ark_left-<版本>-windows-setup.exe` 并运行；安装到当前用户目录，无需管理员权限，提供开始菜单入口、可选桌面快捷方式和卸载入口。要求 Windows 10 / 11 与 .NET Framework 4.8；首次使用在“设置 → 重新登录”中授权。安装与构建说明见 [设置指南](docs/setup.md)。

源码使用 C# 5 + WinForms，由系统 `csc.exe` 编译，无需 .NET SDK 或 NuGet。构建会校验并打包 `runtime-bootstrap/` 的官方 ArkCLI 二进制与第三方许可；个人额度直连不依赖这些组件。

在项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir D:/ark_left/bin-v23
```

双击 `start.cmd`，或运行 `bin-v23\ark_left.exe --show`。启动器按需构建，复用同版实例；替换项目旧实例时先验证目标，再请求正常退出。直接运行 exe 无参数时，首次显示圆圈，此后静默驻留；`--show` 强制显示圆圈。

维护者安装 Inno Setup 6 后执行 `powershell -NoProfile -ExecutionPolicy Bypass -File package.ps1`，在 `dist/` 生成安装包与 `SHA256SUMS.txt`。编译器不在默认位置时传 `-IsccPath`。构建无需 ArkCLI bootstrap；升级会删除旧安装目录中的两份随包 ArkCLI，保留用户登录与偏好。

## 验证命令

离线单测使用隔离状态目录，并在结束后恢复环境：

```powershell
$arkLeftPreviousState = $env:ARK_LEFT_STATE_DIR
try {
    $env:ARK_LEFT_STATE_DIR = 'D:/ark_left/bin-v23/local-test-state'
    & 'D:\ark_left\bin-v23\ark_left-tests.exe'
} finally {
    $env:ARK_LEFT_STATE_DIR = $arkLeftPreviousState
}
```

GUI 冒烟与隔离集成检查需桌面会话：

```powershell
bin-v23\ark_left.exe --smoke-test
powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-interaction.ps1 -OutputDir D:/ark_left/bin-v23
powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-launcher.ps1 -OutputDir D:/ark_left/bin-v23
```

冒烟使用合成数据并输出窗口自绘预览，验证布局、缓存展示和生命周期。集成脚本使用隔离状态与实例名，仅操作自己启动的进程。自动检查与合成预览不能替代人工视觉、托盘手感和多屏验收。

真实查询命令为 `powershell -NoProfile -ExecutionPolicy Bypass -File check.ps1`，直接调用 GetAFPUsage 并输出脱敏摘要。首次登录与真实续期检查使用构建目录下 `ark_left-check.exe --login --refresh-session`。

`build.ps1` 未传 `-OutputDir` 时使用 `bin`，需要指定候选目录时请显式传入参数；`test.ps1` 运行 `bin` 中的测试程序。

源码采用浅层职责目录：`src/App` 管理入口、托盘与 IPC，`src/UI` 放窗口、控件与布局，`src/Quota` 放额度模型、解析、查询与摘要，`src/State` 放身份、展示状态与持久化，`src/Runtime` 放 ArkCLI 运行组件。较大的窗口类通过同名 `partial` 文件拆分；命名空间保持 `ArkLeft`。

`tests/QuotaTests.cs` 保留统一测试入口与共享断言，用例按同样的职责放入测试子目录。`build.ps1` 递归收集 `src` 和 `tests` 下所有 `.cs` 文件，新增源码和用例无需逐个登记。根目录 `bin/`、`bin-*/` 中的 exe、图标、预览和日志均为生成产物，不纳入版本控制；`runtime-bootstrap/` 和第三方许可保留为构建输入。

## 使用

- 圆圈左键或 Enter / Space 打开详情，所有打开路径只展示已有状态。无缓存时显示“暂无数据”。
- 圆圈可拖动；锁定位置后仍可点击。位置只在本次运行有效；减少动画偏好跨启动记忆。
- 托盘与圆圈共享右键菜单：设置、显示 / 隐藏、关于 / 诊断、退出。“设置”内依次为悬浮内容、登出、重新登录；登出清除本项目会话与额度缓存，重新登录打开浏览器和手动回调窗口。
- 启动约 15 秒后后台检查 GitHub 最新正式版，运行期间每 24 小时检查一次；发现新版时同一版本每次运行只提醒一次。“关于 / 诊断”显示当前版本，支持手动检查与打开新版下载页面。自动检查失败静默处理，手动失败可重试；更新检查不发送方舟凭据，不自动安装。
- 仓库已于 2026-10-08 完成隐私清理并公开，Release 可匿名下载，应用更新接口已通过实际联网检查。隐私审查与安装包新校验值见 [隐私检查记录](docs/privacy-review.md)。
- 详情卡片右键或 `Ctrl+C` 复制当前额度摘要；`Ctrl+R` 手动刷新。等待期间保留展示、焦点和滚动，失败保留历史数据与原时间。
- 启动后台查询一次；圆圈或详情可见时每 10 秒轮询，全隐藏时每 5 分钟轮询。同一时间只运行一次查询。
- 关闭或 Esc 隐藏窗口，应用继续驻留；退出释放资源。Windows 可能将托盘图标收进折叠区。

## 数据与隐私

`percent` 表示已用百分比，剩余为 `clamp(100 - percent, 0, 100)`。未知 / 缺失与真实 0 区分，失败不显示为 0；上游错误使用固定安全提示。

应用仅保存 DPAPI CurrentUser 加密的 refresh token 与随机会话绑定；STS、完整授权码及原始身份不落盘或输出。额度快照由 DPAPI CurrentUser 保护，保存展示语义、时间和不可逆 scope 指纹。明确未登录、新 scope 或身份冲突时清除历史；普通失败保留历史。

## 环境变量

| 变量 | 作用 |
| --- | --- |
| `ARK_LEFT_CLI` | 开发 / 诊断覆盖，须为存在的绝对 `.exe` 路径 |
| `ARK_LEFT_STATE_DIR` | 隔离状态目录，包含 marker 与额度快照 |
| `ARK_LEFT_RUNTIME_DIR` | 计划 / 待验收，尚未进入当前分支实现：拟覆盖 Runtime 目录，默认 `%LOCALAPPDATA%\ArkLeft` |
| `ARK_LEFT_INSTANCE_SUFFIX` | 测试用单实例名后缀，正常使用为空 |

`ARK_LEFT_CLI` 和 Runtime 配置只影响保留的 ArkCLI 能力，不参与 Agent Plan 个人版认证和额度查询。直接查询使用固定北京区域，状态目录同时包含加密 refresh token、额度快照及展示偏好。

## 文档导航与维护

| 路径 | 用途 |
| --- | --- |
| [AGENTS.md](AGENTS.md) | 项目工作流规则 |
| [文档索引](docs/README.md) | 按需读取入口 |
| [业务需求](docs/requirements/product-requirements.md) / [交互需求](docs/requirements/interaction-improvements.md) | 当前有效基线 |
| [差异与限制](docs/implementation-gaps.md) | 当前实现差异与验收边界 |
| [契约](docs/contracts.md) | 数据、缓存与接口语义 |
| [设置指南](docs/setup.md) / [Managed Runtime](docs/managed-runtime.md) | 本机使用与运行组件 |
| [任务](docs/tasks.md) | 活跃、阻塞与待长期验证事项 |
| [决定](docs/decisions.md) / [验证](docs/verification.md) | 长期决定与交付证据摘要 |

工作流与文档记录规则见 [AGENTS.md](AGENTS.md)。
