# ark_left

Windows 系统托盘常驻的火山方舟订阅额度查询插件。右下角悬浮圆圈显示剩余百分比，左键打开详情，右键打开设置；详情展示周期、剩余量（若有）和重置时间。

分支已提交源码调用本机原生 ArkCLI，复用用户现有登录状态，不另存凭据；需要用户先安装 ArkCLI 并在终端登录。Managed Runtime、自带 bootstrap、应用内登录 / 账号切换、自动更新与回滚均为**计划 / 待验收**，工作区候选代码不代表当前已交付能力，状态与设计见 [Managed Runtime](docs/managed-runtime.md)。

已接受业务基线为 `v0.1`（R001–R004），交互层为 `v0.15 UX023`。该基线的自动检查已接受，T007 真实托盘、多屏 / DPI 和人工交互验收仍 pending。当前未提交的 Runtime 改动需单独验证；历史结果见 [验证摘要](docs/verification.md)，不能用于证明当前工作区已通过验收。

## 构建与运行

要求 Windows、.NET Framework 4.8。源码使用 C# 5 + WinForms，由系统 `csc.exe` 编译，无需 .NET SDK 或 NuGet。分支已提交构建不打包 Runtime；候选发布包包含 `runtime-bootstrap/` 和第三方许可的方案待验收。

在项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir D:/ark_left/bin-v15
```

双击 `start.cmd`，或运行 `bin-v15\ark_left.exe --show`。启动器按需构建，复用同版实例；替换项目旧实例时先验证目标，再请求正常退出。直接运行 exe 无参数时，首次显示圆圈，此后静默驻留；`--show` 强制显示圆圈。

候选 Runtime 发布包的构建与分发计划见 [Managed Runtime](docs/managed-runtime.md)。未提交的 `build.ps1` 已增加 bootstrap 校验与打包步骤，尚待验证；当前运行进程和部署版本以实际检查为准。

## 验证命令

离线单测使用隔离状态目录，并在结束后恢复环境：

```powershell
$arkLeftPreviousState = $env:ARK_LEFT_STATE_DIR
try {
    $env:ARK_LEFT_STATE_DIR = 'D:/ark_left/bin-v15/local-test-state'
    & 'D:\ark_left\bin-v15\ark_left-tests.exe'
} finally {
    $env:ARK_LEFT_STATE_DIR = $arkLeftPreviousState
}
```

GUI 冒烟与隔离集成检查需桌面会话：

```powershell
bin-v15\ark_left.exe --smoke-test
powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-interaction.ps1 -OutputDir D:/ark_left/bin-v15
powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify-launcher.ps1 -OutputDir D:/ark_left/bin-v15
```

冒烟使用合成数据并输出窗口自绘预览，验证布局、缓存展示和生命周期。集成脚本使用隔离状态与实例名，仅操作自己启动的进程。自动检查与合成预览不能替代人工视觉、托盘手感和多屏验收。

候选 Runtime 专项测试入口为 `bin-v15\ark_left-tests.exe --runtime`，不属于分支已提交测试入口，执行结果待验收。真实查询命令为 `powershell -NoProfile -ExecutionPolicy Bypass -File check.ps1`，会调用 ArkCLI 并输出脱敏摘要。

`build.ps1` / `test.ps1` 未传 `-OutputDir` 时使用 `bin`，需要指定候选目录时请显式传入参数。

## 使用

- 圆圈左键或 Enter / Space 打开详情，所有打开路径只展示已有状态。无缓存时显示“暂无数据”。
- 圆圈可拖动；锁定位置后仍可点击。位置只在本次运行有效；减少动画偏好跨启动记忆。
- 分支已提交右键菜单提供设置（悬浮内容）、显示 / 隐藏和退出；应用内登录与诊断为计划 / 待验收。锁定、减少动画与归位入口的基线差异见 [差异与限制](docs/implementation-gaps.md)。
- 详情卡片右键或 `Ctrl+C` 复制当前额度摘要；`Ctrl+R` 手动刷新。等待期间保留展示、焦点和滚动，失败保留历史数据与原时间。
- 启动后台查询一次；圆圈或详情可见时每 10 秒轮询，全隐藏时每 5 分钟轮询。同一时间只运行一次查询。
- 关闭或 Esc 隐藏窗口，应用继续驻留；退出释放资源。Windows 可能将托盘图标收进折叠区。

## 数据与隐私

`percent` 表示已用百分比，剩余为 `clamp(100 - percent, 0, 100)`。未知 / 缺失与真实 0 区分，失败不显示为 0；上游错误使用固定安全提示。

应用不直接读取凭据文件，也不展示或持久化原始身份。额度快照由 DPAPI CurrentUser 保护，保存展示语义、时间和不可逆 scope 指纹。明确未登录、新 scope 或身份冲突时清除历史；普通失败保留历史；身份未知时不覆盖已确认缓存。

## 环境变量

| 变量 | 作用 |
| --- | --- |
| `ARK_LEFT_CLI` | 开发 / 诊断覆盖，须为存在的绝对 `.exe` 路径 |
| `ARK_LEFT_STATE_DIR` | 隔离状态目录，包含 marker 与额度快照 |
| `ARK_LEFT_RUNTIME_DIR` | 计划 / 待验收：候选 Runtime 目录，默认 `%LOCALAPPDATA%\ArkLeft`；分支已提交源码不使用 |
| `ARK_LEFT_INSTANCE_SUFFIX` | 测试用单实例名后缀，正常使用为空 |

分支已提交 CLI 解析顺序为覆盖路径 → PATH / npm 原生 exe，查询使用 `usage plan --format json`。仅支持原生 `.exe`，不通过 PowerShell 托管 shim。插入 Managed Runtime 与显式 Agent Plan 过滤为候选代码变化，计划 / 待验收；Coding Plan 覆盖差异见 [差异与限制](docs/implementation-gaps.md)。

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
