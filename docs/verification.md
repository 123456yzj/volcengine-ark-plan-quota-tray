# 验证与验收

## 已接受基线

业务基线为 **v0.1（R001–R004）**，交互基线为 **v0.15 UX023**。T057 于 2026-10-04 经独立验收后由主代理接受代码与自动检查：

| 检查 | 对应历史结果 |
| --- | --- |
| `build.ps1 -OutputDir bin-v15` | 构建成功 |
| 隔离 `bin-v15\ark_left-tests.exe` | 1160 passed / 0 failed |
| 隔离 `tests/verify-interaction.ps1` | 通过 |
| 入口隔离 `tests/verify-launcher.ps1` | 20 项通过，退出 0 |
| `git diff --check` | 通过 |

这些结果只对应当时版本。当前未提交 Managed Runtime 等源码改动的验收结果尚未登记，不能沿用历史结果证明通过。当前进程、部署版本和账号数据需现场核实。

## 保留的环境与契约证据

2026-10-03–04 的验证环境为 Windows、PowerShell 5.1、C# 5 / .NET Framework 4.8，使用 Framework64 `v4.0.30319\csc.exe`。

- `check.ps1` / `ark_left-check.exe` 的真实脱敏查询确认 `cli_found=true`、`status=Ok`，后续确认 `scope_verdict=Same`；验证 auth / usage schema 及 Agent Plan 数值口径。其他套餐无真实订阅验证证据。
- T027（UX011）：368 / 0，覆盖打开零查询、single-flight、DPAPI 与原子写、重启快照、scope 清留和展示稳定；Q-005 CLOSED。
- T031（UX012）：518 / 0，覆盖圆圈水位、未知不补 0、拖动阈值、内容选择、保存与双窗口布局；Q-006 CLOSED。
- T032（UX013）：532 / 0，包含生产 WinForms Timer 的 10 秒注入查询，验证可见 10 秒 / 隐藏 5 分钟、显隐不立即查询及相同间隔不重置计时。
- 历史隔离集成与本地启动升级证明对应旧实例正常退出、目标实例可唤起；启动存活不证明额度正确或真实桌面交互已验收。

测试数量为断言 / 检查项数量，不是命令调用次数。各版本的详细输出、返修和执行记录通过 Git 历史追溯；上述契约证据不是本轮重新执行的结果。

## 未验证项

- **T-007 pending，系统交付 Gate 未通过**：真实 Explorer 托盘点击、失焦、右键菜单、退出；多屏 / 高 DPI 视觉与定位；键盘、读屏、tooltip；真实剪贴板与 balloon；慢响应、取消及重试手感。
- 真实身份 A→B 切换、其他套餐真人数据、真实未登录 / CLI 缺失环境未构造；历史相关逻辑主要由合成用例覆盖。
- 当前 Runtime 的实际构建、专项单测、相关集成与分发检查尚需登记；真实浏览器 SSO、全新 Windows 虚拟机、ARM64 执行及磁盘耗尽需单独验收。测试用例存在不等于通过。

自动检查、反射快捷键派发和 DrawToBitmap 合成预览不能替代真人输入或桌面视觉验收。下一步与完成条件见 [tasks.md](tasks.md)，当前实现差异见 [implementation-gaps.md](implementation-gaps.md)。
