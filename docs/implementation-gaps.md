# 实现差异与限制

对照基线为 [业务 v0.1](requirements/product-requirements.md) 与 [交互 v0.15 UX023](requirements/interaction-improvements.md)。本文只记录差异和验收边界；使用方法见 [setup.md](setup.md)，完整 Runtime 行为见 [managed-runtime.md](managed-runtime.md)。

## 未提交 Runtime 的差异

| 项目 | 已接受基线 | 当前源码事实与待解决事项 |
| --- | --- | --- |
| 查询范围 | 默认发现 Agent Plan / Coding Plan 订阅 | QuotaCli 显式调用 `usage plan --product agent-plan --format json`；不能证明覆盖 Coding Plan，需核对实现意图与业务范围 |
| CLI 与登录 | 使用本机 ArkCLI 已登录身份；提供登录指引 | 新增自带 Runtime、用户触发 SSO 登录 / 重登录 / 切换账号、取消登录、更新与诊断；新增接口需单独验收 |
| 共享菜单 | 顶层“设置 / 退出”，设置内含悬浮内容、锁定、减少动画、归位及显隐 | TrayApp 当前顶层包含登录、设置、隐藏、诊断与退出；设置仅挂接悬浮内容，锁定 / 减少动画 / 归位处理器存在但未挂接菜单 |

对应源码：[QuotaCli.cs](../src/QuotaCli.cs)、[TrayApp.cs](../src/TrayApp.cs)。这些差异不自动修改正式需求，也不将历史自动检查结果变成当前源码的通过证据。

## 已知边界

- Windows 可能将托盘图标收入折叠区；真实 Explorer 点击、失焦顺序、多屏 / DPI 视觉、键盘与读屏仍待人工验收。
- 普通查询每命令超时 30 秒，auth 与 usage 顺序执行，总耗时可能超过 60 秒；当前 SSO 登录上限 10 分钟。
- `used` / `total` 单位未证实，界面称“额度”；Coding Plan / Team 缺少真实订阅验证证据。
- 历史身份 A→B、未登录与 CLI 缺失主要由合成用例覆盖；当前真实浏览器 SSO、ARM64、全新 Windows 环境及磁盘耗尽仍需验证。
- smoke 的 DrawToBitmap 预览是合成窗口图，不是桌面截图或人工视觉验收。

未完成事项见 [tasks.md](tasks.md)，已接受证据与未测项见 [verification.md](verification.md)。
