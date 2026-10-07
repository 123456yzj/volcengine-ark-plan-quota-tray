# 当前任务状态

记录规则见 [AGENTS.md](../AGENTS.md)；已接受基线与未测边界见 [verification.md](verification.md)。

## 活跃任务

| 任务 | 状态 | 目标 | 下一步 | 完成条件 |
| --- | --- | --- | --- | --- |
| T-007 | pending | 真实托盘 / 多屏 / 高 DPI / 人工视觉与交互验证 | 在真实桌面完成 verification.md 所列人工检查 | 实际人工验证结果被明确记录并接受 |
| Runtime 范围与菜单差异 | pending | 核对未提交 Runtime 与业务 / 交互基线的差异 | 按 implementation-gaps.md 确认 Agent Plan 过滤、登录和菜单变化的目标，再修正实现或更新获确认的需求 | 业务范围与菜单行为有明确依据，相关文档与实现一致 |
| Runtime 验收 | pending | 验证未提交 Runtime 改动 | 对实际修订运行构建、相关单测与集成；补查登录、更新 / 回滚及分发路径，列出未测环境 | 实际检查结果被记录并接受，历史测试未用于替代本轮证据 |
