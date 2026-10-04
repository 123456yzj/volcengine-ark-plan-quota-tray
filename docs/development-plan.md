# 当前开发工作流

本文件只描述当前生效的工作流。默认采用 **Fast-First Model Routing**：高 TPS 模型负责绝大多数开发执行，强但低 TPS 的模型只在复杂决策和高风险场景短暂介入。

## 1. 设计目标

工作流优先优化端到端开发效率，而不是让最强模型参与每一步。

核心目标：
- 普通需求尽量在一个高 TPS 执行会话内完成；
- 慢模型只做它真正有优势的推理、诊断和审查；
- 子代理只有在收益高于 handoff tax 时使用；
- 子代理启动后直接实施，不重新建立完整项目认知；
- 控制默认读取量，不让历史 md 成为启动成本；
- 发现新问题不自动扩展成任务链；
- 一次性用户指令不自动沉淀为长期规范。

## 2. 默认路径：Fast Executor

普通任务：

```text
理解请求
→ Locate
→ Edit
→ Verify
→ Done
```

适用条件：
- 目标清楚；
- 修改局部；
- 不改变业务需求或接口契约；
- 可以通过有限代码定位和针对性验证完成。

默认由高 TPS 模型完成定位、读取、编辑、测试和总结。

Fast Path 不要求 Strong Reasoner、子代理、T-XXX、独立验收 Agent、完整任务 brief、上下文预算表或完整 docs 读取。

## 3. Complexity Gate

出现以下情况才考虑升级：
- 关键需求/契约歧义；
- 架构或跨模块方案取舍；
- 高风险修改需要独立推理；
- 同一问题连续两次有依据的修复仍失败；
- 根因无法可靠判断；
- 当前上下文出现明显推理退化；
- 有真正可并行的独立工作。

升级前必须比较：

```text
升级带来的推理 / 隔离 / 并行 / 审查收益
>
模型切换 + 新上下文 + 重复读取 + handoff + 回传成本
```

不满足时继续由 Fast Executor 完成。

## 4. Strong Reasoner

Strong Reasoner 是短时顾问，不是默认执行主循环。

输入使用最小 Decision Packet：
- 一个明确的问题；
- 当前目标 / 验收标准；
- 必要代码或契约片段；
- 已知事实；
- 已尝试方案与失败结果（如有）。

输出只需要：
- 推荐结论；
- 核心依据；
- 必须遵守的约束；
- 风险；
- 下一步执行建议。

结论返回后立即回到 Fast Executor。

## 5. 子代理派发流程

真正需要实施子代理时，工作流必须先经过 **Dispatch Preparation**：

```text
Main Agent
→ Locate
→ Freeze Goal
→ Freeze Edit Points
→ Freeze Known Dependencies
→ Freeze Acceptance
→ Freeze Non-goals
→ Dispatch Sub Agent
```

### 最小任务包

```text
Goal:
<一个可独立判断成败的结果>

Edit Points:
- path::Class::Method
- path::Symbol

Known Dependencies:
- <已经确认、与实现直接相关的事实>

Acceptance:
- <行为标准>
- <必跑检查>

Non-goals:
- <明确不做的事项>
```

主代理负责“在哪里改、哪些事实成立、怎样算完成”；子代理负责“具体怎么实现”。

禁止只给模块名或开放式描述后让子代理自行重建项目上下文。

## 6. 子代理执行路径

子代理收到任务后：

```text
Read Edit Points
→ Implement
→ Targeted Verify
→ Return
```

只有遇到明确上下文缺口时才能增加读取。

禁止默认：
- 重扫仓库；
- 整份读取 docs；
- 阅读历史任务/验证流水；
- 自行修改需求或契约；
- 因发现额外问题扩大任务。

## 7. Gap Resolver

如果子代理缺少一个具体事实：

```text
Executor
→ Gap Question
→ Main Agent
→ 必要时调用 explore
→ Conclusion / Source Symbols / Required Facts
→ Executor Continue
```

`explore` 只解决一个明确问题。

允许：
```text
BuildContextMenu 的显隐状态由哪个字段维护？
```

不允许：
```text
帮我分析整个菜单系统。
```

Gap 解决后立即结束检索，不继续扩展背景。

## 8. BOUNDARY_HIT

子代理遇到以下情况必须停止扩展：

- 必须修改未授权文件/符号；
- Known Dependencies 与代码事实冲突；
- 必须改变需求、契约或验收标准；
- 在当前边界内无法可靠满足 Acceptance；
- 新问题不影响当前验收，但继续会扩大范围。

返回：

```text
BOUNDARY_HIT

Reason:
Related:
Confirmed:
Decision Needed:
```

然后由主代理重新判断：扩展原任务、另建后续项、进入 BLOCKED_DECISION，或忽略无关 follow-up。

`BOUNDARY_HIT` 自身不创建新任务，也不允许子代理自行派发下一级 Agent。

## 9. 子代理结果协议

正常完成只返回：

```text
Changed:
- <file::symbol>

Verified:
- <command/check → result>

Not Verified:
- <未测项>

Boundary:
- none / <范围外发现的简短说明>
```

不返回长篇执行流水，不重复整个需求，不粘贴完整搜索过程。

## 10. 主代理验收

默认：

```text
Sub Agent
→ Self Test
→ Compact Result
→ Main Agent 检查关键 diff + 验证结果
→ Accept / Rework
```

实施子代理说“测试通过”不等于任务自动完成。

只有高风险修改才考虑额外 Reviewer；不要把“Executor → Reviewer → Main Agent”作为普通任务固定流水线。

## 11. 上下文读取策略

读取顺序：
1. 搜索定位；
2. 当前代码窗口；
3. 需要业务语义时读取对应 requirement / contract 小节；
4. 有具体疑点才查 decisions / verification / Git 历史；
5. 一个明确 Gap 仍无法解决时才考虑只读 explore。

默认活动上下文：
- `AGENTS.md`
- 当前需求/契约相关片段
- 当前代码与测试
- 必要时的 `docs/tasks.md` 当前状态

Historical Context 默认不进入上下文。

## 12. 问题发现策略

| 分类 | 处理 |
| --- | --- |
| 当前验收必须解决 | 当前任务内修 |
| 相关但不影响当前验收 | follow-up，默认不执行 |
| 关键业务/契约/安全问题 | 暂停受影响范围，重新定界或 BLOCKED_DECISION |

发现问题不能直接触发新 Agent、新任务或 Strong Reasoner。

## 13. 会话与模型路由

优先级：
1. Fast Executor 直接完成；
2. 必要时向 Strong Reasoner 请求一次有界决策；
3. 真正需要上下文隔离、并行或独立验证时才新开子代理。

同一局部需求的定位、修改、验证保持在当前执行会话。
如果运行环境不支持明确选择模型，不为模拟分层制造额外 session。

## 14. 指令与规范

用户指令默认 = 当前任务作用域。

只有用户明确表达长期化意图，才修改项目规范。临时的模型、代理、文件、验证或执行偏好不自动写入长期规则。

## 15. 验证策略

- 低风险：目标测试 / 局部编译 / 直接相关检查；
- 中风险：相关单测 + 构建/静态检查；
- 高风险：集成/端到端；必要时 Strong Reasoner Review 或独立验收。

强模型 Review 是高风险工具，不是固定流水线阶段。

## 16. 观察指标

验证子代理优化是否有效时优先观察：
- 子代理首次 Edit 前工具调用；
- 子代理首次 Edit 前读取量；
- 是否命中 Edit Points 后直接实施；
- Gap Resolver 调用次数；
- BOUNDARY_HIT 次数及是否阻止 Scope Drift；
- 子代理总工具调用；
- 新 Session 数量；
- 一次验收通过率；
- Scope Drift。

目标是让子代理表现为：

```text
少找 → 少读 → 精准实现 → 快速验证 → 快退出
```

而不是第二个主代理。

## 17. 文档与收口

`tasks.md` 只跟踪活跃、阻塞或需长期验证的工作；已完成过程依赖 Git 历史。

任务结束只汇报：
- 改了什么；
- 实际验证了什么；
- 哪些仍未测；
- 是否存在不影响当前验收的 follow-up。

不要因为存在 follow-up 自动继续下一任务。
