# 需求基线入口（已迁移）

> **本文件是兼容入口，不再承载需求正文。**
> 唯一权威需求基线已迁移到 [`docs/requirements/`](requirements/)（目录索引见
> [`requirements/README.md`](requirements/README.md)）。
> 保留本文件是为了让既有链接 / 引用继续可解析，避免两套正文矛盾。

## 权威位置

| 内容 | 位置 |
| --- | --- |
| 正式需求（R001–R004、规则、异常、验收标准、源码映射；顶部含交互层覆盖声明） | [`requirements/product-requirements.md`](requirements/product-requirements.md) |
| 交互改进：**v0.13（UX021，当前权威，T050 主代理 2026-10-04 已独立验收接受）** + v0.12–v0.3（仅未被覆盖部分继续有效）+ v0.2（UX001–UX010，历史保留 / 已实现） | [`requirements/interaction-improvements.md`](requirements/interaction-improvements.md) |
| 已实现行为（实际运行逻辑、边界、数据流、人工验收清单） | [`requirements/implemented-behavior.md`](requirements/implemented-behavior.md) |
| 需求目录索引与权威声明 | [`requirements/README.md`](requirements/README.md) |
| 本地设置指南（安装 / 登录 / `ARK_LEFT_CLI` / 托盘折叠） | [`setup.md`](setup.md) |

## 基线与编号（原样保留）

- 需求版本：**v0.1（正式基线，数据范围不变）**。
- 需求编号：**R001–R004**，含义与验收标准不变；其**交互部分**（打开即查询 / 无轮询 /
  无本地持久化）已被交互层 **v0.3（UX011）覆盖**（当前整体交互权威为 **v0.13 UX021**），详见
  [`requirements/interaction-improvements.md`](requirements/interaction-improvements.md)。
- 交互层版本：**v0.13（UX021，当前权威，T050 主代理 2026-10-04 已独立验收接受）**；新增详情快捷键 `Ctrl+R` 刷新 / `Ctrl+C` 复制摘要。
  v0.12（UX020）悬浮窗归位（当前圆圈所在屏工作区右下，未定位取 primary）、
  v0.11（UX019）复制摘要 – v0.3（UX011）未被覆盖的交互规则**继续有效**；
  v0.2（UX001–UX010）历史保留。
- 本兼容入口本次仅同步版本与索引事实；UX021 不改业务数据范围，需求正文与既有规则留在权威需求文件中维护。

## 相关文档

- 数据契约与技术栈：[`contracts.md`](contracts.md)
- 验证与验收证据：[`verification.md`](verification.md)（历史记录保留）
- 任务与状态：[`tasks.md`](tasks.md)
- 问题与决定：[`decisions.md`](decisions.md)
- 项目说明：[`../README.md`](../README.md)
