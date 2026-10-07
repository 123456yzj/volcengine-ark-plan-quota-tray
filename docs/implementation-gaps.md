# 实现差异与限制

对照基线为 [业务 v0.1](requirements/product-requirements.md) 与 [交互 v0.15 UX023](requirements/interaction-improvements.md)。本文只记录当前分支事实、差异和验收边界；使用方法见 [setup.md](setup.md)，Runtime 方案见 [managed-runtime.md](managed-runtime.md)。Managed Runtime、应用内登录、自动更新 / 回滚等均为**计划 / 待验收，尚未进入当前分支实现**。

## 分支实现与计划 / 待验收差异

| 项目 | 已接受基线 | 当前分支事实与计划 / 待验收事项 |
| --- | --- | --- |
| 查询范围 | 默认发现 Agent Plan / Coding Plan 订阅 | 当前 QuotaCli 调用 `usage plan --format json`；Coding Plan / Team 缺真实订阅验证证据。Runtime 计划须保留默认发现方式并验证正式范围覆盖 |
| CLI 与登录 | 使用本机 ArkCLI 已登录身份；提供登录指引 | 当前使用 `ARK_LEFT_CLI` → PATH / npm 原生 exe，提供复制登录命令；Managed Runtime、自带 bootstrap、应用内 SSO 登录 / 重登录 / 切换账号为计划 / 待验收，尚未进入当前分支实现 |
| 更新与分发 | 用户管理本机 ArkCLI | Runtime 自动更新 / 回滚、维护计时器、bootstrap 校验与打包均为计划 / 待验收，尚未进入当前分支实现 |
| 共享菜单 | 顶层“设置 / 退出”，设置内含悬浮内容、锁定、减少动画、归位及显隐 | 当前顶层为设置、隐藏 / 显示与退出；设置仅挂接悬浮内容，锁定 / 减少动画 / 归位处理器存在但未挂接菜单。应用内登录与 Runtime 诊断入口为计划 / 待验收，尚未进入当前分支实现 |

对应源码：[QuotaCli.cs](../src/Quota/QuotaCli.cs)、[TrayApp.cs](../src/App/TrayApp.cs)。这些差异不自动修改正式需求，也不将历史自动检查结果变成当前源码的通过证据。

## 已知边界

- Windows 可能将托盘图标收入折叠区；真实 Explorer 点击、失焦顺序、多屏 / DPI 视觉、键盘与读屏仍待人工验收。
- 普通查询每命令超时 30 秒，auth 与 usage 顺序执行，总耗时可能超过 60 秒；计划中的应用内 SSO 登录上限为 10 分钟，尚未进入当前分支实现。
- `used` / `total` 单位未证实，界面称“额度”；Coding Plan / Team 缺少真实订阅验证证据。
- 历史身份 A→B、未登录与 CLI 缺失主要由合成用例覆盖；计划中的 Runtime 需在实现后验证真实浏览器 SSO、自动更新 / 回滚、分发、ARM64、全新 Windows 环境及磁盘耗尽。
- smoke 的 DrawToBitmap 预览是合成窗口图，不是桌面截图或人工视觉验收。

未完成事项见 [tasks.md](tasks.md)，已接受证据与未测项见 [verification.md](verification.md)。
