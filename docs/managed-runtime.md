# Managed ArkCLI Runtime

Managed Runtime、自带 bootstrap、应用内登录、自动更新 / 回滚等均为**计划 / 待验收，尚未进入当前分支实现**。本文以下目录、调用、登录、更新、回滚和分发规则均为设计目标；当前分支使用本机 ArkCLI，登录由用户在终端完成。与已接受业务 / 交互基线的差异见 [implementation-gaps.md](implementation-gaps.md)。

计划使用自带、托管的 ArkCLI，发布包拟包含官方 1.0.37
Windows AMD64 / ARM64 bootstrap；首次运行拟按 Windows 原生架构导入用户目录，
无需 Node、npm 或 PATH。无法确认架构时拒绝导入和更新。

```text
%LOCALAPPDATA%\ArkLeft\
  runtime\<version>\arkcli.exe
  downloads\*.exe.part
  runtime-state.json
  runtime-state.json.bak
  logs\runtime.log
```

计划的 `runtime-state.json` 格式1只记录 active、previous、pending、lastCheckAt、
lastSuccessfulVersion。写入经过 temp → flush → replace；损坏优先读备份，
active 路径失效尝试 previous，然后恢复 bootstrap。版本字段必须为严格数字
semver，不能形成目录穿越。业务缓存继续使用原有 DPAPI 与 ScopeFingerprint；
Runtime 版本不参与业务身份。

计划解析顺序：`ARK_LEFT_CLI` → Managed Runtime → PATH 原生 ArkCLI / npm 的 native exe。
无效开发覆盖会明确失败；正式安装不要求用户配置覆盖或 PATH。
`ARK_LEFT_RUNTIME_DIR` 可覆盖 Runtime 根目录，供隔离测试与诊断。

计划让每次查询持有 Runtime lease，整个 auth / usage 两阶段固定同一个 executable。
调用须沿用当前分支的默认订阅发现方式，覆盖正式 Agent Plan / Coding Plan 范围；具体适配需实现后验收：

```text
auth status --format json
usage plan --format json
```

计划的应用内登录由用户点击“登录方舟 / 重新登录 / 切换账号”触发：暂停新查询、取消在途查询、
拒绝旧查询的迟到结果、清除旧展示及缓存，调用 `auth login volc-sso`，超时10分钟。
成功后重新确认 auth 与 scope，再查询额度。取消或失败可重新登录。程序不自动
logout，不主动执行 profile 管理，也不直接读取 ArkCLI 凭据；官方登录命令管理并复用 `~/.arkcli` 用户状态。

计划的自动更新在 UI 就绪后15秒启动独立维护计时器，此后每小时判断一次，联网检查最多每24小时
一次（包括失败尝试）。手动“检查更新”同样遵守24小时限制。官方来源固定为
GitHub `volcengine/ark-cli` latest release；资产名、版本、架构、仓库地址、大小
均校验，通过资产 API 下载。HTTPS 重定向只允许 GitHub 官方下载主机，最多5跳，
元数据超时20秒、二进制下载超时5分钟，均支持取消。

计划安装顺序为 SHA256 → WinVerifyTrust → `--version`（10秒）→ 版本目录 → pending
→ 空闲激活。官方 digest 存在时必须匹配；没有 digest 时仍计算 SHA256、验证签名
及 Windows 信任链。WinVerifyTrust 使用缓存证书获取以支持离线启动，不固定证书
thumbprint。任何活动查询、登录 lease 或 ArkCLI 子进程都会阻止激活和清理。
目标为 Runtime 已准备好时，额度查询不等待后台下载，仍需验证。

计划对同 major 新版自动安装、激活；跨 major 只显示诊断不自动安装或激活。
保留 active / previous / pending，清理其余版本时不跟随 junction、不删除无关内容。
下载、签名、digest、版本校验或写盘失败保留原 active；异常退出只按 Windows
负退出码识别，exe 启动失败也属于 Runtime 故障，回退已验证 previous，并最多
重试一次完整查询或用户已发起的登录。普通非零业务退出码、网络超时、未登录、未订阅、401/403、
5xx、用户取消或未知 JSON 格式错误不触发回滚。启动时 smoke / 签名验证失败可
恢复 previous。无可用 previous 时保持安全错误状态。

计划的日志只写 UTC 时间、严格版本与固定 Runtime 错误分类，不写命令 stdout/stderr、
原始 auth JSON、用户身份、token、API Key 或 Authorization。

构建与分发为计划 / 待验收，尚未进入当前分支实现。当前 `build.ps1` 只构建应用、诊断工具与离线单测，不准备或打包 ArkCLI。后续需实现官方 bootstrap 准备、离线完整性与签名校验、双架构 Runtime 和第三方许可打包，以及隔离的 Runtime 专项验证入口；完成后再补充可执行命令。

计划固定两个 bootstrap 的官方 SHA256；更新 bootstrap 时须同步版本与 digests，
并重新验证 x64 / ARM64 分发文件。目标为普通构建不联网；缺任一 bootstrap、
digest 不匹配或签名无效时应拒绝分发。

专项验证计划覆盖真实官方 x64 binary 的 digest / WinVerifyTrust / version / 无 PATH
导入与损坏恢复，以及合成进程、Release、下载失败和激活回滚场景。Runtime 专项
测试尚未进入当前分支实现；真实浏览器 SSO、全新 Windows 虚拟机、ARM64 执行、
磁盘耗尽和人工多屏视觉需单独验收。实现后须记录实际结果，历史证据见 [verification.md](verification.md)。
