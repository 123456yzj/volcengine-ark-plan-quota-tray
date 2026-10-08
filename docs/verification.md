# 验证与验收

## v0.24.0 主题色设置（2026-10-08）

- 新增主题强调色预设与深色内容面：`Theme.cs` 色板模型、`UiStyle` 动态色板门面与 `Apply`、设置弹窗主题区域（实时预览 + 保存提交 + 取消回滚）、偏好格式 3 与旧格式内存迁移。默认主题（索引 0 + 浅色）与历史常量逐字节一致，既有像素断言不变。
- 隔离全量测试 **2966 / 0**（上版 2897，含新增 UX029 用例；`bin-release` 载入已校验 bootstrap 后运行）。`build.ps1 -OutputDir bin-release` 通过；`prepare-bootstrap.ps1` 校验两架构官方 ArkCLI 摘要与签名通过。
- 专项入口 `--theme` **302 / 0**；受影响既有套件 `--menu-interaction`、`--compact-ui`、`--effective-quota`、`--floating-amount`、`--floating-antialias` 全通过。UX015 短内容滚动与 Tab 序断言随新增主题控件更新为新基线。
- `package.ps1` 生成候选安装包；`verify-installer.ps1` 通过静默安装、payload、注册与开始菜单、安装后启动 / 隐藏 / 单实例，以及旧随包组件清理与用户状态保留、卸载。加入发布说明后重新打包，源码未变化，复用 **2966 / 0** 全量结果；最终包安装验收再次通过，本轮候选与最终包合计 **2 次**。安装包约 2.10 MB，SHA-256：`885a0e6ee789ae230c011c8b79214445f69dc91a83e7df3472b4b1798adb4918`。
- 本轮未执行真人浏览器授权、全新 Windows、多屏、读屏或 ARM64 原生人工验收；合成 WinForms 测试不替代真人视觉验收。安装包尚未代码签名。

## v0.23.0 轻量安装包（2026-10-08）

- 移除随包两架构 ArkCLI、生产托盘 Runtime 管理器 / 维护计时器和组件诊断入口。当前构建不依赖 bootstrap，旧 CLI / Runtime 兼容代码继续参与开发回归。
- `package.ps1` 构建通过；隔离全量测试调用 **1 次，2897 / 0**。新增关于窗口不含 ArkCLI 入口的断言；直连登录、查询、取消及状态保存的既有合成测试通过。`build.ps1 -OutputDir bin-v23` 与 `verify-launcher.ps1 -OutputDir bin-v23` 通过。
- 候选安装包验收调用 **1 次**，通过安装、无随包 Runtime、快捷方式、安装后启动 / 隐藏 / 单实例唤起、卸载和用户状态保留。升级测试在安装目录创建旧两架构 exe 夹具，覆盖安装后确认删除；额外用户文件保留。候选日志：`bin-installer-ef8696ae9dd4494aa6cee370c9ba18cc`。
- 安装包由 v0.22.0 的约 20.65 MB 降至约 2.19 MB，减少约 89.4%。本轮未重新执行真人浏览器授权、真实旧版安装包到新版的完整覆盖、全新 Windows、ARM64 原生或多屏人工验收；仍依赖 .NET Framework 4.8。
- 加入发布说明后重新打包，源码未变化，复用 **2897 / 0** 全量结果；最终包安装验收调用 **1 次**通过，本轮候选和最终包合计 **2 次**。最终日志：`bin-installer-dd739047b5744aa18fe9c1f475372aac`；安装包 SHA-256：`77e12a0625ed1ecf9207744e6a6ec7eb480c3befa8cd15d94f7b29bc1d58b953`。

## 隐私清理与公开检查（2026-10-08）

- 当前文档及全部 Git 历史中的本机用户名路径、真实个人档位和用量数值已清理；源码、测试和二进制文件对象的历史内容未改动。Gitleaks 初扫 21 个提交未发现凭据；清洗后候选及远端重新克隆复扫均未发现凭据，远端 23 个提交 / 296 个独立对象未再命中已识别隐私。具体范围见 [隐私检查](privacy-review.md)。
- v0.21.0 / v0.21.1 / v0.22.0 清理版安装包分别构建并各验收一次，安装、覆盖、卸载和安装后单实例交互均通过。三个远端附件与本地 SHA-256 一致；本轮只调整文档和忽略规则，复用原源码验证结果。
- 仓库已设为 PUBLIC。`ark_left-tests.exe --app-update-live` 对真实匿名 GitHub 接口调用 **1 次，1 / 0**，输出 `github_latest=0.22.0 status=Available`（测试当前版本为 0.21.0），验证正式版本及安装包元数据；匿名安装包请求返回 **HTTP 200**。初次发布时的 404 限制已解除。
- 最新安装包匿名完整下载首次在 45 秒超时，收到约 9.8 MB / 20.6 MB（curl 退出 28）；一次有界断点续传在连接 8 秒后超时（退出 28），本轮未完成下载后的字节级复验。三个发布附件的 GitHub SHA-256 摘要已与本地文件匹配。

## v0.22.0 应用更新检测（2026-10-08，带联网限制发布）

- 应用更新客户端、启动 / 每 24 小时调度、关于窗口手动检查、版本提示和下载入口已实现。首次专项调用 `bin-v22\ark_left-tests.exe --app-update` 为 **50 / 0**；补充数字 patch 比较、未就绪资产、浏览器失败、窗口关闭及退出后迟到结果用例后，最终 `package.ps1` 构建与 `bin-release` 隔离全量调用 **1 次，2896 / 0**（上版 2835 项，不累加专项数量）。
- `build.ps1 -OutputDir bin-v22` 与 `verify-launcher.ps1 -OutputDir bin-v22` 通过。真实安装包验收调用 **2 次**：候选包及加入私有仓库限制说明后重新生成的发布包，均通过静默安装、文件与发布构建字节一致、快捷方式、安装后启动 / 隐藏 / 单实例唤起、同目录覆盖安装、卸载及用户状态保留。最终日志位于 `bin-installer-5b4df48171b6457d8dd7181dbcf8ca30`；源码未变化，复用 **2896 / 0** 全量结果。最终安装包 SHA-256：`87d232a3b9fab9603419f0b096cbc3d18e134195807e37b4d8d6a7a35c7f4e5c`。
- 联网检查调用 **1 次**：`bin-release\ark_left-tests.exe --app-update-live` 退出 **1**，**0 / 1**，期望 Available，实际 Failed。诊断确认 `gh repo view --json visibility` 返回 **PRIVATE**；无认证 HEAD 与 .NET GET 最新发布接口均返回 **HTTP 404**，已登录 `gh release view v0.21.1` 可读取正式 Release 和已上传安装包。因此不是版本比较通过即可交付的问题，匿名更新源尚不可用。
- 用户明确要求“先发布再说”，授权保留私有仓库并先发布当前版本，发布说明披露匿名更新接口 404；公开发布源或私有鉴权后续处理，联网验收仍未通过。真实系统 balloon、实际浏览器启动、自然等待 24 小时和跨电脑升级仍未人工验收；合成 WinForms 与通知意图测试不替代这些检查。

## v0.21.1 圆圈高 DPI 文字修复（2026-10-08）

- 截图问题为圆圈 `100%` 丢失百分号、`50000 AFP` 变为科学计数后仍被省略。定量复现：196px 圆圈、163px 额度区、175% 显式字体缩放下，Point 字体在 96 DPI 表面测得百分比宽 149.24px、额度宽 103.79px；在 168 DPI 表面分别变为 261.18px 与 181.64px，均超出区域。目标电脑的实际 Windows 缩放比例仍未提供。
- 圆圈改用 Pixel 字体，保持 96 DPI 下的字号并只应用一次显式缩放；百分比按实际测量的宽高适配矩形，不换行。额度使用与 DrawString 一致的 GDI+ 测量，超长数值保留科学计数与完整 AFP 单位；标准离屏绘图表面明确为 96 DPI。
- 新增 5 个布局缩放（100 / 125 / 150 / 175 / 200%）× 5 个绘图 DPI（96 / 120 / 144 / 168 / 192）检查，验证 `100%` 与 `50000 AFP` 的宽高及绘制像素不受绘图 DPI 二次放大。首次专项调用 **197 / 25**，暴露旧百分比区域高度不足；补充实际宽高适配后，候选构建 `build.ps1 -OutputDir bin-dpi-fix` 与全量测试 **2835 / 0** 通过。已查看 `preview-circle-dpi-175.png`，完整显示 `100%` 和 `50000 AFP`。
- 最终 v0.21.1 `package.ps1` 构建通过，`bin-release` 全量 **2835 / 0**；本轮专项调用 1 次、全量调用 2 次，结果分别报告。真实安装包验收调用 1 次，通过静默安装、快捷方式、安装后启动与单实例唤起、同目录覆盖安装、卸载和用户状态保留。安装包 SHA-256：`d060765b6647afde071014caf81f8265405051b2980ae622b441e995f5c5e332`。
- 上述是模拟 DPI 与真实 WinForms / 离屏渲染检查；尚未在用户反馈的那台电脑上安装并人工复测，也不替代混合 DPI 多屏拖动、系统文本大小偏好或字体缺失环境的验收。

## v0.21.0 Windows 安装包发布检查（2026-10-08）

- 新增 `package.ps1` 与 `setup.iss`，使用经 Authenticode 验证的官方 Inno Setup 6.7.3 编译器制作按用户安装的 exe。包内文件采用明确清单，包含主程序、诊断工具、amd64 / arm64 ArkCLI bootstrap、第三方许可和文档，不包含单测程序、本机凭据与测试状态。生成 `SHA256SUMS.txt` 供下载校验。
- 隔离全量单测调用 **6 次**，首轮 **2667 通过 / 18 失败**，最终 **2685 项断言通过、0 失败**；数量不累加。修正缩小圆圈后仍按旧尺寸预期隐藏的布局夹具、透明边界处的旧色彩采样；测试程序使用与主程序相同的 DPI manifest。
- 剩余 12 项原生命中失败单独运行无法复现（**72 / 0**），接在直连登录用例后可复现（**313 / 12**）；失败记录包含焦点、鼠标、显隐、窗口边界、原生 style / exStyle、命中 HWND / PID 与事件顺序，命中 PID 属于 Edge，但不将浏览器视为已证明的原因。补充消息泵未解决问题；将该原生窗口夹具隔离到新的 STA 线程后，同一登录前置路径 **325 / 0**。本轮专项诊断调用 **4 次**，另一个额度绘制前置检查 **124 / 0**；这些结果分别报告，不当作全量断言数。没有修改应用渲染或登录行为来绕过检查。
- `tests/verify-installer.ps1` 在 Windows 11 x64 的独立安装目录检查真实静默安装、完整 payload、exe 与构建产物字节一致、HKCU 卸载登记、开始菜单 `--show` 入口、同目录覆盖安装、卸载，以及用户状态保留。首次因 `DisableProgramGroupPage=yes` 忽略测试的 `/GROUP` 而未找到预期入口（脚本退出 1）；日志确认入口实际创建在默认组，清理该次安装后调整为 `auto`，检查通过。安装后的应用通过 `verify-interaction.ps1` 的启动、关闭后隐藏、单实例唤起和静默驻留路径。
- 未执行真人安装向导点击、升级时正在运行的程序自动关闭、缺少 .NET 4.8 的 Windows、全新 Windows 虚拟机、ARM64 原生执行、代码签名或本轮真实账号授权。原生夹具隔离结果不替代真实登录后鼠标命中、托盘、多屏与高 DPI 人工验收。

## v0.21 设置内登出与重新登录（2026-10-08）

- `build.ps1 -OutputDir bin-v21` 构建通过；隔离全量测试调用 **1 次，2613 项断言通过、0 失败**（上版 2567 项）。共享菜单第一级为设置、显隐、关于 / 诊断、退出；设置子菜单依次为悬浮内容、登出、重新登录，登录时最后一项显示取消登录。
- 合成服务与真实 WinForms 路径验证：登出取消活动查询及登录、清展示与 scope 缓存、清持久会话；取消后台续期拉起的登录后不会恢复旧会话；登出后查询及重建服务均为未登录，不隐式续期或打开浏览器；重新登录生成新绑定并恢复额度展示。
- 对真实 DPAPI 凭据文件施加只读共享锁，删除失败返回 `CredentialStorageFailed`；解锁后重试成功且文件消失。UI 另验证删除失败显示登出未完成、清旧展示并重新启用登出菜单，允许重试。
- `verify-interaction.ps1 -OutputDir bin-v21` 与 `verify-launcher.ps1 -OutputDir bin-v21` 通过。正式启动器报告 `CLOSED pid=14308 exitcode=0`、`LAUNCHED pid=24868 path=D:\ark_left\bin-v21\ark_left.exe`，进程检查确认运行新版。
- 本轮未执行真实账号登出 / 浏览器重新授权、真人鼠标菜单点击、跨账号或多屏人工验收；合成协议及窗口测试不替代这些验收。

## v0.20 移除悬浮圆圈 tooltip（2026-10-07）

- `build.ps1 -OutputDir bin-v20` 构建通过，全量 **2567 项断言通过、0 失败**。悬浮圆圈不再创建 ToolTip，也不调用临时状态 Show；完整描述保留为 AccessibleDescription。
- 圆圈可见 / 隐藏时的偏好保存失败均记录托盘通知意图，保持旧偏好、无额外查询；独立的锁定与动画文案检查通过。离线用例未显示真实系统通知，也未执行真人鼠标悬停验收。
- `verify-interaction.ps1 -OutputDir bin-v20` 与 `verify-launcher.ps1 -OutputDir bin-v20` 通过。正式启动器报告旧版 `CLOSED pid=22920 exitcode=0`、新版 `LAUNCHED pid=14308 path=D:\ark_left\bin-v20\ark_left.exe`；进程检查确认运行新版。

## v0.19 详情首行与隐藏每日 AFP（2026-10-07）

- `build.ps1 -OutputDir bin-v19` 构建通过。首次全量检查为 **2564 通过 / 1 失败**（`compact.textHeight`：完整套餐标题在单行宽度内换行）；改为显示完整订阅类型、完整套餐名称留在 tooltip 后通过。随后完成隐藏 daily 的展示键处理，最终全量检查 **2567 项断言通过、0 失败**；本轮全量调用 3 次，数量不累加。
- 真实 WinForms 控件与合成额度验证：订阅类型、更新时间、刷新按钮同一行且无重叠；按钮触发既有刷新事件；仅查询时间或隐藏的 daily 数据变化不重建卡片；保持焦点，失败保留原成功时间。原始模型仍保留四窗口，左侧只显示三个周期。
- 100 / 125 / 150 / 200% 布局检查通过，已查看 `preview-detail-header.png`、`preview-spacing-200.png` 合成控件预览。`verify-interaction.ps1 -OutputDir bin-v19` 与 `verify-launcher.ps1 -OutputDir bin-v19` 通过。
- 正式启动器报告 `LAUNCHED pid=22920 path=D:\ark_left\bin-v19\ark_left.exe`，进程检查确认运行该新版。本轮未重新执行真实账号额度查询或真人鼠标点击刷新验收。

## v0.18 浏览器后显示回调弹窗（2026-10-07）

- `build.ps1 -OutputDir bin-v18` 构建通过，全量测试 **2460 项断言通过、0 失败**。随后加强浏览器打开时弹窗尚未可见的顺序断言，重新构建并运行 `--direct`：**106 项断言通过、0 失败**；两者是独立调用，不累加为全量断言数。
- 真实 STA 登录窗口测试确认打开浏览器的动作先于弹窗显示，回调弹窗可见、TopMost=true、允许最小化；既有提交 / 重试 / 取消与合成 token / 额度链路通过。浏览器打开动作使用注入替身，未以真实浏览器验证焦点竞争或真人授权。
- `verify-launcher.ps1 -OutputDir bin-v18` 通过。随后正式启动器报告 `LAUNCHED pid=25324 path=D:\ark_left\bin-v18\ark_left.exe`，进程检查确认运行路径为该新版；这只证明运行版本，不能替代真人点击登录验收。

## v0.17 手动提交登录回调（2026-10-07）

`build.ps1 -OutputDir bin-v17` 构建通过；隔离全量测试 **2458 项断言通过、0 失败**。本次新增 / 调整回调测试，取代 v0.16 自动监听的断言。

- 登录请求等待人工提交时，测试可绑定其回调端口，确认应用没有启动回调监听。PKCE golden 校验保留，验证回调 URL 解码和本次 verifier 匹配。
- 拒绝错误 state、scheme、host、port、path、fragment、重复参数、授权错误、缺少 code、超长地址和单独授权码。
- 使用真实 STA WinForms 登录窗口、按钮处理器与合成回调验证：错误输入留窗重试，提示不回显授权码，正确输入提交；外部取消与关闭窗口返回取消。已检查 `bin-v17/manual-login-preview.png` 控件自绘预览。
- 手动窗口 → PKCE 登录服务 → 合成 token 交换 → 托盘额度查询链路通过；已取消的登录不会打开窗口。`verify-interaction.ps1 -OutputDir bin-v17` 与 `verify-launcher.ps1 -OutputDir bin-v17` 均通过。
- 本轮未重新执行真人浏览器授权与手动复制粘贴，也未操作真实剪贴板；v0.16 的真实授权 / refresh grant 证据仅证明既有服务端协议，不作为 v0.17 真人手动流程的验收结果。

## v0.16 Agent Plan 个人额度直连（2026-10-07）

Windows / .NET Framework C# 5 csc 验证：`build.ps1 -OutputDir bin-direct` 与交付目录 `bin-v16` 构建通过；隔离全量测试 **2437 项断言通过、0 失败**。现有 CLI / Runtime 回归测试保留，新增直连签名、PKCE/state、并发回调、DPAPI、续期/过期、失败重登录和 WinForms 登录后显示测试。

- 正式 C# `ark_left-check.exe --login --refresh-session` 真实浏览器登录成功；首次 GetAFPUsage 成功，重新创建服务丢弃 STS 后，真实 refresh grant 成功并继续查询，`restart_refresh=Ok`、`scope_verdict=Same`。
- 同次额度与 `arkcli usage plan --product agent-plan --format json` 对照，三窗口额度与换算百分比一致，daily 正常解析。公开记录仅保留验证结论，个人档位与实际用量数值已移除。
- `ark_left-tests.exe --direct-live-ui` **5 项通过、0 失败**：真实续期后查询数据进入现有 WinForms 卡片，显示四窗口与个人订阅类型；检查了 `direct-live-preview.png`。该图为真实数据的 DrawToBitmap 控件预览，不是桌面截图。
- `verify-interaction.ps1 -OutputDir bin-v16` 通过；`verify-launcher.ps1 -OutputDir bin-v16` 通过。旧版夹具来自 Git 基线的隔离构建；测试只操作自身 PID/实例名。新版无直连会话时明确 NotLoggedIn，按认证契约清除旧额度缓存；标记与选择/偏好保持。旧“缓存必须保留”断言已按认证契约更新。
- 自然时间下完整 STS 到期等待、真实账号 A→B、真人拒绝授权和服务端签名/权限故障未在本轮人为构造。到期前 2 分钟、过期后续期、并发单次刷新和刷新失败重新登录由时间推进与失败注入测试覆盖；真实 refresh 接口已验证。

本节仅证明个人额度直连；以下旧基线与 Runtime“计划/待验收”描述为历史记录，不能替代本节证据，也不扩大其他能力的验收范围。

## 已接受基线

业务基线为 **v0.1（R001–R004）**，交互基线为 **v0.15 UX023**。T057 于 2026-10-04 经独立验收后由主代理接受代码与自动检查：

| 检查 | 对应历史结果 |
| --- | --- |
| `build.ps1 -OutputDir bin-v15` | 构建成功 |
| 隔离 `bin-v15\ark_left-tests.exe` | 1160 passed / 0 failed |
| 隔离 `tests/verify-interaction.ps1` | 通过 |
| 入口隔离 `tests/verify-launcher.ps1` | 20 项通过，退出 0 |
| `git diff --check` | 通过 |

这些结果只对应当时版本，不能沿用历史结果证明后续直连登录或 Runtime 能力通过。v0.16 直连证据见本页首节；运行进程、部署版本和账号数据需现场核实。

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
- 真实身份 A→B 切换、其他套餐真人数据、全新 Windows 的未登录 / CLI 缺失环境未构造；隔离未登录启动路径已由 v0.16 启动器集成覆盖。
- 保留的 Runtime 诊断、更新 / 回滚及独立工具不由本次直连验证扩大验收范围；全新 Windows 虚拟机、ARM64 执行及磁盘耗尽需单独验收。个人 Agent Plan 的真实浏览器 SSO 与 refresh grant 已完成本页首节验证。

自动检查、反射快捷键派发和 DrawToBitmap 合成预览不能替代真人输入或桌面视觉验收。下一步与完成条件见 [tasks.md](tasks.md)，当前实现差异见 [implementation-gaps.md](implementation-gaps.md)。
