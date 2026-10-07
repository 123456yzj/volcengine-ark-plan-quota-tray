# 本地使用指南

本指南以当前分支说明运行方法。已接受交互基线为 v0.15；Managed Runtime、应用内登录、自动更新 / 回滚等均为**计划 / 待验收，尚未进入当前分支实现**，差异见 [implementation-gaps.md](implementation-gaps.md)。

## 启动

需要 Windows 与 .NET Framework 4.8。双击项目根目录 `start.cmd`，或运行构建目录的 `ark_left.exe --show`。首次运行显示悬浮圆圈，之后无参数启动静默驻留；重复启动唤起已有实例。

当前分支需要本机已安装原生 ArkCLI，并能通过 PATH 找到它，或设置 `ARK_LEFT_CLI`。自带 bootstrap、第三方许可分发、用户 Runtime 目录导入以及无需 Node、npm 或 PATH 的目标均为计划 / 待验收，尚未进入当前分支实现。构建、更新与回滚方案见 [managed-runtime.md](managed-runtime.md)。

Windows 可能将托盘图标放入隐藏图标区；可展开任务栏角落箭头，将图标拖到可见区域，或在任务栏设置中启用显示。

## 登录与组件诊断

当前分支界面提供“复制登录命令”，用户在终端执行 `arkcli auth login volc-sso` 完成登录。应用内“登录方舟 / 重新登录 / 切换账号”、浏览器 SSO 与取消登录为计划 / 待验收，尚未进入当前分支实现。应用不直接读取凭据文件。

计划的应用内登录将暂停新查询、取消旧查询并清旧展示及缓存；成功后重新执行 `auth status --format json`，确认 `logged_in=true` 和身份 scope，再查询额度。该流程为计划 / 待验收，尚未进入当前分支实现。

若提示未找到本机 ArkCLI，检查安装和 PATH，或设置 `ARK_LEFT_CLI`。当前分支的解析顺序为 `ARK_LEFT_CLI` → PATH / npm 原生 exe，仅支持原生 `.exe`。Managed Runtime、“关于 / 诊断”及更新检查为计划 / 待验收，尚未进入当前分支实现。

开发或诊断可在 PowerShell 中设置当前会话覆盖，然后从同一会话启动：

```powershell
$env:ARK_LEFT_CLI = 'C:\path\to\arkcli.exe'
.\bin-v15\ark_left.exe --show
```

覆盖必须是存在的绝对 `.exe` 路径；已有实例须先正常退出才能使用新的环境。无效覆盖会明确失败。

## 查看与刷新

- 托盘左键切换圆圈；圆圈左键或 Enter / Space 打开全部额度详情。打开与重复唤起只展示已有状态，不查询；无历史时显示“暂无数据”。
- 圆圈显示选定套餐 / 周期的剩余百分比，拖动位置只在本次运行有效，不保存坐标。选择目标缺失时显示“暂无数据”，保留选择。
- 当前分支共享菜单含设置、隐藏 / 显示与退出；应用内登录与 Runtime 诊断入口为计划 / 待验收，尚未进入当前分支实现。“设置 → 悬浮内容”直接保存选择并按已有数据生效；写失败保留旧选择。锁定位置、减少动画和归位菜单项未挂接，详见差异记录。
- 详情卡片右键菜单为刷新、复制摘要、设置、关闭。“设置”打开选择弹窗，取消、Esc 或 Alt+F4 不保存；保存失败保持弹窗打开。
- 详情含焦点且设置 / 菜单未打开时，精确 Ctrl+R 手动刷新，Ctrl+C 复制摘要。摘要只包含展示额度和时间，不含原始身份或上游错误文本。
- 启动后台查询一次；圆圈或详情可见时每 10 秒轮询，全隐藏时每 5 分钟轮询。仅设置弹窗可见不提速，显隐变化不立即查询；在途重复刷新不排队。
- 等待结果期间保持界面、焦点和滚动。普通失败保留历史与原查询时间；明确未登录或身份变化清旧数据。关闭、Esc 或失焦隐藏详情，应用继续驻留；“退出 ark_left”结束应用。

`percent` 表示已用百分比，剩余为 `clamp(100 - percent, 0, 100)`。未知与真实 0 区分；`used` / `total` 单位未证实，统一称“额度”。当前分支查询使用 `usage plan --format json`，默认发现订阅；正式范围为 Agent Plan / Coding Plan，其他套餐仍缺真实订阅验证证据。Runtime 计划须遵守该范围。

## 本地文件与隐私

应用状态默认位于 `%LOCALAPPDATA%\ark_left`，可用 `ARK_LEFT_STATE_DIR` 隔离：

| 文件 | 内容 |
| --- | --- |
| `first-run.done` | 版本 / 日期标记；删除后下次启动再次显示圆圈，不改变登录状态 |
| `quota-cache.dat` | DPAPI CurrentUser 加密历史快照与不可逆 scope 指纹；仅 Same 的成功类结果保存 |
| `floating-settings.json` | 格式 1，明文产品 / 周期标识，无身份、额度或坐标 |
| `floating-preferences.json` | 格式 2，位置锁定 / 减少动画两个布尔，严格格式 1 可只读迁移 |

不保存凭据或原始身份。普通失败保历史；Unknown 不覆盖已有确认历史；明确未登录、新 scope 或 Mismatch 清历史。损坏快照按无缓存处理。

Runtime 文件目录、`ARK_LEFT_RUNTIME_DIR`、应用内登录、GitHub Release 后台更新及 Runtime 日志均为计划 / 待验收，尚未进入当前分支实现，方案见 [managed-runtime.md](managed-runtime.md)。当前分支通过本机 ArkCLI 子进程查询登录状态与额度，登录由用户在终端完成。业务状态格式规则见 [contracts.md](contracts.md)。
