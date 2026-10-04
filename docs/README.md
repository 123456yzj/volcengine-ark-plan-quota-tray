# 文档规范与索引

本目录只保存对当前开发仍有直接价值的长期信息。工作流默认采用**最小活动上下文**；历史流程和已完成任务优先通过 Git 历史追溯，而不是让新 Agent 重读长篇 markdown。

项目当前工作规则见 [`../AGENTS.md`](../AGENTS.md)，当前工作流见 [`development-plan.md`](development-plan.md)。

## 1. 默认读取集

普通开发任务默认只读取：
1. `../AGENTS.md`；
2. 与当前请求直接相关的 requirement / contract 片段；
3. 当前代码与测试；
4. 只有存在复杂活动任务时才读 `tasks.md`。

以下文件默认不整份读取：`verification.md`、`decisions.md`、历史需求版本、完整任务历史。
出现具体疑点时先搜索定位，再局部读取。

## 2. 文档职责

| 文档 | 用途 | 默认读取 |
| --- | --- | --- |
| [`requirements/README.md`](requirements/README.md) | 需求索引 | 按需 |
| [`requirements/product-requirements.md`](requirements/product-requirements.md) | 正式业务需求 v0.1（R001–R004） | 业务相关时局部读取 |
| [`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) | 当前交互层；现为 v0.15 UX023 | 交互相关时局部读取 |
| [`requirements/implemented-behavior.md`](requirements/implemented-behavior.md) | 当前实现事实 | 出现实现/需求差异时 |
| [`contracts.md`](contracts.md) | 数据与接口契约 | 契约相关时局部读取 |
| [`tasks.md`](tasks.md) | 仅活跃/阻塞/待人工验证任务 | 复杂活动任务时 |
| [`decisions.md`](decisions.md) | 有长期影响的重要决定 | 有具体决策疑点时 |
| [`verification.md`](verification.md) | 有交付价值的验证证据 | 验收/发布时按需 |
| [`setup.md`](setup.md) | 本机运行说明 | 运行/部署相关时 |
| [`development-plan.md`](development-plan.md) | 当前 Fast/Complex Path 工作流 | 需要调度规则时 |

## 3. 当前权威版本

- 正式业务需求：`v0.1`（R001–R004）。
- 当前交互层：`v0.15 UX023`；T057 的代码与自动检查已接受。
- 旧交互版本只作为历史链，不应为了普通开发默认全部读取。
- 数据/接口契约以 `contracts.md` 当前有效条目为准。

## 4. 需求与契约

需求或契约发生**实质变化**时才更新长期文档。

普通实现修复、样式微调、局部重构如果不改变长期语义，不为了留下流水而更新 requirements / contracts / decisions。

关键业务或契约歧义未解决时，可使用 `BLOCKED_DECISION`；已有明确依据的问题直接执行。

## 5. 任务记录

`tasks.md` 不再承担完整开发日志。

- 普通 Fast Path 小改动不创建 T-XXX；
- 复杂、跨会话、阻塞或需要长期人工验证的工作才进入 tasks；
- 完成后的详细过程依赖 Git 历史追溯；
- 不在 tasks 中长期堆叠 session ID、模型切换、重复测试输出和旧流程实验。

## 6. 验证证据

只有有交付价值的验证需要长期记录，例如：
- 构建/发布候选；
- 重要集成或端到端结果；
- 真实账号/环境验证；
- 会影响系统交付 Gate 的未测项。

普通小改动只需在当前执行结果中说明实际跑过的针对性检查，不强制同步 `verification.md`。

任何情况下：
- 未执行的检查必须标为未测；
- 自动检查不得冒充人工验收；
- 发现疑点时只补查相关证据，不重复读取全部验证历史。

## 7. 指令作用域

用户一次性指令默认只属于当前任务。只有用户明确要求长期化、项目统一或写入规范时，才更新项目规则。

AI 不得因为一次执行偏好自动修改：
- `AGENTS.md`
- requirements
- contracts
- development-plan
- decisions

## 8. 系统交付 Gate

系统交付仍要求：
- 必要需求已覆盖；
- 风险匹配的集成/端到端验证完成；
- 关键问题关闭；
- 未验证项被显式列出并被有权者接受。

Fast Path 简化的是**开发调度成本**，不是降低产品交付标准。
