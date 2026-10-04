# 任务列表（tasks）

本文件跟踪任务、承载任务 / 交接模板与关键记录。字段定义见下，状态区分 `blocked_decision` 与 `done`。
文档维护规范与索引见 [`README.md`](README.md)。

## 任务字段

- 任务编号：T-XXX（**逻辑任务**的稳定标识）
- 执行会话：承接本次执行的会话标识 / 句柄（可为"新会话"）；**区别于逻辑任务编号**
- 任务包边界：本会话承接范围的上限（不得事后扩展）
- 返修：本会话已用轮数 / 上限（默认 2）；逻辑任务累计轮数
- 责任：负责代理 / 角色
- 需求版本：对应的 requirements 版本或编号（或关联必要技术工作）
- 依赖：前置任务
- 写入范围：允许修改的文件或路径
- 状态：pending / in_progress / blocked_decision / done
- 证据：验证命令、结果位置或文件路径（含环境与版本标识口径，见 [`README.md`](README.md)）
- 问题编号：Q-XXX（关联 [`decisions.md`](decisions.md)）

规则：`blocked_decision` 表示受未决问题阻塞，不得视为完成；
`done` 需有证据且影响验收的问题已关闭；任务返回 / 交接（`NEEDS_HANDOFF`）都不是完成。

## 任务清单

| 任务编号 | 责任 | 需求版本 | 依赖 | 写入范围 | 状态 | 证据 | 问题编号 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| T-001 | ds41-writer 创建 / 主代理复核 | N/A | 无 | README/AGENTS/docs 骨架 | done | docs/verification.md 文档检查 | 无 |
| T-002 | ds41-writer 起草 / 主代理审查 | requirements v0.1 | T-001 | docs/ 7 份文档 | done | 需求基线 v0.1；Q-001 已由用户确认关闭 | Q-001 |
| T-003 | ds41-writer 执行 / 主代理复核 | v0.1 | T-002 | 环境检查记录、构建/测试脚本 | done | `build.ps1` 编译成功；`ark_left-check.exe` 真实查询成功；见 verification.md | Q-001 |
| T-004 | ds41-writer 实现 / 主代理验收 | v0.1 | T-003 | src/Program.cs, src/TrayApp.cs, app.manifest | done | `ark_left.exe --smoke-test` 布局断言通过；正常启动 4-5s 存活；见 verification.md | 无 |
| T-005 | ds41-writer 实现 / 主代理验收 | v0.1 | T-003 | src/Models.cs, src/QuotaParser.cs, src/QuotaCli.cs | done | `ark_left-tests.exe` 81/81 通过；真实 CLI 查询解析成功 | Q-001, Q-003 |
| T-006 | 主代理审查 + 自动验证 | v0.1 | T-004, T-005 | tests/、docs/verification.md | done | 主代理独立执行 build.ps1 / test.ps1（81/81）/ check.ps1（status=Ok）/ `--smoke-test`（退出 0）均通过；见 verification.md | 无 |
| T-007 | 主代理 / 用户 人工验证 | v0.1 | T-006 | 无（仅记录，不改代码） | pending | 多屏 / 高 DPI / 托盘点击与菜单交互的**人工视觉验证**；见 verification.md | Q-002 |
| T-008 | ds41-writer 创建 / 主代理复核 | interaction v0.2 | T-006 | docs/requirements/interaction-improvements.md, docs/requirements/README.md, docs/tasks.md | done | 主代理已审查通过文档清单（本轮进入实施） | 无 |
| T-009 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX001) | T-008 | src/TrayApp.cs | done | `HideController` 单 Timer 延迟 + 切换抑制；单元测试 `HideControllerCases`（213 全通过）；真实 Explorer 事件人工未测 | 无 |
| T-010 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX002) | T-008 | src/Program.cs, src/TrayApp.cs, src/Ipc.cs | done | 命名事件 IPC 无 MessageBox；第二实例（**不带 `--show`**）也应唤起；单元测试 `IpcSignalCases` + 隔离实例验证（单进程 / 第二实例退出 0） | 无 |
| T-011 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX003) | T-008, T-016 | src/TrayApp.cs, src/QuotaCli.cs, src/ViewState.cs | done | `PanelModel`（`BeginQuery` 隐藏、仅同 scope 复用）+ `IProgress` 阶段 / 8s 慢提示 / 取消重试 / 迟到进度不覆盖；单元测试 `PanelModel*` / `ProgressStageCases` | 无 |
| T-012 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX004) | T-008 | src/TrayApp.cs | done | 右键菜单“查看额度 / 退出 ark_left”；代码路径核对 | 无 |
| T-013 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX005) | T-008, T-019 | src/TrayApp.cs, src/ViewState.cs, docs/setup.md | done | 复制登录命令 / 打开指南（记事本）/ 重试；未登录同时给重试与复制 / 指南；`ErrorView` 动作标志单测；真实记事本 / 剪贴板人工未测 | 无 |
| T-014 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX006) | T-008, T-011 | src/TrayApp.cs, src/ViewState.cs | done | `RiskSummaryBuilder`（每产品最低、含周期中文、不求和）/ `PercentFormat`；产品名不重复团队版；单元测试 `RiskSummaryCases` / `PercentFormatCases` | 无 |
| T-015 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX007) | T-008, T-011 | src/TrayApp.cs, src/ViewState.cs | done | 倒计时 + 新鲜度 / 服务端更新区分；15s 计时器仅改文本；单元测试 `RelativeFormatCases`；真实走动人工未测 | 无 |
| T-016 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX008) | T-008 | src/Identity.cs, src/QuotaCli.cs, src/QuotaParser.cs, src/ViewState.cs, src/TrayApp.cs | done | TRN 解析 + 严格 scope（不子串、含 region/project/主子身份）+ Unknown 不缓存 + 友好提示；单元测试 `Scope*` / `Identity*`；真实身份切换人工未测 | 无 |
| T-017 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX009) | T-008, T-009, T-011 | src/TrayApp.cs | done | 去拖动 + `PanelPositioner` 固定尺寸；单元测试 `PanelPositionerCases` + 冒烟固定尺寸断言；多屏视觉人工未测 | 无 |
| T-018 | ds41-writer 实现 / 主代理验收 | interaction v0.2 (UX010) | T-008, T-010, T-013 | src/TrayApp.cs, src/Program.cs, src/Ipc.cs, tests/ | done | `AccessibleName` / tooltip / 静默启动 + marker（`%LOCALAPPDATA%/ark_left/first-run.done`，仅日期版本）；首启 banner 持久可关闭；单元测试 `MarkerIsolationCases`；冒烟不写 marker；键盘 / 读屏人工未测 | 无 |
| T-019 | ds41-writer 创建 / 主代理复核 | interaction v0.2 | T-008 | docs/setup.md | done | 本地设置指南已交付（安装 / 登录 / `ARK_LEFT_CLI` / 刷新 / 折叠处理 / 隐私 / marker） | 无 |
| T-020 | 主代理 独立复核 + 文档同步 | interaction v0.2 | T-009–T-019 | docs/verification.md, docs/requirements/implemented-behavior.md, docs/tasks.md | done | 主代理独立执行：`build.ps1`（无告警）/ `test.ps1` **245/245** / `--smoke-test` 退出 0 / `tests/verify-interaction.ps1` 8 项 ok / 真实 `check`（`scope_verdict=Same`、`status=Ok`，脱敏无 ID/余额）。**人工视觉 / 真实托盘手感仍由 T-007 `pending` 跟踪，不据此验收为真人通过** | 无 |
| T-021 | ds41-writer 实施 / 主代理验收 | interaction v0.2（复核修复轮） | T-009–T-020 | src/Identity.cs, src/ViewState.cs, src/TrayApp.cs, src/Check.cs, tests/QuotaTests.cs, tests/verify-interaction.ps1, docs/verification.md, docs/requirements/*, README.md, docs/tasks.md | done | 构建无损（消除 CS0414）、单测 **245/245**、`--smoke-test` 退出 0（含 intro banner + `preview-intro.png` + 终态权威回归）、`tests/verify-interaction.ps1` 退出 0（隔离环境）；真实远端查询由主代理执行确认 `scope_verdict=Same` | 无 |
| T-022 | ds41-writer 实施 / 主代理审查（有条件接受） | 流程规范（框架 7–9、11–12；用户本轮授权；**无业务编号**） | 无（文档规范更新，不依赖应用任务） | AGENTS.md, docs/README.md, docs/tasks.md, docs/requirements/README.md | done | 执行会话 `ses_eff4a3361ffe0l9KetrFpHvPHD`；主代理独立读四文件后合并反馈 **1 轮**（本会话 **1/2**、逻辑累计 **1**），6 条意见已全部精确修正并自查；**文档检查证据在本任务**（2026-10-03），相对链接全部解析；**未重跑应用测试** | 无 |
| T-023 | ds41-writer 实施 / 主代理验收 | interaction v0.3（UX011 持久快照 + 定时轮询 + 打开不查询 + 界面简化） | T-020, T-021 | src/PersistentState.cs, src/ViewState.cs, src/TrayApp.cs, src/QuotaCli.cs, tests/QuotaTests.cs, tests/verify-interaction.ps1, build.ps1, README.md, AGENTS.md, docs/requirements/interaction-improvements.md, docs/requirements/implemented-behavior.md, docs/requirements/README.md, docs/contracts.md, docs/decisions.md, docs/tasks.md, docs/verification.md, docs/setup.md | in_progress（**已中止，待 T-025 接续；不可标 `done`**） | **中止，未完成**：`src/PersistentState.cs` 草稿存在已核缺口（语义字段不全、`FromCache` 非法日期回退 `DateTime.Now`、未接入 TrayApp）；`src/ViewState.cs` 有 `NoteStrong` 为 `string` 却赋 `true`（无法编译）；缓存未接入。执行会话 `ses_2a1e0a9f` 为**占位 TBD，真实句柄未取得，不伪造**；历史保留。详见下方 T-023 中止记录 | 无 |
| T-024 | ds41-writer 写文档 / 主代理核对 | interaction v0.3（UX011）**需求契约（文档）** | T-022 | docs/requirements/*, docs/contracts.md, docs/decisions.md, docs/tasks.md, docs/README.md（必要链接） | done（**文档任务**，非系统交付） | 本任务：建立 UX011 权威覆盖旧规则、冻结持久快照契约、登记 Q-005、标注 T-023 中止与 T-025–T-027 拆串行；文档链接检查通过；**不做代码 / 不构建 / 不跑测试**。详见下方 T-024 记录 | 无 |
| T-025 | Sol fallback 实施 / 主代理复核 | interaction v0.3（UX011）数据模型 / 缓存 / 轮询接入 | T-024（需求契约）, T-023（接续） | src/PersistentState.cs, src/ViewState.cs, src/TrayApp.cs（controller + 最小 form 绑定）, tests/QuotaTests.cs, tests/verify-interaction.ps1, build.ps1, docs/tasks.md | done（主代理接受数据实现与独立自动验证） | 本轮主代理独立验收：`bin-release` build / **368/368** / smoke / 集成 8 项 ok；证据见 verification.md「v0.3（UX011）T027 主代理独立验证」及 `bin-release/t027-sha256.csv`（主代理收尾生成）；首次交付历史保留 | Q-005（CLOSED） |
| T-026 | ds41-writer 接续收尾 / 主代理复核 | interaction v0.3（UX011）展示简化 | T-025（数据控制部分经主代理复核） | 本轮冻结范围见下方 T026 记录 | done（主代理接受展示实现与独立自动验证） | 本轮主代理按 UX011 A–F 独立审查与验收，无新增实现缺陷；`bin-release` build / **368/368** / smoke / 集成 8 项 ok；证据见 verification.md「v0.3（UX011）T027 主代理独立验证」及 `bin-release/t027-sha256.csv`（主代理收尾生成）；T026 收尾轮历史保留 | Q-005（CLOSED） |
| T-027 | 主代理 独立验证收尾 | interaction v0.3（UX011） | T-025, T-026 | 文档收尾冻结范围：README.md, AGENTS.md（仅背景）, docs/tasks.md, docs/decisions.md, docs/verification.md；后续独立索引同步：docs/requirements/README.md, docs/requirements/interaction-improvements.md, docs/requirements/implemented-behavior.md, docs/contracts.md（本 writer 仅拥有前5文件） | done（主代理独立自动验证通过） | 主代理实际执行 `bin-release` build / **368/368** / smoke exit 0 / 隔离交互 8 项 ok，并接受 T025 / T026；writer 仅记录事实。见 verification.md「v0.3（UX011）T027 主代理独立验证」及 `bin-release/t027-sha256.csv`（主代理收尾生成）；T-007 人工仍 pending，系统 Gate 未通过 | 无 |
| T-028 | ds41-writer 尝试（ses_efeb5af45ffecM3iPxAKcwZPJ9，工具不可用，0 写入）/ Sol fallback 实施（ses_efeb41d53ffehSXAGh523FBCrj，落盘）/ 主代理审阅 | 流程更正（无业务编号） | 无 | AGENTS.md, docs/README.md, docs/tasks.md | done | 主代理逐段复核与局部补丁范围检查通过，新增链接目标存在；Windows / PowerShell 5.1.26100.9444；SHA256：bin-validation/t028-sha256.csv（主代理收尾生成）；未重跑应用测试 | 无 |
| T-029 | ds41-writer 需求登记 / 主代理审阅 | interaction v0.4（UX012）**需求登记（文档）** | 无（独立结构需求登记） | docs/requirements/interaction-improvements.md, docs/decisions.md, docs/tasks.md | done（主代理已审阅确认原登记写入；Q-006 落实补信息后最终复核接受） | 原登记经主代理审阅确认；本轮据用户 question 回复原文补记 Q-006 = DECIDED 与 v0.4 确认版，更新 UX012 结构与 Q-006 / T-030；**未实现、未构建、未测试、未声称已验证**；主代理 2026-10-03 最终复核接受（T031 收尾），**不回填原登记记录** | Q-006 |
| T-030 | ds41-writer 实现 / 主代理独立审阅验收 | interaction v0.4（UX012，确认版）圆圈 / 详情实现 | T-029, Q-006（DECIDED） | src/FloatingQuotaForm.cs（新增）, src/TrayApp.cs（运行接入 / smoke 局部）, tests/QuotaTests.cs, docs/contracts.md（v0.4 契约）, docs/tasks.md | done（主代理独立验收修复轮 2/2；T031 收尾接受） | 新增 `src/FloatingQuotaForm.cs`、运行接入与单测；修复 1 补充严格字段数校验、四向布局回退 / 暂隐恢复、两行标题 / DPI 字体、真实鼠标处理器单测、设置弹窗字体缩放；**修复 2** 修正 `PrepareDetails` 按圆圈所在屏且所有分支（含 HideCircle）预置非零 bounds、`ShowDetails` 先初始化圆圈再按圆圈屏尺寸布局、显式隐藏清自动恢复标志、测试改走生产共享菜单设置路径；主代理 2026-10-03 独立自动验收通过（`bin-v04` build 无告警 / **518/518** / smoke exit 0 / 交互 8 项 ok），证据见 verification.md「v0.4（UX012）T031 主代理独立验证」及 `bin-v04/t031-sha256.csv`；T030 本会话原始 465 / 500 / 518 记录保留 | Q-006（CLOSED，T031 闭环） |
| T-031 | 主代理 独立验证收尾 | interaction v0.4（UX012） | T-029, T-030 | 文档收尾冻结范围：docs/verification.md, docs/tasks.md, docs/decisions.md, docs/contracts.md | done（主代理独立自动验证通过；并行需求索引记录见短记录） | 主代理实际执行 `bin-v04` build（无告警）/ **518/518** / smoke exit 0 / 隔离交互 8 项 ok / SHA256 `4F4D…27ED3`（`bin-v04/t031-sha256.csv`），并接受 T029 / T030；writer 仅记录事实。见 verification.md「v0.4（UX012）T031 主代理独立验证」；T-007 人工仍 pending，系统 Gate 未通过 | Q-006（CLOSED） |
| T-032 | 初始实现 ds41-writer / 主代理独立验收；文档收尾 tl-openai/gpt-6-luna | interaction v0.5（UX013）可见性轮询间隔 | T-031 | src/TrayApp.cs（轮询间隔 + 最小 test hook）, src/FloatingQuotaForm.cs（circle VisibleChanged 转发 event）, tests/QuotaTests.cs, README.md, AGENTS.md（背景段）, docs/README.md, docs/requirements.md, docs/requirements/README.md, docs/requirements/product-requirements.md, docs/requirements/interaction-improvements.md, docs/requirements/implemented-behavior.md, docs/setup.md, docs/contracts.md, docs/tasks.md, docs/verification.md | done（代码与独立自动检查验收通过；非系统交付） | 主代理独立执行：`build.ps1 -OutputDir D:/ark_left/bin-v05`，3 exe 成功、无警告；隔离状态运行 `ark_left-tests.exe`：**passed 532 / failed 0**（含真实 10 秒生产 WinForms Timer 注入 query 测试）；`verify-interaction.ps1 -OutputDir D:/ark_left/bin-v05`：8 项 ok，suffix `43a42a5e`；隔离 smoke exit 0 且未创建 state。产物 `ark_left.exe` SHA256 `BE0F0CD426D2D7171BC96D926ED325BC3DF56D60B6451E81694A25DB0967A852`。清单 `bin-v05/t032-sha256.csv` **待主代理于文档收尾后生成**。实现规则：圆圈或详情任一可见 10000ms，两者全隐藏 300000ms；设置不单独提速；显隐仅调整间隔、不立即查询，相同间隔不重置 countdown；启动查询及 single-flight / cache / data semantics 不变。文档收尾仅记录已提供证据，未重跑检查。T-007 人工仍 pending，系统 Gate 未通过；测试离线合成，不运行 CLI / 账号 / 网络 | 无 |
| T-033 | tl-openai/gpt-6-luna 实施 / 主代理独立审查验收 | interaction v0.6（UX014）仅视觉与布局优化 | T-032（已完成并接受；不恢复其范围） | src/TrayApp.cs（CardPanel / QuotaBar / PopupForm、共享菜单样式、合法 smoke 预览）, src/FloatingQuotaForm.cs（圆圈绘制 / 字体 / hover focus、设置窗与菜单外观）, src/UiStyle.cs（如确需新增）, tests/QuotaTests.cs（少量视觉布局断言）, docs/requirements/interaction-improvements.md（顶部及 v0.6 UX014）, docs/contracts.md（v0.6 视觉契约）, docs/tasks.md | in_progress（实现待主代理审阅；未标完成） | 已在实现前登记 UX014 基线与 v0.6 视觉契约。冻结语义：业务 v0.1 R001–R004、UX012 圆圈 / 水波 / 拖动 / 菜单 / 设置选择、UX013 轮询 10s / 5min 与设置窗不提速均不变；打开不查询、single-flight / cache / identity、同语义刷新焦点与滚动保持不变。实现与构建 / 测试 / smoke / 交互证据待实际执行后追加。本任务仅完成实现交付供主代理审阅，T-007 人工视觉验收仍 pending；不部署、不改正式 bin / start.cmd / 历史候选。 | 无 |
| T-034 | 待主代理指派（委派**工具被中断**，无最终报告 / task_id） | interaction v0.6（**UX014 已登记，实现候选待核验**；**不将 UX015 记为已完成**） | T-032（已验收）、T-033（UX014 已登记，实现候选待核验） | 待核验：src/TrayApp.cs / src/FloatingQuotaForm.cs 等**部分落盘**改动（实际所有者待主代理核验） | in_progress（委派中断 / 部分落盘 / 待核验；**不声称原 writer 已完成，不覆写 T-033 执行者 tl-openai/gpt-6-luna**） | 实际 `src/` 已有 `SettingsRequested` / direct settings 等部分改动，**无构建 / 审阅证据、无 task_id**。后续先局部核实源码与旧执行确已停止，再拆串行**新会话**：①视觉布局修复 ②设置入口与 modal 返回逻辑 ③验证与本地交付。冻结语义（圆圈水位 / single-flight / 10s / 5min / 打开零查询）不得回退 | 无 |
| T-035 | 主代理主导（产品取舍 / 拆分 / 基线登记 / 独立验收）；实施按顺序委派 | interaction v0.6 草稿（新增功能须**先登记基线**） | T-034、T-033 核验结果 | docs/ 基线登记 + 后续各实施包源码（每包单一边界与文件所有权） | pending（**设计已定、产品实施待继续**；设计经主代理审阅后接受） | 阶段A 核验并完成现代化草稿（保留圆圈水位 / single-flight / 10s / 5min / 打开零查询）；阶段B 可见设置入口 + `Esc` / `Tab` / 保存失败 / 失焦返回 + 长文案 / DPI；阶段C 离线构建 / 风险匹配测试 / 预览 / 证据 / 可启动本地成品，验收通过后最多 1–2 个独立新增功能包。候选（**仅候选**）：圆圈位置锁定、透明度调节，主代理评估选择并**先登记需求再做**；**不凭空增加业务数据来源**。授权免重复确认 ≠ 验收自动通过；人工未测仍 T-007，旧关键 `BLOCKED_DECISION` 不自动关闭。交付标准：exe / 明确启动入口 / 预览 / 测试与版本 hash 证据 / 实际已测未测；不把设计当成品 | 无 |
| T-036 | 主代理定义规则 / 设计 writer 落盘（`ses_efd1a4496ffeSEkR3nC2h0JO8e`） | 流程规则（无业务编号） | T-022、T-028 | docs/development-plan.md、AGENTS.md、docs/tasks.md、docs/README.md | done（**仅指设计规则经主代理独立审阅接受，非产品交付**） | 证据：主代理 2026-10-04 局部审阅接受（读 development-plan 1–105、AGENTS 29–105、tasks 58–65、README 15–28、verification 524–547）；设计 writer 会话 `ses_efd1a4496ffeSEkR3nC2h0JO8e`，本轮第 1 轮返修 1/2、逻辑累计 1。规则：每个 brief 必须给路径 + 章节 / 符号或当前行窗口、必要基线摘要、依赖签名、允许写范围、命令 / 验收；先 `Grep` 后 `read offset/limit`。禁止全量载大源码 / 完整历史 / 全文对话；仅小且整段必须使用的文件可整读并注明理由；遗漏依赖仅按具体疑点补读并注明原因；不为了机械 token 预算跳过正确性必须读取。验收附实际读取窗口摘要，主代理审查有无无关全文加载；原则性自动加载 system / AGENTS 与程序化 build / schema / hash / 完整 JSON 解析不计人工全文加载，但不得输出密钥 / 无关内容 | 无 |
| T-037 | 主代理定义规则 / 各执行子代理遵守（`ses_efd1a4496ffeSEkR3nC2h0JO8e`） | 流程规则（无业务编号） | T-022、T-028 | docs/development-plan.md、AGENTS.md、docs/tasks.md | done（**仅指设计规则经主代理独立审阅接受，非产品交付**；与下文「会话生命周期」一致） | 证据：主代理 2026-10-04 局部审阅接受；设计 writer 会话 `ses_efd1a4496ffeSEkR3nC2h0JO8e`，第 1 轮返修 1/2、逻辑累计 1。规则：不同细分任务每次 `task` 新调用、**不传旧 task_id**；每个任务单一边界与文件所有权。仅**同一未验收任务**的有限返修 / 信息补齐可复用原 id，**最多 2 轮**、逻辑累计保留。前任务停写并交接摘要后再开新 writer，**相同文件不并发写**。验收任务号与返回真实会话 ID 分别记录、**不伪造**；验收审查 `task` 不复用跨任务旧会话，handoff 含清晰交接与 modified symbols | 无 |
| T-038 | 全局配置会话 `ses_efd242e48ffeBFydOCRFGdF6R9` / 主代理审阅 | 工具链配置（项目外，用户授权） | 无 | **项目外用户授权**：全局 opencode 配置（agent `glm-implementer` + `glm-patch-routing.ts` 等）；本项目仅 docs/development-plan.md, docs/tasks.md, docs/verification.md | done（主代理已审 review + 离线桩 6/6 + 真实图片核验成功；**系统 Gate 未通过**） | 真实句柄 `ses_efd242e48ffeBFydOCRFGdF6R9`，范围内修复 **1/2**，逻辑累计 **1**；修复原因：GLM Responses 映射空文本，改仅 GLM per-model SDK 走 `chat/completions`（npm `@ai-sdk/openai-compatible`）。**不声称当前父会话已用 GLM 写源码**；真实图片证据 **≠** 子代理真实代码写入；220k/30k 仅为配置上限，未做极限容量压力测。短证据见 verification.md | 无 |
| T-039 | 主代理定义规则 / ds41-writer 落盘（`ses_efd0fe82fffez7TOf7EWUyl4ay`） | 流程规则（无业务编号） | T-036、T-037 | AGENTS.md, docs/development-plan.md, docs/tasks.md, docs/README.md, docs/verification.md | done（**主代理独立审阅接受**；仅规范文档，**不自动运行时监控**；非产品交付） | 子代理上下文防膨胀规范：`development-plan.md` 新增 **§9 子代理上下文控制**为唯一详细执行规范，`AGENTS.md` / `README.md` / `tasks.md` 以简短引用与模板字段连接。主代理 2026-10-04 局部审阅 development-plan 103–123、AGENTS 90–106、README 88–94、tasks 63–68/500–513/531–560、verification 558–564 后接受；**修正 1/2、逻辑累计 1**（条 4 阈值限定为当前 GLM 示例，其他模型沿各自 brief）。本次仅纯文档最小 `apply_patch`，**不实现自动 token 监控**、不改 global config / 源码 / bin、不跑应用 / CLI / model、不部署。SHA256 `bin-validation/t039-sha256.csv` 由主代理文档收尾后生成，**记录时未生成** | 无 |
| T040 | **由 T041（GLM `tl-openai/glm-5.3-flash`）伴随登记完成**；前一纯文档 task 被中断、无报告 / 未落盘，**不伪称其完成** | interaction v0.7（UX015）基线登记 + v0.6 状态更新 | T-034（中断核验）、T-035 | docs/requirements/interaction-improvements.md（顶部 + 新增 v0.7 UX015）, docs/contracts.md（v0.6 状态 + 新增 v0.7 短契约）, docs/tasks.md（本三行） | done（基线登记落盘；**仅登记，非行为实施**；主代理 2026-10-04 已复核通过） | UX015 v0.7 交互基线冻结：常驻可聚焦设置入口、共享菜单 / 详情打开单实例 modal、打开 / 保存 / 取消零查询、Esc / 取消 / Alt+F4 不保存、保存失败保持打开 inline 错误、设置期间不失焦隐藏详情、关闭回原详情保留焦点滚动、圆圈 / 托盘开不强制详情、Tab 顺序、空态禁保存、长选项完整、100/150/200% DPI 不重叠裁切。T042 pending 实施验证；不伪称 UX015 行为已实现 | 无 |
| T041 | GLM `tl-openai/glm-5.3-flash`（本会话） | interaction v0.6（UX014）现代化视觉 / 布局收尾 + 修明显布局缺陷 | T040（伴随登记）、T-034（中断核验，接续未验收候选） | src/TrayApp.cs（CardPanel / QuotaBar 复核、PopupForm.LayoutChrome / MakeHeaderButton、新增 chrome smoke 断言与 TitleLabelForTest hook）, src/FloatingQuotaForm.cs（仅复核，未改动）, src/UiStyle.cs（复核，未改动）, tests/QuotaTests.cs（chrome 布局断言）, docs/tasks.md（本行） | done（主代理 2026-10-04 独立验收接受：bin-v07 构建、538 单测 0 失败、smoke 0、8 项交互检查全通过；视觉源码接受；本会话返修 1/2；T-007 人工视觉仍 pending，不构成系统交付） | 修复 LayoutChrome 明显缺陷：死 `_identity` 标签（从未加入 Controls、文本恒空）不再占用第二行标题空间（整体移除）；隐藏的 `_cancelBtn`（恒 Visible=false）不再预留宽度压缩 title，title 单行垂直居中并占用至刷新按钮的全部空闲宽度；不引入外部依赖，海军蓝 / 青绿 / 瓷白方向不变。冻结语义不变：业务 v0.1 R001–R004、固定详情尺寸 / 工作区规则、136dp circle、真实水位 0/100/unknown、选择格式 1、轮询 10s/5min、single-flight / cache / identity、打开零查询、同语义保焦点滚动；不改设置 modal 行为（UX015 归 T042）。构建 / 测试 / smoke / 交互检查证据由主代理独立执行通过；T-007 人工视觉仍 pending | 无 |
| T042 | GLM `tl-openai/glm-5.3-flash`（新会话执行） | interaction v0.7（UX015）交互语义实施与验证 | T041、T040 | src/TrayApp.cs / src/FloatingQuotaForm.cs（设置 modal 返回 / 焦点 / Tab / DPI 行为）, tests/QuotaTests.cs, docs（按实际改动） | done（主代理 2026-10-04 独立验收接受：build bin-v07、668 单测 0 失败、smoke exit 0、verify-interaction 8 项通过；本会话返修 1/2（逻辑累计 1）；T-007 人工视觉仍 pending，不构成系统交付） | 按 v0.7 UX015 契约实施：单实例 modal、零查询打开 / 保存 / 取消、Esc / Alt+F4 / 取消不保存、保存失败 inline、设置期间不失焦隐藏、关闭回原详情恢复 ActiveControl 与 scroll、重复请求不提前解锁 dialog suppression（SettingsModalOpen 守卫）、owner 所在屏 DPI / workArea 定位与缩放、空态禁保存、长候选择量 ToolTip + 下拉宽度测量 clamp、错误标签折行增高；不加位置锁定。返修 1：owner 改按真实圆圈 surface（新生产属性 `CircleSurface`），圆圈 / 详情均不可见时两者都不强制弹出；返修 2：跨容器 Tab 顺序 host→save→cancel→header(close)，并以 `SelectNextControl` 在已显示窗口真实遍历断言 combo→save→cancel→close；返修 3：有效布局 scale（下限 50%，字体与布局同步）+ 按钮宽度按可用空间收缩，100/150/200% × 400x200 矩阵头部 / 内容 / 按钮不重叠、均在窗内、下拉不超工作区；返修 4：`RestoreDialogState` 先激活 / 恢复有效焦点再恢复滚动（含显式恢复 0），disposed / Disposing 不重激活（含行为测试）。新增 UX015 用例（注入布局矩阵、窄 / 短工作区、生产路径 modal 取消 / 保存失败 / 重复请求 / 焦点滚动 / 零查询 / 圆圈 owner 屏）；人工视觉仍 T-007 | 无 |
| T043 | GLM `tl-openai/glm-5.3-flash`（本会话，独立执行，禁止再委派） | interaction v0.8（UX016）位置锁定实施与验证 | T042（done） | src/FloatingPreferences.cs（新增）, src/FloatingQuotaForm.cs（circle 锁定手势 / 共享菜单项 / 偏好注入 / LockSaveFailed）, src/TrayApp.cs（共享菜单 Build / Opening 同步 / 失败托盘通知意图）, tests/QuotaTests.cs, docs/requirements/interaction-improvements.md（顶部 + v0.8 UX016）, docs/contracts.md（UX015 状态 + 新 UX016 契约）, docs/tasks.md（T042 收尾 / 本行） | done（主代理 2026-10-04 审读通过；返修累计 1；build bin-v08 3 exe 无告警、**732 单测 0 失败**、隔离 smoke exit 0 且真实 state 无 `floating-preferences.json` 落盘、verify-interaction **8 项 ok**（suffix d631e69d）） | UX016 v0.8 冻结：右键圆圈与托盘菜单在查看 / 设置之后新增 checkable「锁定位置」，两菜单共用状态 / handler；默认解锁，锁定只阻止拖动，不阻止左键 / Enter / Space 详情、右键菜单、显示隐藏；锁定时超阈值手势不误开详情、单击有效；切换中释放 Capture / reset 手势。跨启动记忆锁定布尔：新增 identity-free `floating-preferences.json` 格式 1（仅 Version int / PositionLocked bool，严格字段 / 类型校验，损坏回默认），不影响 floating-settings.json 格式 1 / DPAPI；原子写失败不丢旧有效文件；写失败保旧状态与菜单 check + 短可读提示（circle tooltip status，不回显异常）；加载 / 保存可注入供离线测试。位置仍本会话有效、不存坐标，原屏工作区 clamp 不变；默认零额外写入、smoke 无 prefs 落盘（SmokeFloating 亦改为注入 no-op prefs）；不影响 quota / query / auth 缓存 / 轮询 / 单实例。范围冻结：不加透明度 / 动画（另包）。旧 MenuCount / index 断言有意更新（菜单 5→6，toggle 2→3，lock=2）。新增 UX016 用例（store 严格解析 / 往返 / 原子写失败保旧 / 默认与重启记忆 / 锁定点击与阈值手势 / 解锁可拖 / 中途切换 capture 安全 / 保存失败与抛异常保旧 / 共享菜单 check 同步 / 零查询）。候选 exe SHA256 `84C3E93A4623C1702AE8489FEA130EDC00582EC27EC714ADC9372653E65636F4`；过程修复 2 处测试自身断言（replace-failure 前先 resave 有效文件；midToggle 基准改用该手势前位置），非产品语义变更。**返修 1**（主代理审阅 2 缺口，本会话）：① `FloatingPreferencesStore` 新增 `MaxFileBytes=4096`，Load 读取前 `FileInfo.Length` 检查、超限视为损坏回默认（新增 oversize / 上限内加载测试），契约同步大小上限；② 新增 `LockSaveFailed` 事件——circle 不可见且保存失败时 `TrayApp.OnLockSaveFailed` 经 `NotifyIcon.ShowBalloonTip` 固定短文案「位置锁定状态未保存」（非 raw），offline 无真实 NotifyIcon 仅暴露通知意图计数（`LockFailNotifyCountForTest` / `LastLockFailTextForTest`），circle 可见时仍走 tooltip 且不计数，成功切换零新增通知；不强制打开 circle / 详情、不改 poll / query；事件绑定于 `_floating` 创建后，handler 带 `_disposed` / `_notify==null` 守卫；新增构造重载 `TrayApp(query, prefsSaveOverride)` 供离线注入失败 save 验证隐窗失败通知意图 + 旧状态 checked。复跑证据见状态列（SHA256 `DFC6D320FC31A00D41B7CCD0C37D5CCF082241216202792D71D06ADCD197BA6C`） | 无 |
| T044 | GLM `tl-openai/glm-5.3-flash`（本会话，独立执行，禁止再委派） | interaction v0.9（UX017）减少动画实施与验证 | T043（done） | src/FloatingPreferences.cs（格式 2）, src/FloatingQuotaForm.cs（circle UpdateWaveState / SetReduceMotion / 偏好菜单与保存）, src/TrayApp.cs（共享菜单项 / 同步 / 失败通知意图 / 静态波 smoke）, tests/QuotaTests.cs, docs/requirements/interaction-improvements.md（顶部 + v0.9 UX017）, docs/contracts.md（UX016 状态 + UX017 契约）, docs/tasks.md（本行） | done（主代理 2026-10-04 独立验收接受：build bin-v09 3 exe 无告警、**820 单测 0 失败**（含新增 UX017 用例）、隔离 smoke exit 0 且真实 state 无 `floating-preferences.json` 落盘、`preview-floating-static.png` 已生成且既有预览保留、verify-interaction **8 项 ok**（suffix d0d2af54）；过程中新增测试捕获 `SetPositionLocked` 保存未携带 `ReduceMotion` 的缺陷并已修复（格式 2 任一保存双 flag），非语义变更；T-007 人工视觉仍 pending） | UX017 v0.9 冻结：右键圆圈 / 托盘在「锁定位置」之后新增 checkable「减少动画」，默认 false，两菜单共用状态 / handler；开启停止水波动画定时器并立即重绘静态波面，剩余比例 / 百分比 0/100/未知保持，后台轮询 10s/5min 不变、零查询；关闭仅 circle 可见且已知 0<percent<100 重启动画，隐藏 / 端点 / 未知不启动，disposed 不再启动；跨启动记忆，开关互不覆盖锁定，同值不写盘，保存失败保持全部旧偏好与两菜单勾选，失败提示区分 reduceMotion 不得误称锁定失败（tooltip / 隐藏托盘短固定文案）。prefs 升级格式 2（Version=2 / PositionLocked / ReduceMotion 严格三字段 / 类型 / 4096 上限），格式 1 严格两字段读取迁移为内存格式 2 且 ReduceMotion=false、加载不写盘，后续任一保存写格式 2 且保留另一 flag，非 schema 不接受；floating-settings 格式 1 / quota-cache 格式 1 不变；迁移理由入契约，无凭据 / 坐标。菜单 6→7（motion=3，toggle 3→4）为有意调整。新增 UX017 用例（格式 2 往返 / v1 迁移不写盘 / 严格拒绝 / 双 flag 互不覆盖 / 失败保双状态双菜单 / 隐藏失败通知意图区分 / 真实 wave timer 启停 / 重复 toggle 零查询 / poll 间隔不变 / 0,100,unknown / 隐藏显示恢复 / Dispose 不重启）；smoke 新增静态波快照 preview-floating-static.png（同窗 DrawToBitmap，非桌面截图）。范围冻结：不加透明度 / 全局热键。 | 无 |
| T045 | GLM `tl-openai/glm-5.3-flash`（本会话，独立执行，禁止再委派） | 技术交付 UX018 / interaction v0.10（本地启动入口与旧实例平滑替换） | T044（done） | start.cmd, launch.ps1（新增）, tests/verify-launcher.ps1（新增）, src/Program.cs（仅 AssemblyVersion / FileVersion → 0.10.0.0）, docs/requirements/interaction-improvements.md（顶部 + v0.10 UX018）, docs/contracts.md（本地启动契约）, docs/tasks.md（本行）, bin-v10（构建产物与 hash 清单） | in_progress（返修 1/2、逻辑累计 1 已完成并全绿；实施与隔离自动检查通过，待主代理审阅验收；真实 PID 15796 未触碰，真实启动由主代理执行） | UX018 v0.10 冻结：`start.cmd` → `launch.ps1`（默认 `-OutputDir bin-v10`）→ `bin-v10\ark_left.exe --show`，exe 缺失经 `build.ps1 -OutputDir` 构建；同路径当前版本实例仅复用单实例 `--show` 唤醒、不关闭不重复常驻；旧构建目录（bin / bin-release / bin-v04–bin-v09）实例以 `EnumWindows`+`GetWindowThreadProcessId` 定位唯一 WinForms UI 线程（含隐藏窗，严格 `WindowsForms10*` 签名无 fallback）`PostThreadMessage(WM_QUIT)` 恰一次，`Application.Run` 退出后应用自身 `Cleanup()` 释放资源，每实例等待 ≤10s 后再启动新 exe；目标目录须 root 直接子目录，构建先于关闭（失败保旧），NoLaunch 不构建；超时 / GUI 线程不唯一 / PID 不存在或非 ark_left / 路径不可读或不在白名单 / 嵌套目标 → 简短错误非 0 不启动；生产模式路径不可读的 ark_left 候选即失败不静默；绝不 Stop-Process / kill / 覆盖二进制；脚本不设 env、不写真实 state。返修 1 落实：互斥体断言改"新实例接手"（替换流）+ 挂起恢复后 gone（suffix2）；构建前置 close 循环前 + exe 存在/非空检查；移除 UI 线程 fallback；生产模式不可读路径失败；新增嵌套目标拒绝（old 未退出）与 NoLaunch 不构建断言。第 5 轮 verify-launcher **20 项 ok exit 0**（suffix b528dc44）；build bin-v10 3 exe 无告警、820/0、smoke 0、交互 8 项 ok（产物未变维持）；hash `bin-v10/t045-sha256.csv`：ark_left.exe `F2B26B2A…FCE7826` | 无 |
| T046 | ds41-writer（模型 `tl-openai/deepseek-v4.1-flash`，真实会话 `ses_efc1a2b5dffeHBnaPSPxEtKA2Q`）；承接 GLM 会话 `ses_efc3694a9ffenSwl9M83LP5qXk`（已返回停止、输出陷入重复、未完成测试且不能恢复同异常上下文）的异常交接，按用户实施顺位 GLM→DS→Sol 回退直接实施、不再委派 | 交互改进 UX019 / interaction v0.11（复制当前额度摘要）修复与验收（**T045 主代理已独立验收 done；T046 仍待主代理验收**） | T045（主代理 2026-10-04 已验收 **done**） | src/QuotaSummary.cs（纯 formatter；**本次**仅改安全文案与 null 防御）, src/TrayApp.cs（copybutton / footer / binding / hooks；**本次**仅 footer 窄屏不重叠 + 删除未用 hook）, tests/QuotaTests.cs（QuotaSummaryCases / CopySummaryUX019Cases / CheckFooterLayout；新增焦点 / scroll 保持断言）, src/Program.cs（版本 0.11.0.0）, start.cmd / launch.ps1（默认 bin-v11、oldDir 加 bin-v10）, docs/requirements/interaction-improvements.md（**事后**补登记 v0.11 UX019）, docs/contracts.md（新复制摘要契约 v0.11）, docs/tasks.md（本行） | in_progress（自动检查已全绿、待主代理验收；**返修 1/2、逻辑累计 1**：GLM 异常属交接不计返修，本轮为主代理审阅后的返修 1） | **流程偏差（如实记录，不伪造原登记 / 执行报告）**：UX019 的 `src/QuotaSummary.cs`、`src/TrayApp.cs` copy 相关代码与 `tests/QuotaTests.cs` UX019 用例由 GLM 在执行会话先于正式文档落盘，但 `docs/` 顶部当时仍为 v0.10、无 UX019，违反“先登记基线再实施”，该会话随后异常停止；本会话为 fallback，先补登记 v0.11 基线再修正与验收。**修复**：① 根因——隐藏窗体下 `Button.CanSelect=false`，`copy.PerformClick()` 不触发 `Click`，导致 `clips` 为空后 `clips[0]` 抛 `ArgumentOutOfRangeException`（bin-v11/test-crash.txt 6 行）；测试改为 `ShowPanel()` + `Application.DoEvents()` 走真实控件事件路径（同步剪贴板路径，非异步），注入 recorder 不触真实剪贴板，并断言 copy 前/后焦点与 scroll 保持；② 严重安全修正——`QuotaSummary` 原直接附 `v.Data.Message` / `p.Error` / `q.Error` / `q.UnknownNote` 原文（raw-text 泄露通道），改为**固定安全文案**（未订阅 / 额度获取失败 / 订阅状态未知 / 缺少可用的百分比数据 / 总量为 0 等），并冻结为独立信任边界、不依赖 parser sanitize；新增含 secret sentinel 的 Message/Error/UnknownNote 断言；③ 诚实数据——null product/period 安全跳过、`total=0` / `cache` / `stale` / `identityUnknown` 明确标记、剩余量文案带「额度」；④ footer 窄屏——原 `Math.Max(S(100), …)` 在窄屏 2x 会越过按钮，改为按按钮左界反推并允许收缩，测试新增 360px@2x 用例（不只 480px@1x）。**返修 1**：删除新增但完全未用的 `SetCopyActionForTest` / `CopyActionHandler`，恢复最短生产 `_copyBtn.Click += delegate { OnCopySummaryClicked(); }`；`ClipboardSetForTest` 才是所需注入接口。**实测（返修 1 重跑）**：build bin-v11 3 exe 无告警、**906 单测 0 失败**、隔离 smoke exit 0 且未创建 state；verify-interaction 8 项 ok（suffix 4fd5f135）与 verify-launcher 20 项 ok exit 0（suffix 2e8c2886，PID 限定、未触真实实例）**源码未变故不重复运行**。**接受 T045**：build bin-v10 3 exe 无告警 / 820 单测 0 失败 / smoke exit 0 / verify-interaction 8 项 ok / verify-launcher 20 项 ok exit 0（suffix b528dc44）/ hash 清单 `bin-v10/t045-sha256.csv`，主代理 2026-10-04 独立验收 done；真实 PID 15796 未触碰。**未测**：人工托盘视觉 / 真实剪贴板点击仍 pending（承接 T-007）；未启动真实常驻实例。 | T-007 人工视觉 / 真实剪贴板 pending；系统交付 Gate 未通过 |
| T047 | GLM `tl-openai/glm-5.3-flash`（本会话，独立执行，禁止再委派） | interaction v0.12（UX020）悬浮窗归位实施与验证 | T046（主代理 2026-10-04 独立验收接受 **done**：906 单测 0 失败、smoke 0、交互 8 项 ok、launcher 20 项 ok；T046 行超 2000 字符无法局部改写，其行内状态由主代理收尾同步，本行随附记录） | src/FloatingQuotaForm.cs（FloatingCircleControl MoveHome / 手势重置；FloatingQuotaForm RepositionCircleHome / 注入 core / autoHidden / Positioned hook）, src/TrayApp.cs（共享菜单「悬浮窗归位」/ handler）, tests/QuotaTests.cs（UX020 用例 + 菜单 7→8 / toggle 4→5 收紧）, src/Program.cs（0.12.0.0）, launch.ps1（默认 bin-v12、oldDir 加 bin-v11）, start.cmd（bin-v12）, tests/verify-launcher.ps1（仅 v10 输出标签改用实际 OutputDir）, docs/requirements/interaction-improvements.md（顶部 + v0.12 UX020）, docs/contracts.md（UX020 契约）, docs/tasks.md（本行） | in_progress（**返修 1/2、逻辑累计 1**；隔离自动检查全绿，待主代理验收） | UX020 v0.12 冻结：共享菜单在「减少动画」后新增「悬浮窗归位」（7→8，toggle 4→5 有意收紧）；归位=当前圆圈所在屏工作区右下默认安全 margin（复用 InitialBounds/ClampTo），未定位取 primary，用该屏 DPI 136dp；锁定不阻止归位；先重置拖动手势并释放 Capture；详情可见先 HidePanel/RestoreAfterDetails 再归位并清 _autoHiddenForDetails；归位不开详情；SettingsModalOpen 时 handler 安全 return（不动 owner、不关 modal）；零查询、不改 10s/5min 结构、两 prefs 零写入、不保存坐标。测试注入 work area/scale 与生产共用核心。**实测（首轮）**：build bin-v12 3 exe 无告警；隔离单测 **959/0**（首跑 958/1 暴露 ApplyHome 未清 auto-hidden 标志，补 1 行修复后全绿）；隔离 smoke exit 0 且 state 零落盘；verify-interaction **8 项 ok**（suffix 46aa0649）；verify-launcher **20 项 ok exit 0**（suffix 1efec826，输出标签已用实际 OutputDir bin-v12）；真实 PID 15796 / 真实 CLI / 真实剪贴板未触碰。**返修 1**（主代理审读 1692-1726）：原实现直接 `CircleScreen()`，而 fresh 控件默认非零 Bounds(0,0) 可落 secondary，「未定位取 primary」未落实；改为 `!_positioned → Screen.PrimaryScreen ?? AllScreens[0]`，其余 `CircleScreen()`；新增 `PositionedForTest` hook 与未定位 primary 用例（fresh form 为真实未定位 fixture，单屏确定性、不依赖第二屏，不把未测称 passed）；审实 `ShowCircleAtForTest` 一直不设 `_positioned`，旧行为未改；`home.hiddenAppHome` 期望改用 `GetScale(primary)` 移除测试机 100% 隐含假设。**返修 1 实测**：build bin-v12 3 exe 无告警；隔离单测 **963/0**；隔离 smoke exit 0 且 state 零落盘；launcher / 8 交互源码未变未重跑（主代理独立通过：交互 suffix 4562c4f9、launcher suffix 006feb38） | 无 |
| T048 | ds41-writer（纯文档 / 配置手工编辑；**本会话，不再委派**；真实执行句柄 `ses_efbe4d1dcffeLACoYeQUlkmPRk`） | 文档收尾（无业务编号；仅记录 v0.12 UX020 与主代理 2026-10-04 已验收事实） | T047 | README.md, AGENTS.md（仅背景 11–15）, docs/README.md, docs/requirements.md, docs/requirements/README.md, docs/requirements/product-requirements.md（仅顶部覆盖说明）, docs/requirements/interaction-improvements.md（顶部 + UX018/UX020 当前状态）, docs/requirements/implemented-behavior.md（顶部 + v0.6–v0.12 事实短节）, docs/setup.md, docs/tasks.md, docs/verification.md, docs/contracts.md（UX018/UX019/UX020 状态） | done（**主代理 2026-10-04 接受**局部文档改动与相对 links 检查；返修 2/2、逻辑累计 2） | 记录主代理 2026-10-04 独立验收接受 T041–T047、主代理授权内本地升级 v0.12、`bin-v12\ark_left.exe` 0.12.0.0 / SHA256 `C5114937…67607E`；**未改源码 / 脚本 / 二进制，未跑应用 / CLI / 网络**；`bin-v12/t048-sha256.csv` 已由主代理生成（48 文件 manifest），最终文档落盘后由主代理刷新；T007 人工 / 系统 Gate 仍 pending | 无 |
| T049 | ds41-writer（纯文档缺陷修复；**本会话，不再委派**；真实会话 `ses_efbd900bdffeuzZcQBI7dDJ1WJ`） | 文档缺陷修复（无业务编号；README 构建 / 测试推荐命令可复制性） | T048 | README.md（构建 / 测试段 46–62）, docs/tasks.md（本行） | done（**主代理 2026-10-04 接受**；真实会话 `ses_efbd900bdffeuzZcQBI7dDJ1WJ`、返修 0/2） | 修复 README 旧 `-Command "$env:..."` 在父 PowerShell 先插值展开 `$env:` 导致语法错误：改会话内多行 `try/finally` 保存恢复 `ARK_LEFT_STATE_DIR`，推荐命令仅 `build.ps1 -OutputDir D:/ark_left/bin-v12`；仅最小 `apply_patch`；用 `[System.Management.Automation.Language.Parser]` 静态解析 snippet **0 语法错误**；**未运行 963 单测**、未执行真实 CLI / 应用进程 | 无 |
| T050 | GLM `tl-openai/glm-5.3-flash`（本会话，独立执行，禁止再委派） | interaction v0.13（UX021）详情快捷键 Ctrl+R 刷新 / Ctrl+C 复制摘要实施与验证 | T048、T049（均 done） | src/TrayApp.cs（PopupForm 构造 tooltip / ProcessCmdKey override / OnCopySummaryClicked 注释）, tests/QuotaTests.cs（RunTests 注册 / CopySummaryUX019Cases tooltip 修订 / 新增 UX021 用例）, src/Program.cs（0.13.0.0）, launch.ps1（默认 bin-v13、oldDir 加 bin-v12）, start.cmd（bin-v13）, docs/requirements/interaction-improvements.md（顶部 + v0.13 UX021 + UX019 覆盖说明）, docs/contracts.md（UX021 契约 + UX019 覆盖说明）, docs/tasks.md（T049 / 本行 / 短状态表）, README.md, docs/requirements/implemented-behavior.md, docs/setup.md, AGENTS.md（仅背景）, docs/README.md, docs/requirements.md, docs/requirements/README.md, docs/requirements/product-requirements.md（仅顶部索引）, docs/verification.md（顶部候选 + 文末 T050 自测） | in_progress（阶段1基线 / 契约 / 本行已先登记，实施与隔离自动检查进行中，待主代理验收；本会话返修 0/2、逻辑累计 0） | UX021 v0.13 冻结：详情 `Visible && Enabled && ContainsFocus && !IsDisposed && !Disposing` 且 `!_dialogOpen && !_menuOpen` 时，精确 `Control|R` 复用刷新按钮 Click（singleflight 不排队）、精确 `Control|C` 复用复制按钮 Click（纯 formatter / 固定安全文案 / 安全身份边界 / inline 反馈；无 snapshot 不写剪贴板但消费键、不触 query）；仅这两个精确组合识别，Ctrl+Shift / Ctrl+Alt 等走 base，不改 Esc；隐藏 / 未聚焦 / modal / 菜单打开走 base 不开窗；设置 ComboBox 本地按键归自身窗；无全局热键 / SendKeys；tooltip 加 Ctrl+R / Ctrl+C 不改 label / AccessibleName / footer 布局；UX019「click-only 写剪贴板」被增量覆盖为显式 click 或有效本地 Ctrl+C，自动打开 / 刷新 / 渲染仍零 copy；不改 10s / 5min / 缓存 / prefs 格式 1 / 1 / 2。**阶段1先登记基线 / 契约 / 本行再改源码**；真实 PID 9160 / bin-v12 未触碰，仅构建 bin-v13 | 无 |

| T052 | GLM `tl-openai/glm-5.3-flash`（本会话，独立执行，禁止再委派） | interaction v0.14（UX022）详情卡片化 card-only 弹窗实施与验证 | T050、T051（均 done） | src/TrayApp.cs（CardPanel 可聚焦 / 布局缓存失效；PopupForm 构造、尺寸贴合、详情右键菜单、状态行 / tooltip、移除 header / footer / 外框、ShowPanel / ApplyModelView / RenderCards、testhooks、smoke 迁移；TrayApp ShowDetails / 重定位）, src/Program.cs（0.14.0.0）, launch.ps1（默认 bin-v14、oldDir 加 bin-v13）, start.cmd（bin-v14）, tests/QuotaTests.cs（PopupDisplay / ActualRefresh / modal 焦点 / CopySummary / 快捷键 / CheckFooterLayout 迁移 + 新增 CardOnlyUX022Cases）, docs/requirements/interaction-improvements.md（顶部 + v0.14 UX022 + UX012/014/015/019/021 覆盖声明）, docs/contracts.md（顶部 + 卡片弹窗短契约）, docs/tasks.md（本行 / 短状态表）, README.md, docs/requirements/implemented-behavior.md, docs/setup.md, AGENTS.md（仅背景）, docs/README.md, docs/requirements.md, docs/requirements/README.md, docs/requirements/product-requirements.md（仅顶部索引）, docs/verification.md（顶部候选 + 文末 T052 自测） | in_progress（阶段1基线 / 契约 / 本行已先登记，实施与隔离自动检查进行中，待主代理验收；本会话返修 0/2、逻辑累计 0） | UX022 v0.14 冻结：左键详情弹窗只显示卡片本身——移除可见标题 / 刷新 / 设置 / 关闭 header、底部更新时间 / 复制按钮 footer、矩形外框、外围 padding 与固定 420dp 最小高度；单 card 窗口贴合 card 圆角与实测高度（Form Region 同 card 圆角），多 card 竖向 stack、10dp 间距仅 cards 之间、无尾部空白，超过 min(560dp, 工作区-16dp) 才纵向滚动；逻辑宽度 406dp clamp 工作区，100/150/200% DPI 与窄工作区无横向 overflow；高度按真实 layout 两 pass（全宽 measure → 溢出才扣真实滚动条重排），不做无限 resize 循环、不以周期 count 猜 scroll。空 / 错 / 未登录仍消息 card，未知不伪 0；singleflight / 身份 / cache / 10s / 5min / 打开零 query 不变。设置仍走圆圈 / 托盘（UX015 详情头部设置按钮移除），modal owner 与返回焦点 / scroll 不回退；详情 card 本地右键菜单仅「刷新 / 复制摘要 / 设置 / 关闭」四 action 复用既有 handlers（UX019 footer 复制按钮移除，复制改右键 / Ctrl+C），打开菜单零 query 且抑制失焦隐藏，复制仅快照时启用，共享 8 菜单项不变。Ctrl+R / Ctrl+C 精确组合条件不变（UX021），Esc / 失焦 / WM_CLOSE 继续 hide 并恢复自动隐藏圆圈；card 设 Selectable / TabStop 真正可聚焦，ShowPanel 激活后 focus card / 可聚焦内容，已 focus 不强制 reset；复制反馈为可见短 Tooltip（已复制 / 失败，约 2s），更新时间移 card tooltip，stale / cache / 身份未确认在首 card 内紧凑可见状态行（原位更新不重建）。初次 ShowDetails 先 final size 再 PrepareDetails；可见数据小 ↔ 大变化按当前圆圈重新定位不跳鼠标屏；同语义 / fetchedAt 变化不改尺寸 / 不重建 / 不 reset focus / scroll。跨 DPI 重开同步 padding / font / radius / 高度，同宽 DPI 变化 invalidate 布局缓存。**阶段1先登记基线 / 契约 / 本行再改源码**；真实 pid 15260 / bin-v13 / 真实剪贴板 / CLI 未触碰，仅构建 bin-v14 | 无 |

### 当前交互任务状态（T041–T052，2026-10-04）

> 说明：上方任务表中 **T-033 / T-034 / T-035 的 `in_progress` / `pending`，以及 T045 / T046 / T047
> 原行的 `in_progress`（待主代理验收）均为原提交状态**，按历史保留不涂改；其遗留实现范围已被后续
> 任务完成并由主代理 2026-10-04 独立验收接受：T-033（UX014 视觉）→ **T041**；T-034（UX015 设置实现）
> → **T042**；T-035（本地开发成品阶段）→ **T045**，持续迭代仍继续；T045 / T046 / T047 由主代理独立
> 验收接受。**不伪称原中断 writer（T-033 执行者 tl-openai/gpt-6-luna、T-034 无最终报告）已完成**。
> 下表为这些任务的**当前权威状态**，显式覆盖上表 T-033/34/35 与 T045/46/47 旧行状态，并显式覆盖 T052 上行巨型行内的 `in_progress` 旧状态：

| 任务编号 | 需求 | 实际实施者 | 主代理 2026-10-04 独立验收结果 | 状态 |
| --- | --- | --- | --- | --- |
| T041 | v0.6 UX014 视觉 / 布局 | GLM `tl-openai/glm-5.3-flash` | bin-v07 构建 3 exe、**538/0**、smoke 0、8 项交互全通过 | done（主代理接受） |
| T042 | v0.7 UX015 设置 modal | GLM `tl-openai/glm-5.3-flash` | bin-v07、**668/0**、smoke 0、8 项交互 | done（主代理接受） |
| T043 | v0.8 UX016 位置锁定 | GLM `tl-openai/glm-5.3-flash` | bin-v08、**732/0**、smoke 0、8 项交互（suffix d631e69d） | done（主代理接受） |
| T044 | v0.9 UX017 减少动画 | GLM `tl-openai/glm-5.3-flash` | bin-v09、**820/0**、smoke 0、8 项交互 | done（主代理接受） |
| T045 | v0.10 UX018 启动入口 | GLM `tl-openai/glm-5.3-flash` | bin-v10 build 3 exe、820/0、smoke 0、8 项交互、launcher **20 项 ok**（主代理复核 suffix 446bc758） | done（主代理接受） |
| T046 | v0.11 UX019 复制摘要 | ds41-writer（承接 GLM 异常交接） | bin-v11 build **906/0**、smoke 0、8 项交互（suffix 19ef96de）、launcher 20 项（suffix 8bfef04b） | done（主代理接受） |
| T047 | v0.12 UX020 悬浮窗归位 | GLM `tl-openai/glm-5.3-flash` | bin-v12 build 3 exe、首次 **959/0**、返修后最终 **963/0**、smoke 0；8 项交互 suffix 4562c4f9、launcher suffix 006feb38 | done（主代理接受） |
| T048 | 纯文档收尾（v0.12 状态） | ds41-writer（本会话） | 仅文档最小 `apply_patch`；未跑应用 / CLI / 网络 | done（主代理接受；返修 2/2、逻辑累计 2） |
| T049 | README 构建 / 测试命令可复制性修复（无业务编号） | ds41-writer（本会话） | 文档最小 `apply_patch`；Parser 静态解析 snippet 0 语法错误（未跑单测 / CLI） | done（主代理接受；真实会话 `ses_efbd900bdffeuzZcQBI7dDJ1WJ`、返修 0/2） |
| T050 | v0.13 UX021 详情 Ctrl+R / Ctrl+C 快捷键 | GLM `tl-openai/glm-5.3-flash`（会话 `ses_efbd524c7ffeShId1RAwrM6asQ`，已停止写入；返修 1/2、逻辑累计 1） | 主代理 2026-10-04 独立验收接受：主代理在最后测试修订后**实际重建** bin-v13（3 exe）、隔离单测 **1030/0**、隔离 smoke exit 0、verify-interaction 8 项全 pass（suffix 9d4a1d0b）、verify-launcher 20 项全 pass（suffix d3df298f）；未仅复用 GLM 自测；详见 verification.md 文末「T051 主代理 v0.13 独立验收记录」（上行 T050 巨型行内"自动检查进行中"由本行覆盖） | done（主代理接受；返修 1/2、逻辑累计 1） |
| T051 | 纯文档收尾（v0.13 UX021 状态；无业务编号） | ds41-writer（本会话，不再委派；真实会话 `ses_efba75eecffeKK16MP32JytHNq`） | 文档最小 `apply_patch`：记录主代理 2026-10-04 独立验收接受 T050（bin-v13 重建 3 exe、1030/0、smoke 0、8 项 suffix 9d4a1d0b / launcher 20 项 suffix d3df298f）、授权内本地升级 v0.13（PID 9160 → 15260）、`bin-v13\ark_left.exe` 0.13.0.0 / SHA256 `EE871EF2…ADE3`；`bin-v13/t051-sha256.csv` 已由主代理生成并校验（48 文件、0 mismatch）；未改源码 / 脚本 / 二进制，未跑应用 / CLI / 网络 / 真实进程；主代理已审阅接受（相对 links 0 broken） | done（主代理接受；返修 1/2、逻辑累计 1；T007 人工 / 系统 Gate 仍 pending） |
| T052 | v0.14 UX022 详情卡片化 card-only 弹窗 | 启动接续 GLM `tl-openai/glm-5.3-flash`（真实会话 `ses_efb1e82ceffeUTT35h1tuqx2u2`：launch 默认 / 白名单、start.cmd）；DS4.1 `tl-openai/deepseek-v4.1-flash` 修复（真实会话 `ses_efb187030ffepFLzeUgbDMfIXT`：TrayApp / tests，首次交付 0/2；原逻辑累计返修轮数无法可靠确认，不伪称清零） | 主代理 2026-10-04 独立验收接受：先 build.ps1 -OutputDir bin-v14 通过、首次单测 1079/10；DS 修复后构建 3 exe、单测 1088/0、smoke 0；主代理实际复跑 bin-v14 单测 1088/0（state review-ux022-state）、smoke ExitCode 0、verify-interaction 8 项全 pass（suffix 03102674）、verify-launcher 20 项全 pass（suffix 9015f7c6）；读取 preview-intro.png 确认合成卡片无 header / footer / 外围留白（合成图非人工点击）；launch.ps1 -OutputDir bin-v14 -PreviousProcessId 2268 → CLOSED 2268 exit 0、LAUNCHED 10600 `D:\ark_left\bin-v14\ark_left.exe`；详见 verification.md 文末「T052 主代理 UX022 接管独立验收记录」 | done（主代理接受；T007 人工 / 系统 Gate 仍 pending） |

### T-032 文档收尾交接

首次实现会话 `ses_efdbd9bfeffe1lmDxLSldwqm3a` 已停止，代码与测试已由主代理独立审阅接受；本次文档收尾执行者为 tl-openai/gpt-6-luna（用户明确指定），只记录主代理提供的验证事实，未重跑验证。独立检查仅审 visibility event forwarding、`UpdatePollInterval` 与 `PollIntervalUX013Cases`；未发现阻塞缺陷，未改源码 / 测试。主代理将在文档编辑完成后生成 `bin-v05/t032-sha256.csv`，目前不得视为已生成或已验证。候选未部署，正式 `bin` 仍 v0.2，`start.cmd` 不变。

说明：

- **T-001**：由 ds41-writer 创建文档骨架，主代理已逐文件读取并审查，结果记入
  `docs/verification.md` 文档检查记录（历史真实记录，保留）。
- **T-002**：业务需求已从"空骨架"更新为具体基线 `v0.1`；Q-001 经用户确认后关闭。
- **T-003（环境与真实数据验证）**：已完成。环境检查结论：无 dotnet SDK，存在
  `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5 / .NET Framework 4.8）。
  `build.ps1` + `csc` 编译成功；`ark_left-check.exe` 对真实 CLI 查询成功（输出脱敏）。
- **T-004（托盘 UI）**：已实现于 `src/Program.cs`、`src/TrayApp.cs`、`app.manifest`。
  证据：`ark_left.exe --smoke-test` 退出码 0 且布局断言通过；正常启动后存活。
- **T-005（额度适配器）**：已实现于 `src/Models.cs`、`src/QuotaParser.cs`、`src/QuotaCli.cs`。
  证据：`ark_left-tests.exe` 81/81 通过；真实 CLI 查询解析正确。
- **T-006（整合与自动验收）**：测试与集成文件已交付（`tests/QuotaTests.cs`、`check.ps1`、
  `test.ps1`、`start.cmd`）。主代理已独立执行并全部通过，状态 `done`：
  - `build.ps1` 通过；`test.ps1` → 81/81；`check.ps1` → `status=Ok`；
    `--smoke-test` → 退出 0（含布局与子控件边界断言、`bin/preview.png`）。
  - 修复轮加强：统一 CLI 认证闸门与结果判定、真实进程 `ReadToEndAsync` + 有界超时 +
    Dispose 同 gate、UI 打开屏幕约束与 CardPanel 宽度驱动布局、消息卡按文本测量可变高、
    item/period 空值 / 错误 / updated_at 解析、`--smoke-test` 布局断言与合成预览。
- **T-007（人工验证）**：**多屏 / 高 DPI / 托盘点击与右键菜单交互未做人工视觉验证**，
  独立列为 `pending`，由主代理或用户在有图形界面的环境中确认。T-006 完成**不等于**
  完全验收。
- **T-008（交互改进文档化）**：已完成，主代理已审查通过文档清单：
  - 新增 `docs/requirements/interaction-improvements.md`（v0.2，UX001–UX010）；
  - 更新 `docs/requirements/README.md`（需求索引加入交互改进层与设置指南）；
  - 更新本 `docs/tasks.md`（新增有序任务 T-009–T-020）。
- **T-009–T-019（交互改进实现）**：**已实现**，均有自动检查证据（见 `../verification.md`）：
  - 新增 `src/Identity.cs`（真实 auth/usage schema + SHA256 内存 scope + 友好身份提示）、
    `src/ViewState.cs`（`PanelModel` 状态机 / 进度视图 / 百分比 / 风险周期 / 相对时间）、
    `src/Ipc.cs`（命名事件唤起，可隔离测试）；改写 `src/TrayApp.cs`、`src/Program.cs`；
    重构 `src/QuotaCli.cs`（`QueryDetailedAsync` + `IProgress`，保留 `QueryAsync` 兼容）。
  - 事实核对：真实 `viewer.profile` 为**字符串**；scope 用 auth `active_profile`
    （name / owner_trn / region / project）。不展示 / 不落盘身份与 scope。
  - **UX001 竞态仍属真实 Explorer 未人工复现**，代码只提供单一隐藏机制，需人工验收。
  - **UX008** 以真实脱敏 JSON 核对字段（`viewer.profile` 字符串）；
    未见嵌套 `viewer.profile.name`，已按真实 schema 实现，无需回退。
  - **UX010** 保留绿色水滴图标，仅清晰化 tooltip；marker `%LOCALAPPDATA%/ark_left/first-run.done`
    仅含日期 / 版本，不含身份 / 额度 / 凭据；`--smoke-test` 与测试不写用户 marker。
  - **T-019** 已交付 `docs/setup.md`。
  - **开机启动 / 定时联网刷新 / 告警 / 复杂设置** 仍为范围外。
- **T-020（独立复核 + 文档同步）**：**主代理独立复核与自动验证本轮已通过 → `done`**：
  `build.ps1`（无告警）、`test.ps1` **245/245**、`--smoke-test` 退出 0、
  `tests/verify-interaction.ps1` 8 项 ok、真实 `check` 确认 `scope_verdict=Same` / `status=Ok`
  （脱敏输出，无 ID / 余额）。**T-007 人工视觉 / 真实托盘手感仍 `pending`**，
  自动证据与真实 Agent scope=Same 的确认**不替代**人工验收，也不代表其他套餐 / 托盘手感已验证。
- **T-021（复核修复轮）**：已实施并自测通过（构建无告警、单测 **245/245**、冒烟退出 0、
  集成脚本退出 0）；修正内容：子用户 scope 对 `is_root` 的 Mismatch、`owner_trn`
  region 槽 / user id 校验、面板复用绑定本次 pending scope、共享终态
  `ApplyFinalOutcome`（不依赖 Progress 顺序，取消未确认时先清缓存再 cancel）、
  `HideController.ConsumeToggle` 一次性消费、消除 `_firstRunBanner` 告警、
  首启 banner 冒烟断言 + `preview-intro.png`、`check` 输出 `scope_verdict` 且 Mismatch
  非零退出、`Check.Remaining` 改用 `PercentFormat`（`<1%` / 已用尽）、
   新增 `tests/verify-interaction.ps1`。T-020 已 `done`；历史 213 项测试记录保留。
- **T-022（文档规范更新，done）**：依据框架（绝对来源
  `%USERPROFILE%\multi-agent-development-framework.md`）第 7–9、11–12 节与本轮用户授权，
  建立 [`README.md`](README.md) 作为 docs 文档维护规范与索引，并在本文件补全任务 / 交接模板；
  同步 `AGENTS.md` 与 `docs/requirements/README.md`。本任务为**纯流程规范更新**，
  **不提升业务 `v0.1` / 交互 `v0.2`**，不改应用代码，不新增业务编号。
- **T-022 执行会话**：`ses_eff4a3361ffe0l9KetrFpHvPHD`。
  - **首次交付**后主代理独立读取四文件，主体符合要求，**合并反馈为第 1 轮返修**
    （**本会话 1/2、逻辑任务累计 1**）。6 条意见及修正结果：
    1. `docs/README.md` 第 12 行：`AGENTS.md` 定位改为**项目协作规则**（非全局配置）。
    2. 第 55 行恢复工作、第 88 行 DoD：补**受影响代理确认采用版本及任务影响**；
       DoD 并补**经主代理验收**。
    3. 第 99 行链接：指向本文件实际存在的「子任务 / 交接模板」中的 **DoD 条目**
       （不再指向不存在的章节）。
    4. 版本流程补**例外**：仅引用已有明确规则澄清可**保留基线版本**（须记录依据），
       见于本文件与 `docs/requirements/README.md`。
    5. 无 Git 证据口径：**不声称任务号 / 时间即可复现**，要求 **SHA256 清单 / 归档**；
       有 Git 但存在未提交变更须标 `dirty` + 快照，单 commit 不足。
    6. 本记录补**实际会话句柄**与轮次计数；文档检查证据落于本任务。
  - **主代理有条件接受**（仅限全部精确完成上述意见）；修正后自查通过，**标 `done`**。
  - **证据范围**：本任务的证据为**文档检查**（相对链接解析、内容与框架一致性核对，
    2026-10-03），**不包含、也未重跑**应用构建 / 测试 / 查询；不声称应用测试重跑。
  - 历史会话 / 返修数**未记录，不追溯虚构**。
- 真实启动 / 测试命令已确定并写入 `README.md` 与 `docs/verification.md`。

### T-023 开工记录（2026-10-03；**已中止，未完成**）

> **中止与句柄更正**：原记录写的执行会话句柄 `ses_2a1e0a9fTBD` 为**占位 TBD**，
> **真实会话句柄未取得，不伪造**（不写成任何具体 `ses_*`）。本任务**已中止**，
> **待 T-025 接续，不可标 `done`**；历史开工记录**保留**，下方为更正后的状态。
> 中止时的实际工作区证据见「T-023 中止核查」与 [`contracts.md`](contracts.md)
> §“持久快照契约（v0.3 冻结）/ 当前实现状态”。

- **执行会话**：**真实句柄未取得（原占位 TBD 不实，已更正；不伪造）**。
- **任务包边界**：用户本轮授权为**新交互需求基线 v0.3（UX011）**：打开仅显示持久状态、
  不触发查询；启动后台立即查询一次后每 5 分钟轮询；手动/定时/启动共用 single-flight；
  手动刷新保留界面、不显示加载/刷新中/慢响应/取消等过程状态、不清空数据、不重建面板等结果；
  失败保留上次成功数据及原时间，至多简洁反馈；首次无缓存用固定空态不假造 0；刷新按钮常驻；
  持久状态为跨重启本地快照（DPAPI 保护、原子写入、state 目录、隔离测试、损坏忽略）；
  界面简化（移除风险摘要/紧张周期/最低周期重复展示、冗余身份/说明/首启长 banner 等），
  保留套餐名称、各周期剩余额度/条、必要重置时间、简短最后更新时间、刷新/关闭。
- **需求版本**：交互层 v0.2 → **v0.3**（新增 UX011，历史 v0.2 项保留）；正式业务基线仍 v0.1。
- **依据**：用户最新授权（覆盖旧"无轮询"/"每次打开重查"）；主代理设计决定 1–4（持久状态
  技术选择、界面简化、测试覆盖）；契约仍为 `usage plan` v0.1，不新增业务数据范围。
- **写入范围**：见任务清单 T-023 行；不修改全局配置、不 git 提交、不联网真实查询、
  不启动正式应用、不杀现有实例（当时有真实实例 PID 16252 运行，`bin\ark_left.exe` 被占用）。
- **验证方式**：离线构建 + 单元测试（新增 UX011 用例）+ 隔离 `--smoke-test`（独立输出目录）
  + 隔离交互集成脚本（用独立编译输出，避免覆盖运行中的 `bin\ark_left.exe`）。
- **返修**：本会话 0/2（中止时首次交付前）；逻辑任务累计 0（**未交付，不构成 done**）。
- **风险**：DPAPI/路径在真实 Windows 用户目录未做人工视觉；多屏/DPI/托盘手感仍人工未测（T-007）。
- **状态**：**已中止（`in_progress` → 中止；待 T-025 接续；不可 `done`）**。

#### T-023 中止核查（主代理已读代码确认，2026-10-03）

- **写入范围文件清单**见任务清单 T-023 行；下列为中止时的实际实现进度与缺口。
- **已完成但未交付实现的部分**：
  - `src/PersistentState.cs`：缓存类、DPAPI、`Encode` / `Decode` / `Save` / `Load` /
    `Clear` / `ToCache` / `FromCache` 草稿**存在**。
  - `src/ViewState.cs`：`ShowSnapshot` / `ShowEmptyNoData` / `PanelView.FromCache` /
    `ShowingCache` 草稿**存在**。
- **已核实的缺口（阻断编译 / 未接入）**：
  1. `src/ViewState.cs`：`NoteStrong` 声明为 `string`，却被赋 `true`
     （`rv.NoteStrong = true;` `v.NoteStrong = true;`）→ **无法编译**。
  2. `src/PersistentState.cs`：缓存字段**未保存完整展示语义**（缺
     `Subscribed` / `SubscribedKnown`、产品级 `Error` / `Malformed` /
     `PeriodErrorPresent`；`FromCache` 一律置 `SubscribedKnown=true`）。
  3. `src/PersistentState.cs`：`FromCache` 在 `FetchedAt` 非法时**回退 `DateTime.Now`**
     （违反“非法日期不推现在”）。
  4. 缓存**未接入** `TrayApp` / `PanelModel`（无 `Load` / `Save` 调用），
     `PanelModel.ShowSnapshot` 无调用方。
  5. 未实现：启动后台一次 + 每 5 分钟轮询（含隐藏）、single-flight、
     打开路径零查询、等待期无过程态、语义相同不重建控件。
- **结论**：T-023 **中止**，上述内容由 **T-025 接续实现**，**不得**据草稿标 `done`。

### T-024 记录（2026-10-03，文档任务）

- **任务编号**：T-024；**性质**：需求契约（文档），**非系统交付**。
- **执行会话**：**新会话**（本文件写作者，ds41-writer）。
- **任务包边界**：仅写文档——`docs/requirements/*`、`docs/contracts.md`、
  `docs/decisions.md`、`docs/tasks.md`、`docs/README.md`（必要链接）；
  **不实现代码、不构建、不跑测试、不改 `src/` / `tests/`**；`README.md` / `AGENTS.md`
  仅在必要时纠正当前混合状态（本任务未改）。
- **接收 / 验收**：本任务交付物为**文档契约**，接收验收方式为**文档一致性 / 链接检查**；
  **本任务为文档，非系统交付**，**不构成系统交付 Gate 通过**，不代表 v0.3 已实现。
- **交付**：
  - `docs/requirements/interaction-improvements.md`：新增
    §「v0.3 交互层（UX011，当前权威，覆盖旧规则）」权威覆盖 v0.2；v0.2 历史保留并注明被替代。
  - `docs/contracts.md`：新增 §“持久快照契约（v0.3 冻结）”（格式版本 1）与“当前实现状态”。
  - `docs/requirements/product-requirements.md`：顶部交互层覆盖声明 + R004 / 数据规则 /
    未实现项 / 待确认处的 v0.3 覆盖标注（**不伪称基线一致**）。
  - `docs/decisions.md`：登记 **Q-005**（v0.3 技术决定）。
  - `docs/tasks.md`：T-023 标中止 + 句柄更正；新增 T-024（本任务）与拆串行 T-025 / T-026 / T-027。
  - `docs/requirements/README.md`：版本与索引同步 v0.3 / UX011。
- **返修**：本会话 **0/2**；逻辑任务累计 **0**（**逻辑 0/2**，本任务为文档，非系统交付）。
- **未决 / 待主代理复核**：见文末“T-024 交接与待主代理复核”。
- **历史返修数**：**未提供（未记录）**，**不编造**。
- **状态**：`done`（**文档任务**；不等于系统交付）。

### T-024 交接与待主代理复核

- **当前目标**：把用户最新交互意图冻结为 **v0.3（UX011）权威需求契约**，并登记
  T-023 中止、拆出 T-025 / T-026 / T-027，供后续新会话按序实施；**不实现代码**。
- **有效基线 / 契约版本**：业务 `v0.1`（R001–R004，数据范围不变）；
  交互 `v0.2`（UX001–UX010，历史）→ **`v0.3`（UX011，当前权威，覆盖旧冲突项）**；
  `usage plan` 解析契约 `v0.1`；**新增持久快照契约格式版本 1（冻结，未实现）**。
- **修改文件与符号**（仅文档）：
  - `docs/requirements/interaction-improvements.md`：v0.3 头、UX011 权威章节、
    Out of Scope 覆盖标注、概览表覆盖提示、状态与后续。
  - `docs/contracts.md`：§“持久快照契约（v0.3 冻结）”。
  - `docs/requirements/product-requirements.md`：顶部覆盖声明与 R004 / 数据规则 /
    未实现项 / 待确认处覆盖标注。
  - `docs/decisions.md`：Q-005。
  - `docs/requirements/README.md`：版本 / 索引同步。
  - `docs/tasks.md`：T-023 中止与句柄更正、T-024 / T-025 / T-026 / T-027。
- **已验证 / 未验证**：**仅文档**——Markdown 相对链接解析检查通过；
  **未构建 / 未跑单元测试 / 未跑冒烟 / 未做真实查询**。
- **失败及已尝试方案**：T-023 实现中止（编译阻断 + 未接入，见「T-023 中止核查」）；
  本会话不尝试修复代码。
- **未决问题**：无新增业务歧义（Q-005 由主代理授权技术决定，非用户指定值）；
  **T-023 真实会话句柄未取得**，照实标注占位、不伪造。
- **下一步**：主代理审阅本契约文档 → 新会话 T-025（缓存 / 轮询接入）→ 主代理复核 →
  T-026（展示简化）→ T-027（独立验证收尾）。**T-025 未完成前 T-026 不开工**；
  **后续代理须新会话，不并发写 `TrayApp.cs`**。
- **工作区现状与写入归属**：`src/PersistentState.cs` / `src/ViewState.cs` 存在
  T-023 未完成草稿（不可编译 / 未接入），由 **T-025** 接管；文档写入归属本任务 T-024。
- **待主代理复核**：UX011 规则是否完整忠实于用户意图与授权；持久快照契约是否可作为
  T-025 冻结输入；T-023 中止标注与 T-025–T-027 拆串行是否认可。

### T-025 首次交付记录（2026-10-03；待主代理 review）

- **执行会话**：新会话，Sol（`tl-openai/gpt-6.1-sol`）；ds41-writer 此次无响应 / 空结果、
  主代理检查未发生编辑，并已向用户说明 fallback。旧 T-023 已停止，依本次任务包接续；
  不伪造未取得的执行句柄。
- **采用基线**：已确认采用交互 **v0.3 UX011**、`contracts.md` 持久快照**格式 1**；
  业务 / 上游解析仍 v0.1。用户本次 brief 明确要求去掉破坏性 `File.Delete` 替换回退，
  Replace 失败保留旧文件，按该明确授权执行。
- **写入归属 / 修改清单**：仅 `src/PersistentState.cs`、`src/ViewState.cs`、
  `src/TrayApp.cs`、`tests/QuotaTests.cs`、`tests/verify-interaction.ps1`、`build.ps1`、本文件；
  全部手工修改为最小局部 `apply_patch Update`，未新增手写文件，未再委派。
- **数据控制**：启动加载 DPAPI 历史及不可逆 scope 归属；无缓存固定“暂无数据”；
  启动后台一次、300000ms Timer（隐藏时继续）；启动 / 手动 / 轮询共用 single-flight，
  在途触发立即返回、不排队。首启 / `--show` / 第二实例 IPC / 菜单 / 托盘打开均只 show。
  等待期不提交 UI；仅 AuthProgress 确认不同 known scope 可立即清内存 / 磁盘并提交空态。
- **身份 / 持久化**：最终 QueryOutcome 独立重建认证门控，一次 UI 提交，不依赖进度顺序；
  NotLoggedIn / 新 scope / usage Mismatch 清缓存，包含无 Last 时仍调用 eviction；
  普通失败 / 取消保留历史及原时间；Unknown 不替换已有历史，无历史受限显示且不保存；
  只有 Same + known auth 的 Ok / PartialError / NoSubscription 保存。
- **缓存修复**：保留原草稿已具备的订阅 known / 错误 / 数值 known / 时间字段；
  解码检查所有结构字段、类型、指纹及 known 数值；产品与周期错误语义、数值与时间往返；
  严格 round-trip 日期，非法 / 仅时分不推“现在”；未知时间文案为“更新时间未知”；
  保存前校验有效格式，Replace 失败不删除旧主文件。
- **T-026 接口**：`PanelModel.ShowSnapshot(snapshot, fingerprint)` 实际持有 `Last` +
  `ConfirmedFingerprint`；`BeginQuery()` 仅重置 pending；`CurrentView` 为现有视图；
  `CommitOutcome(QueryOutcome)` 返回一次终态 `PanelView`；`FromCache` 标历史来源，
  `NewData` 标新提交，`Persist` 标 scope 门控通过的可保存结果；`EvictPersistent` 接磁盘清除。
  `SnapshotController` 可注入 query / load / save / clear / commit / show；`Open()` 零查询，
  `Start()` 仅一次，`Refresh()` / `Poll()` 同门控；生产 Timer 与测试调用同一 `Poll()`。
  `TrayApp.ApplyFinalOutcome` 与控制器共用 `CommitOutcome`，form 仅消费 `ApplyModelView`。
- **实际验证命令 / 环境**：Windows、本机 Framework64 `v4.0.30319\csc.exe`，
  C# 5 / .NET Framework 4.8，无 SDK / NuGet；源码版本由下表 SHA256 标识。
  1. `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputDir bin-validation`：
     三个 exe 编译成功，无告警；构建增加 `System.Security.dll` 引用与可选 OutputDir。
  2. `.\bin-validation\ark_left-tests.exe`：**334 passed, 0 failed**，保留 245 条旧用例，
     UX011 冲突预期局部修订；新增缓存往返 / DPAPI 损坏 / 旧版本 / 结构与数值损坏 /
     日期 / 替换失败保旧 / 重启保留及身份删除 / single-flight / 打开计数 / 隐藏 Poll /
     终态门控 / 进度迟到与 generation / Dispose 后不提交。
  3. `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify-interaction.ps1 -OutputDir bin-validation`：
     **8 项 ok，退出 0**；脚本使用独立 state / IPC 后缀与不存在 CLI，仅停止自建 PID。
  4. `Start-Process .\bin-validation\ark_left.exe -ArgumentList '--smoke-test' -Wait -PassThru`：
     **退出 1，未通过**；源码旧 `finalhandler.unconfirmedCancel` 断言要求清历史且进入 Error，
     与 UX011 保留历史冲突。生成的 preview 图片仅为自动测试产物，不代表视觉验收。
  5. `Get-Process -Id 16252`：仍为 `D:\ark_left\bin\ark_left.exe`，未杀 / 重启真实实例，
     未覆盖 `bin` exe、未执行真实联网查询。
- **证据位置**：本记录为命令结果记录；测试实现与计数断言在 `tests/QuotaTests.cs`；
  构建产物与自动 preview 在 `bin-validation/`；本项目无 Git，下表保存本轮源码快照标识。

| 文件 | SHA256（最终构建 / 单测版本） |
| --- | --- |
| src/PersistentState.cs | AEC25602B7CEB691F3AA961A86B2E6AB02B692D0013CB2CAC8F015C2011C3F80 |
| src/ViewState.cs | A014CC245C6DDC08882CF15E36B7AD47700D5FF60B03F48A54F25C402008C8D7 |
| src/TrayApp.cs | 55634ED3AF2FE5CEABF84B3E021E53D5603B2C6127817D32ACA918F76CC1C4B6 |
| tests/QuotaTests.cs | 71D150B6EE432B2DD768F5BC046665C8AB75A4A72B5FD199DBDE21B0100B182E |
| tests/verify-interaction.ps1 | 0DF117E3631A4961E792B19512F964EB159B68C62A6B637AB74C16C8611B4155 |
| build.ps1 | 88A23F00D0B52A65F4D5D4619A7F2127C7392845E1F3603FDB001243678A1434 |

- **未测 / 后续**：真实账号查询、真实身份切换、多屏 / DPI / 真实 Explorer 手感未测；
  无重建闪动、界面简化、历史 / 最后更新时间展示与旧 smoke 适配归 T-026。
  打开计数证明在共用控制器 `Open()`，各入口接线已源码核对；实际托盘入口逐项计数与
  UI 焦点 / 滚动视觉证据仍供 T-027 独立复核，不把隔离 IPC 可见性检查当成全部计数证明。
- **返修**：首次交付，本会话 **0/2**，T-025 逻辑累计 **0**；T-023 历史计数保留，不追溯重写。
- **状态**：`in_progress`，待主代理 review；无新增业务决策阻塞，非 `done`、非系统交付。

## 状态流转

### T026 新执行会话首次交付（2026-10-03）

- **采用基线**：v0.3 UX011 + 持久快照格式1；业务 / 上游解析 v0.1 不变。
  用户本轮 brief 确认主代理 T025 独立代码审查、build `bin-validation` 与 334 项单测通过，接受数据控制部分进入 T026（T025 当时计数；T026 后当前源码为 368）。
  旧 T025 执行已结束；旧 smoke 取消清历史 / banner 预期在本轮适配，旧失败记录保留。
- **执行与所有权**：新执行会话 Sol（`tl-openai/gpt-6.1-sol`），ds41-writer 此前无响应且主代理已说明 fallback。
  直接写文件、未再委派；无工具会话句柄可记录，不虚构。
- **预先冻结范围 / 实际修改**：`src/TrayApp.cs`（form/UI、smoke、最小离线入口 hook）、`tests/QuotaTests.cs`、
  `README.md`、`AGENTS.md`（仅背景）、`docs/setup.md`、`docs/requirements/implemented-behavior.md`、
  `docs/requirements/interaction-improvements.md`（状态）、`docs/requirements/README.md`、
  `docs/contracts.md`（当前实现段）、`docs/decisions.md`（Q005 落实）、`docs/tasks.md`、`docs/verification.md`。
  授权但未修改：`src/ViewState.cs`、`tests/verify-interaction.ps1`。全部手改局部 apply_patch Update，无手写新文件。
  SHA256 清单为正常生成证据输出，位于 `bin-validation/t026-sha256.csv`。
- **行为 / 接口**：form 仅消费 PanelView，未改 controller 业务；卡片语义比较忽略 fetchedAt、未展示的 server 时间与用量字段；
  相同展示保留卡片及子控件，metadata / time / note 文本更新。变化先构建再统一替换，恢复合理滚动。
  按宽度去重 CardPanel Reflow，同尺寸 ShowPanel 不触发卡片重建。常驻真实刷新 / 关闭，无过程态 / banner / 风险汇总 / 紧张周期 / 冗余身份。
  订阅 known / 周期未知 / 桶错误保持明确文案，失败短 note 保历史原时间。
- **打开证据边界**：离线注入 TrayApp，逐项调用生产 QueueShow（首启与 --show）、OnExternalShow（IPC）、ShowFromMenu、
  MouseDown / OnTrayClick，分别断言可见及 query=0。实际第二进程 IPC 信号另由隔离脚本验证；不假称真实 Explorer 点击计数已验收。
- **自测**：命令、最终结果与快照 SHA256 见 `verification.md`「v0.3（UX011）T026 收尾轮验证」；无网络调用，
  使用相对 OutputDir `bin-validation`、隔离 state / IPC；不覆盖正式 bin、不 kill / restart 真实 PID16252。
- **未测 / 下一步**：T026 待主代理复核，T027 仍 pending 独立验证；Q005 保持 DECIDED，独立验证后才判断 CLOSED。
  T007 真实 Explorer 手感、人工视觉、多屏 / DPI / 键盘读屏 / 真实身份切换仍 pending，自动控件证据不替代人工验收。
- **返修**：首次交付，本会话 **0/2**，T026 逻辑累计 **0**；无新增 BLOCKED_DECISION。
  本记录与任务返回不是 done 或系统交付；主代理需审阅并传递接收结果。

### T026 接续 / 收尾执行记录（2026-10-03，ds41-writer 独立会话）

- **执行会话**：新会话，**ds41-writer（`tl-openai/deepseek-v4.1-flash`）已恢复**，
  直接写文件、未再委派；工具未返回可记录的会话句柄，**不伪造**。
- **开工核实**：旧 Sol T026 会话 `ses_efef06b8effe5ratFD5YaZFC3t` 返回空但**实际已落盘**
  `src/TrayApp.cs` / `tests/QuotaTests.cs` 及部分文档；旧执行**已结束**，无并发 writer，
  **未重做**。按共享记录**和实际文件**重新核实后接续，不复用未验证假设。
- **采用基线**：交互 **v0.3 UX011** + 持久快照**格式 1**；业务 / 上游解析仍 v0.1。
  主代理已复核 T025 数据控制部分（build `bin-validation` + 334/334 当时），接受进入 T026。
- **冻结范围 / 实际修改**：仅 `build.ps1`、`tests/verify-interaction.ps1`（绝对 `-OutputDir`
  correctness 修复）+ 证据 / 状态文档：`README.md`、`AGENTS.md`（仅背景）、
  `docs/README.md`、`docs/requirements/README.md`、`docs/requirements/interaction-improvements.md`、
  `docs/requirements/implemented-behavior.md`、`docs/contracts.md`、`docs/decisions.md`、
  `docs/tasks.md`、`docs/verification.md`、`docs/setup.md`。
  **未修改** `src/` 源码；已有文件全部最小局部 `apply_patch Update`，未整写、未新增无关代码。
  生成输出 `bin-validation/t026-sha256.csv` 为正常证据产物，非手改。
- **代码审查结论**：主代理关注点只读核对后**不无理由扩改**——`DisplayKey`（`src/TrayApp.cs`）
  与实际 renderer 字段同步（忽略 `fetchedAt`、未展示 server 时间与 used/total）；
  `CardPanel` 仅实际内部宽度变化时 `Reflow`，首次初始化顺序稳定；
  未知 / 桶错误不显示 0；smoke 覆盖真实 `RefreshRequested` → 生产刷新按钮与
  生产 `OpenEntryForTest` 打开路径（firstRun / --show / IPC / menu / tray 均 query=0）。
  详细逐项结论见 `verification.md`「T026 收尾轮验证 / 代码审查结论」。
- **实测命令 / 结果**（Windows，Framework64 `v4.0.30319\csc.exe`，C# 5 / .NET Framework 4.8，无 SDK / NuGet）：
  1. `build.ps1 -OutputDir bin-validation`：3 exe 编译成功、无告警；
  2. `bin-validation\ark_left-tests.exe`：**passed: 368, failed: 0**（历史 245 项保留）；
  3. `bin-validation\ark_left.exe --smoke-test`：**退出 0**；**未创建任何 state 目录**（不写 marker）；
  4. `tests\verify-interaction.ps1 -OutputDir bin-validation`：**8 项 ok，退出 0**，仅操作自建 PID；
  5. 绝对路径复测 `build.ps1 -OutputDir D:\ark_left\bin-validation` 及同名集成脚本：均退出 0
     （修复前 `Join-Path` 拼成 `D:\ark_left\D:\ark_left\bin-validation`，已实测确认）。
  无网络调用，未覆盖正式 `bin`，未 kill / restart 真实 PID 16252，未执行真实查询。
- **文档修复**：`verification.md` 原先只有旧 v0.2 记录，本任务**新增**「T026 收尾轮验证」
  并补充当前状态；`tasks.md` 原先指向不存在的「T026 本轮自测」，已修正为上述真实章节；
  测试数量由旧 334 / 245 如实更新为 **368** 并保留历史；`README.md` / `setup.md` 明确
  **正式 `bin` 仍为 v0.2、`start.cmd` 未改**，v0.3 需退出旧实例后从 `bin-validation` 运行
  或正式重建 `bin`，避免默认启动旧版却称新版。
- **未测 / 下一步**：真实账号查询、真实身份切换、真实 Explorer 手感、多屏 / DPI / 键盘读屏
  仍**人工未测**（T-007）；T026 待**主代理复核**，T027 仍 **pending** 独立验证收尾；
  Q005 保持 `DECIDED`，独立验证后才判断 `CLOSED`。**本记录与任务返回不是 `done` 或系统交付。**
- **返修**：本会话 **0/2**；T026 逻辑累计 **0**（历史未发生返修）；无新增 `BLOCKED_DECISION`。

### T030 首次交付执行记录（2026-10-03，ds41-writer 新会话）

- **采用版本**：业务 **v0.1** + 交互 **v0.4 UX012** + 额度缓存 **格式 1 不变**；
  悬浮选择存独立 `floating-settings.json`（版本 1，仅 product key + period label，无身份 / 凭据）。
- **执行会话 / 所有权**：`ds41-writer`（`tl-openai/deepseek-v4.1-flash`）直接写文件，未再委派；
  工具未返回可记录会话句柄，**不伪造**。无 Git。
- **冻结范围 / 实际修改**：新增 `src/FloatingQuotaForm.cs`；
  `src/TrayApp.cs`（运行接入 / `RunSmokeTest` 附近局部）；`tests/QuotaTests.cs`（新增用例 + 打开入口调整）；
  `docs/contracts.md`（新增 v0.4 悬浮窗选择与窗口契约）。**未改**业务接口 / 解析 / 身份 / 轮询 / DPAPI，
  未写 `docs/requirements/*` 索引 / README，未部署 `bin` / `bin-release`，未结束旧 PID 16252。
  `tests/verify-interaction.ps1` 经核对无需改动（默认入口改为圆圈后，旧 WM_CLOSE 语义仍成立）。
- **实现落点**：`FloatingSettingsStore`（严格版本 / 非空 / 限长校验，损坏按未配置，原子 Replace 保旧）；
  修复轮补充：`Load` 额外要求**字段数恰为 3**，多余 / 未知字段判为损坏按未配置；
  `FloatingSelection`（按接口原顺序默认首个已知有效百分比，不聚合 / 不最低；未知取首个可展示；缺失不暗换）；
  `FloatingCircleControl`（置顶无边框不进任务栏、椭圆 Region 真穿透、DPI 缩放、右下 16dp / 136dp、
  拖动约束工作区、拖动阈值不触发详情、失焦不隐藏、Esc / `WM_CLOSE` 只隐藏、仅 0<p<100 轻动画且隐藏停止、Dispose 释放）；
  `FloatingCircleControl` 修复轮补充：两行产品 / 周期短标题由各自可见行绘制、字体按 DPI 重建、
  100% 全填充且近 0 / 100 振幅收敛、`SimulateMouse*ForTest` 改走真实鼠标处理器；
  `FloatingSettingsForm`（ComboBox 列全部订阅产品 / 周期含状态，保存 / 取消；保存失败短提示不假装成功）；
  修复轮补充：设置弹窗字体按 `_scale` 缩放并随窗释放；
  `FloatingQuotaForm`（消费同一 `PanelView`、共享托盘菜单、零查询打开详情）；
  修复轮补充：`FloatingLayout` 纯几何选择 + `PrepareDetails` / `RestoreAfterDetails`，
  详情关闭经 `PopupForm.VisibleChanged` 恢复因布局暂隐的圆圈（用户显式隐藏不恢复）。
- **采用的重叠方案（修复轮更新，主代理授权）**：详情打开时**依次尝试左 / 右 / 上 / 下**，
  各位置先按工作区 Clamp 再校验是否仍不覆盖圆圈；**均无法容纳时暂时隐藏圆圈**，
  详情关闭后再恢复；用户**显式**隐藏的圆圈不被恢复（不改 `PopupForm` 固定尺寸）。
  已单测覆盖正常左侧、边界回退右 / 上 / 无空间暂隐与负坐标多屏。
- **本 writer 实测命令 / 结果**（Windows，Framework64 `v4.0.30319\csc.exe`，C# 5 / .NET Framework 4.8）：
  0. （修复轮重跑，最终结果）build.ps1 → 3 exe 成功；隔离 state 下 tests **passed 500, failed 0**；
     `--smoke-test` 退出 **0**（写出 `preview-floating*.png` / `preview-settings.png`）；verify-interaction **8 项 ok，退出 0**。
  1. `build.ps1 -OutputDir D:/ark_left/bin-v04`：3 exe 编译成功、无告警；
  2. `bin-v04\ark_left-tests.exe`（`ARK_LEFT_STATE_DIR=bin-v04/t030-writer-state` 隔离）：**passed 500, failed 0**（历史 368 保留）；
  3. `bin-v04\ark_left.exe --smoke-test`（隔离 state）：**退出 0**，未创建 state 内容，保存 `preview-floating.png` / `preview-settings.png`（及端点为 0 / 100 / 未知的预览）；
  4. `tests\verify-interaction.ps1 -OutputDir D:/ark_left/bin-v04`：**8 项 ok，退出 0**，仅操作自建 PID。
  无网络调用、无真实账号、未覆盖正式 `bin`、未 kill 旧 PID。
- **未测 / 下一步**：真实账号查询、真实 Explorer / 托盘手感、多屏 / 高 DPI / 键盘读屏仍**人工未测**（T-007）；
  T-030 待**主代理独立审阅与验证**；**未标 done、未关闭 Q-006**；本记录与任务返回不是完成或系统交付。
- **返修**：本会话**修复轮 1/2**；T030 逻辑累计 **1**；无新增 `BLOCKED_DECISION`。

### T030 返修轮 2/2 执行记录（2026-10-03，ds41-writer 同会话）

- **范围**：仍只同冻结范围（`src/FloatingQuotaForm.cs`、`src/TrayApp.cs` 局部、`tests/QuotaTests.cs`、
  `docs/tasks.md` 记录）；**未扩展**，`docs/contracts.md` 现有规则已准确，**本轮未改**。
- **主代理独立发现并授权的修正**：
  - A：`PrepareDetails` 从 `Screen.PrimaryScreen` 改为 `Screen.FromRectangle(_circle.Bounds)`
    （新增 `CircleWorkingArea()` / `CircleScreen()`）；**所有分支（含 HideCircle）都预置**一个
    非零、含详情尺寸、工作区 Clamp 的 `_pendingDetailsBounds`，修复首次无空间返回空矩形导致详情不可见。
  - B：`TrayApp.ShowDetails` 先 `_floating.ShowCircle()` 初始化 / 定位，再 `ShowPanel(circleScreen)`
    让 `PopupForm` 按**圆圈所在屏**完成真实尺寸，随后 `PrepareDetails(actualSize)` 再套 bounds；
    新增 `PopupForm.ShowPanel(Screen)` 重载（默认仍鼠标屏，旧调用 / 测试保留）；已 visible 时重复点击直接返回，不闪。
  - C：`ShowCircle` / `HideCircle` / 圆圈 Esc 与 `WM_CLOSE` 显式隐藏时清 `_autoHiddenForDetails`；
    `RestoreAfterDetails` 仅自动布局隐藏才恢复且跳过已 `Disposed` 圆圈，退出时不复活。
  - D：测试改走生产共享菜单 `PerformMenuForTest(1)` 打开真实 modal，Timer 驱动保存 / 取消；
    新增 `ApplyViewForTest`（零查询注入数据）与 `RunQueryForTest`（一次真实 controller 查询），
    断言保存后 circle 切到所选目标、`queries` 不增加（base=1 → 仍 1）。
  - E：修正测试 L373–388 缩进；`BuildFonts` 仅在新字体创建成功后才 Dispose 对应旧字体；
    端点 `preview-floating-100` 等经 `TrySaveBitmap(..., Region)` 透明角 clip（pixel 断言仍用 raw 位图）。
- **本 writer 本轮实测**（隔离 state，`bin-v04`）：
  `build.ps1 -OutputDir bin-v04` 3 exe 成功；`ark_left-tests.exe`（`ARK_LEFT_STATE_DIR=bin-v04/t030-writer-state`）
  **passed 518, failed 0**；`--smoke-test`（隔离 `t030-smoke-state`）**退出 0**、未创建 state 内容，
  `preview-floating*.png` 四角 alpha=0（透明）而中心 alpha=255；`verify-interaction.ps1 -OutputDir bin-v04` **8 项 ok，退出 0**。
  无网络 / 真实账号；未覆盖正式 `bin` / `bin-release`；未 kill 旧 PID；未部署。
- **保留历史计数**：首次交付 **465**、修复轮 1 **500** 均在上方记录保留，本轮为 **518**（新增 A–E 用例，不减旧例）。
- **返修**：本会话**修复轮 2/2**；T030 逻辑累计 **2**；无新增 `BLOCKED_DECISION`。
- **未测 / 下一步**：真实账号查询、真实 Explorer / 托盘手感、多屏 / 高 DPI 视觉 / 键盘读屏仍**人工未测**（T-007）；
  T-030 待**主代理随后独立构建验收**；**未标 done、未关闭 Q-006**；本记录与任务返回不是完成或系统交付。

### T031 独立验证与文档收尾记录（2026-10-03，主代理执行 / writer 仅记录）

- **范围**：仅文档收尾（`docs/verification.md`、`docs/tasks.md`、`docs/decisions.md`、`docs/contracts.md`）；
  **不改**需求索引 / 源码 / 脚本，**不跑测试**，**不新增维护**。本 writer 仅记录主代理已验证事实，**不承担独立审查**。
- **会话句柄（主代理提供）**：T031 实施真实 task_id `ses_efe2f613dffeGSAXSCdSlvtXng`；
  并行需求索引 writer `ses_efe38f31effeHEOl7GLl0mPVXz`。**T030 历史记录当时未取得句柄，按原文保留，不回填。**
- **采用版本**：业务 `v0.1` + 交互 `v0.4 UX012` + 额度缓存 `格式 1` + floating 设置 `格式 1`。新候选 `bin-v04`。
- **主代理实际执行（本 writer 未重跑）**：
  1. `build.ps1 -OutputDir D:\ark_left\bin-v04`：3 exe 成功、无告警；
  2. 隔离 `ARK_LEFT_STATE_DIR=bin-v04/t031-main-state` 执行 `bin-v04/ark_left-tests.exe`：**passed 518 / failed 0，退出 0**；
  3. `ARK_LEFT_STATE_DIR=bin-v04/t031-main-smoke-state`（预先不存在）+ `INSTANCE_SUFFIX=t031-main-smoke` 启动 `bin-v04/ark_left.exe --smoke-test`，`WaitForExit(20000)`：**exit 0，未创建 state**；合成预览 circle 75 / 0 / 100 / unknown 及 settings，**不构成人工桌面验收**；
  4. `verify-interaction.ps1 -OutputDir D:\ark_left\bin-v04`：**8 项 ok，退出 0**，仅自建 PID，不联网 / 无真实 CLI；
  5. 应用 SHA256 `4F4DE91A4E18FA8677C996102FA545FEB8D02234686842221F339DA034277ED3`；版本清单 `bin-v04/t031-sha256.csv`（主代理生成）。
- **主代理核对**：T027 清单仅 `src/TrayApp.cs`、`tests/QuotaTests.cs`、`docs/tasks.md`、`docs/decisions.md`、
  `docs/requirements/interaction-improvements.md`、`docs/contracts.md` 有既定修改，另加新 `src/FloatingQuotaForm.cs`；
  其余 baseline 文件（含 `bin`、`bin-release`）未改（后续文档同步会再变，源码其余不变）。
- **进程说明**：本轮未停止 / 替换用户进程或正式 `bin`；主代理当前查旧 PID 16252 **已不存在**（原因未确认，
  **不假装仍运行**），未改 `start.cmd` 默认 `bin`。用户如有旧实例，退出托盘后运行 `bin-v04\ark_left.exe --show`。
- **审查覆盖**：真实生产鼠标 down / move / up 拖动阈值无误开、双窗口同 `PanelView` 且身份清理同步 / unknown no 0、
  0 空 / 100 满水位、配置选择加载 / 缺失保留 / 坏版本 extra 字段 / 原子失败保旧、真实共享菜单 settings 保存 / cancel / 0 query、
  第一次静默菜单打开初始化 circle / 详情按圆圈所在 screen 与实际 size / 窄屏非零 fallback 及 auto-restore rule；
  原 UX011 single-flight / 后台 poll / cache stability 保持。**人工真实 Explorer / 多屏 DPI / 键盘读屏 / 真实账号切换仍 T007 pending，系统 Gate 未过，不称系统交付。**
- **结论**：`T029 / T030 / T031 done`（主代理接受），`Q-006 CLOSED`（T031 闭环）。

```
pending → in_progress → done
              ↘ blocked_decision → (有权决定已记录 DECIDED 且受影响代理确认采用版本) in_progress
```

- `blocked_decision`：存在影响该任务的未决问题，暂停相关范围；问题登记到
  [`decisions.md`](decisions.md)。只有在有权者确认并将问题记录为 `DECIDED`、且受影响代理
  确认采用版本后，才恢复 `in_progress`；等待期间可做不依赖该问题的独立工作。
  **跨会话仍阻塞**：改开新会话不使 `BLOCKED_DECISION` 变为已批准。
- 问题 `CLOSED` 需在决定落实且实施验证通过之后，属于完成条件，**不是**恢复工作的前提；
  恢复工作不依赖 `CLOSED`，因此不构成死锁。
- `done`：满足验收，且影响本任务的问题已关闭；证据必须指向真实执行的检查，
  未执行或不适用的检查显式标注，不记为通过。
- 任务返回不等于任务完成，关键问题未关闭不得汇总为成功。

## 会话生命周期（框架第 9 节）

- **逻辑任务编号 `T-XXX` 与执行会话 ID 分离**：同一逻辑任务可由多个执行会话接力承接，
  接力不改变逻辑任务身份；工具返回的会话 `task_id` 是恢复会话的句柄，**不是**逻辑任务编号。
- **一个执行会话默认只承接一个预先对齐、可验收的有界任务包**：开工前对齐逻辑任务编号、
  需求编号、范围边界、验收标准与交付物；强耦合任务仅在**开工前**明确合并才同包。
- **任务包不可事后扩展**：开工后不得以"同一模块 / 顺手再改一处"为由追加范围外工作。
- **返修预算**：每个执行会话首次交付后默认**最多 2 轮**返修（一次"合并反馈 → 执行"计一轮，
  纯信息澄清不计入）；不得把实现返修伪装成澄清。达到上限仍失败时，由主代理先分析原因、
  重新拆分，必要时更换承接角色或补充对齐，再**开新会话**。
- **禁止仅换会话重置计数**：同时保留**当前会话返修轮数**与**逻辑任务累计返修轮数**；
  新会话单独计轮但不清零逻辑任务累计，失败记录跨会话保留，开新会话前须说明调整后的方法或拆分依据。
- **验收后开新会话**：完成验收后不再向同一会话追加任务；新增任务、或已验收后才发现的新缺陷，
  另开新会话承接。范围内的局部修正、补信息、`BLOCKED_DECISION` 归位可复用原任务包 / 会话。
- **`NEEDS_HANDOFF` 不等于完成**：子代理到阶段边界或上下文可靠性下降时，在可安全停止边界
  返回 `NEEDS_HANDOFF`（表示需交接继续），并给出进度、已验证 / 未验证与下一步。
  无法准确获得 token 用量 / 上下文余量时**不伪造用量、余量或阈值触发结论**，改用可观察信号（反复探索、遗漏约束、
  方案混淆、开始臆测接口或需求）判断；上下文预算 / 预警与交接阈值、检查时点与不可见指标详见 [development-plan.md §9](development-plan.md#9-t-039-子代理上下文控制)。
- **新会话前旧执行收尾**：开新会话前原执行须已结束或确认停止，不得新旧会话并发写同一文件；
  状态持久化到共享记录（当前目标、有效基线 / 契约版本、修改文件与符号、已验证 / 未验证、
  失败与已尝试方案、未决问题、下一步、工作区现状及写入归属）。
- **新会话核实**：开工前按上述记录**和实际文件**重新核实（记录可能与工作区不一致），
  再重新对齐范围与验收标准，不复用未经验证的假设。
  核实仅限当前任务相关记录 / 文件 / 符号，先搜索后局部读取；历史保留不等于全量重读，疑点按需扩大并注明原因；证据要求见 [README.md §5](README.md#5-验证证据要求)。

## 通信与交接（框架第 11–12 节）

- 无实时通信能力时，子代理把问题与上下文交给**主代理中转**；`BLOCKED_DECISION` 返回后由
  主代理确认并恢复原会话，或新开会话并传入交接记录。
- **共享文件不是通知渠道**：写入共享文档不自动通知或唤醒他人，不能假设对方已看到；
  关键结论与决定须由主代理明确传递并检查接收确认。
- **信息状态不是批准**：消息的送达 / 回复状态（SENT / DELIVERED / READ / REPLIED）只描述投递，
  **不代表同意**；问题状态 `OPEN → DECIDED → CLOSED` 独立推进，`DECIDED` 是已有答案，
  `CLOSED` 才表示决定落实且验证通过。无回复不得默认批准，不设超时自动通过。
- 子代理间直接通信仅限事实查询、既定契约联调、依赖通知、缺陷复现与验证反馈；
  契约 / 业务 / 验收 / 范围变更仍须主代理协调，涉及业务含义时升级。

## 子任务 / 交接模板（框架第 7、9 节）

**适用范围**：下列完整模板用于**复杂任务、交接与再委派**；目标明确、影响局部、无关键契约 / 业务歧义的小任务优先**短任务单**（仅目标、可写范围、验收方式 + 必要事实），默认剩余委派层数 `0` 由接收方直接执行，不硬设文件数 / 行数门槛，固定纪律自动继承；唯一项目细则见 [development-plan.md §11](development-plan.md#11-t055-短任务优先派发)。

下发任务或交接时至少包含：

- **逻辑任务编号**：T-XXX（区别于执行会话 ID）
- **执行会话标识**：会话 ID / 句柄，或"新会话"
- **任务包边界**：本会话承接范围的上限（含开工前合并说明；不得事后扩展）
- **返修**：本会话已用轮数 / 上限（默认 2）；逻辑任务累计轮数
- **对应需求编号 / 版本**：
- **负责范围（做什么 / 不做什么）**：
- **输入**：接口契约、数据模型、依赖交付物、精确章节 / 符号 / 有效版本、证据来源及实际执行者
- **上下文预算 / 来源 / 可观测性 / 检查点**：所选模型 context 上限 `C` / 输出预留 `O` / 收尾余量 `B` 与来源；预警 / 交接阈值；派发前、阶段交付、扩大读取与返修前检查；当前用量可见性（可信来源或"不可见"）
- **输出**：交付物路径 / 接口签名
- **异常与边界**：
- **验证方式**：需通过的单元 / 模块 / 集成测试；本会话实际执行检查与仅记录主代理结果分开写
- **修改文件清单与所有权**（避免冲突）：
- **完成定义（DoD）**：满足框架第 8 节全部条件并经主代理验收；任务返回 / 交接不等于完成
- **阻塞与依赖**：需要谁先完成
- **问题编号与状态**：Q-XXX / OPEN、DECIDED、CLOSED
- **主代理接收问题的渠道**：
- **子代理直接通信渠道 / 依赖代理**（如有）：
- **再委派**（如有）：有限剩余委派层数（下级减 1）、下级唯一子任务标识 / 父子关联、严格小于父范围的边界、文件所有权与停写交接；详见 [development-plan.md §10](development-plan.md#10-t-054-有界再委派规范)
- **共享记录位置**：关键结论、决定与版本写入何处
- **交接记录路径**：状态持久化文件位置（见下方交接要点）
- **关键结论同步**：需同步给哪些受影响代理、何时确认采用版本
- **决策权限**：内部实现可自主决定；业务与契约歧义须升级
- **恢复条件**：决定已记录，受影响代理已确认采用的版本

**交接记录最少包含**：当前**目标**、有效**基线 / 契约版本**、**修改文件与符号**、
**已验证 / 未验证**部分、**失败及已尝试方案**、**未决问题**、**下一步**、
**工作区现状与写入归属**。

**另须包含**：读取窗口摘要、上下文预算与当前度量（或"不可见"）、触发 `NEEDS_HANDOFF` 的原因；会话与逻辑任务累计返修轮次。若发生**有界再委派**，另记下级真实 session / 结果 / 已测未测 / blocked 的逐级汇总；细则见 [development-plan.md §10](development-plan.md#10-t-054-有界再委派规范)。

> 对齐要求：子 Agent 需能用自己的话复述"需求编号 + 范围 + 验证方式"才算确认，
> 而不是仅回复"收到"；本模板与规范的一致性由 [`README.md`](README.md) 约束。

## T053 / T054 有界再委派流程记录（2026-10-04，纯规范 / 静态配置，主代理已接受）

> 状态：`done`（**仅规范 / 静态配置验收**，非真实嵌套运行或产品交付）。T053 / T054 均由 ds41-writer 执行；主代理已独立审阅并接受规则与静态解析，**真实嵌套委派未测**（运行会话需退出重启加载）。任务表 / 现有 T041–T052 / T007 / 系统 Gate 状态不变。

- **T053（全局有界委派规则；六全局文件）**：范围 `%USERPROFILE%/.config/opencode/file-writing-policy.md` + 4 个 agent definition + 外部框架 `%USERPROFILE%\multi-agent-development-framework.md` **§7 / §9.2**；**本次未改 `opencode.json`**。由 ds41-writer 执行，真实 session `ses_efb39863effeq03HLF4D1hI5O3`，**返修 1/2、逻辑累计 1**（主代理最终审阅全局 policy 23–30 / 70–101、4agent body 修改窗口与 framework §7 / §9.2 后接受）。白名单说明已修正为 **leading deny**、**each** 子任务；只改说明、未改真实权限顺序。
- **T054（项目再委派规范；六项目文件）**：范围 `AGENTS.md`、`docs/development-plan.md`（§10 为唯一详细规范）、`docs/tasks.md`、`docs/README.md`、`docs/decisions.md`、`docs/verification.md`。由 ds41-writer 执行，真实 session `ses_efb3984f7ffeovDIoE717zejZp`，**返修 2/2、逻辑累计 2**（第 2 轮收尾，非新任务、未清零）。规则要点：层数为**非负整数**，`>0` 才可派下级 `depth-1`、`=0` 只能直接执行；所有层新 session、最多 2 轮有限返修与逻辑累计保留；派发须给父子关联 / 层数 / 范围 / 路径符号窗口 / 交付物 / 所有权 / 验收 / 输出摘要；父 / 子 / 兄弟不并发写相同文件，交出写入权方停写并在收回审查交接后恢复。
- **检查证据（主代理实际执行，仅录事实，非 writer 自测）**：2026-10-04，Windows PowerShell **5.1** + `opencode` **1.18.18**。主代理执行 `opencode --version` 与 `opencode agent list`，均退出成功（完整输出 `%USERPROFILE%/.local/share/opencode/tool-output/tool_104c891ad001lnAVznfKkPlahA`）；以 **`ConvertFrom-Json`** 解析各 agent 权限，断言四个 writer 各 **7 条 `task` 规则、首条 `'*': deny`、后续 6 指定 allow，六个 target 均已注册、last matching action 均 allow**，输出 `4/4 permission checks passed; no model requests made`。初次 **Node 管道**验证因 **Windows 引号**失败（`subagent` 误作命令），改用纯 PowerShell 后全绿；**未伪造初次通过**。**model 未改，未发任何 model / 业务调用**；**真实嵌套委派未测**，配置启动加载需退出重启。
- **生成证据计划**：`bin-validation/t053-t054-sha256.csv`（覆盖本轮 **12 个**修改文件）由**主代理在最终收尾后生成**；**本 writer 记录时尚未生成**，实际结果以产物与主代理最终核对为准。

## T055 短任务优先派发（2026-10-04，纯规则同步，主代理已审阅接受规则设计）

> 状态：`recorded`（主代理 2026-10-04 已审阅接受**规则设计**，**仅文档静态审阅**、**重启后实际运行未验证**；非产品交付）。用户本轮批准"小任务优先简短派发"。
> 范围（11 文件 = 8 项）：全局 `file-writing-policy.md` + 4 个 agent definition + 外部框架 `multi-agent-development-framework.md`；项目 `AGENTS.md`、`docs/development-plan.md`（新增 **§11 唯一项目细则**）、`docs/tasks.md`、`docs/README.md`、`docs/decisions.md`（新增 **Q-008**）。**不改业务 / UI / 契约 / 源码，不跑应用，不重启 OpenCode，不改 JSON。**
> 由 ds41-writer 直接执行，真实 session `ses_efafe59a7ffegKpj218dji30pb`，**返修 1/2、逻辑累计 1**（剩余委派深度 `0`）。规则要点：主代理先按复杂度分流；小任务用短任务单；默认剩余层数 `0` 直接执行，省略层数不阻塞；仅明确允许再委派才需显式非负预算且 `>0` 才能下派；复杂 / 交接 / 再委派用完整模板；固定纪律自动继承；需求 / 契约实质变化仍走基线。**Q-008 仅限此次规则落盘 + 文档静态审阅为 `CLOSED`；重启后实际运行未验证；Q-007 状态不变。**
