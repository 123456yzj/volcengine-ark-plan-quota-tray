# 实现差异与限制

对照基线为 [业务 v0.5](requirements/product-requirements.md) 与 [额度交互 v0.18 UX026](requirements/interaction-improvements.md)。Agent Plan 个人版在 v0.16 切换为独立浏览器登录和 GetAFPUsage 直连，v0.17 改为手动提交完整回调 URL，v0.19 左侧详情隐藏每日 AFP，并在首行显示更新时间和刷新按钮；v0.20 移除悬浮圆圈 tooltip，v0.21 将登出与重新登录放入设置子菜单，见 [直连说明](direct-agent-plan.md)。其他历史差异与 Runtime 方案不在本次扩展或验收范围。

## 分支实现与计划 / 待验收差异

| 项目 | 已接受基线 | 当前分支事实与计划 / 待验收事项 |
| --- | --- | --- |
| 查询范围 | Agent Plan / Coding Plan 订阅；v0.2 切换个人 Agent Plan 查询实现 | 生产个人额度调用 GetAFPUsage，保留 5h/daily/weekly/monthly。其他兼容模型与 ArkCLI 能力保留，Coding Plan / Team 的真实订阅证据不由本次补齐 |
| CLI 与登录 | v0.3：个人 Agent Plan 独立浏览器 SSO、手动回调提交 | PKCE S256、手动回调窗口、URL / state 校验、内存 STS、DPAPI refresh token 保存与自动续期已实现；额度不依赖 CLI 登录。真人粘贴回调与真实跨账号切换仍待人工验收 |
| 更新与分发 | 用户管理本机 ArkCLI | Runtime 自动更新 / 回滚、维护计时器、bootstrap 校验与打包均为计划 / 待验收，尚未进入当前分支实现 |
| 共享菜单 | 顶层设置、显隐、关于 / 诊断、退出；设置内含悬浮内容、登出、重新登录 | v0.21 按 UX026 将账号操作收进设置；登出清会话和缓存，重新登录接直连 SSO |

对应源码：[QuotaCli.cs](../src/Quota/QuotaCli.cs)、[TrayApp.cs](../src/App/TrayApp.cs)。这些差异不自动修改正式需求，也不将历史自动检查结果变成当前源码的通过证据。

## 已知边界

- Windows 可能将托盘图标收入折叠区；真实 Explorer 点击、失焦顺序、多屏 / DPI 视觉、键盘与读屏仍待人工验收。
- 直连每网络请求限时 30 秒；浏览器回调上限 10 分钟，可取消；刷新失败会重新拉起网页登录。正常查询可先包含一次 token 刷新。
- `used` / `total` 单位未证实，界面称“额度”；Coding Plan / Team 缺少真实订阅验证证据。
- 历史身份 A→B、未登录与 CLI 缺失主要由合成用例覆盖；计划中的 Runtime 需在实现后验证真实浏览器 SSO、自动更新 / 回滚、分发、ARM64、全新 Windows 环境及磁盘耗尽。
- smoke 的 DrawToBitmap 预览是合成窗口图，不是桌面截图或人工视觉验收。

未完成事项见 [tasks.md](tasks.md)，已接受证据与未测项见 [verification.md](verification.md)。
