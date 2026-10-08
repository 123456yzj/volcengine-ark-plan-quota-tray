# Agent Plan 个人额度直连（v0.23）

生产托盘与 `ark_left-check.exe` 使用 `DirectAgentPlan`，登录和额度查询不调用 ArkCLI。v0.23 不分发 ArkCLI，托盘不构造其 Runtime 管理器或启动组件维护，也不显示组件诊断。旧 CLI 解析、查询及 Runtime 代码保留为开发兼容回归，独立诊断工具的显式 Runtime 参数仍仅用于开发。

## 登录与续期

点击托盘或圆圈右键菜单“设置 → 重新登录”先打开浏览器，再显示置顶、可最小化的手动回调弹窗；首次登录也使用此入口。窗口可复制登录链接、重新打开浏览器、粘贴完整回调地址并提交。公共客户端为 `trn:signin:::devtools/same-device`，scope 为 `Console:All:All`，接口来自已验证的 SSO PoC。

1. 用加密随机数生成 state 和 PKCE verifier，challenge 为 SHA256 后的 base64url（S256）。
2. 回调地址为 `http://127.0.0.1:<随机端口>/oauth/callback`，不启动任何回调监听。授权后浏览器可能提示无法访问，用户复制地址栏中的完整 URL 并主动粘贴提交；应用不自动读取剪贴板。
3. 校验本次回调的 scheme、host、port、path 与 state，拒绝重复参数、URL fragment、userinfo、缺失 code 或授权错误。地址上限 8 KiB，code 上限 4 KiB；校验失败允许重新粘贴，不发送 token 请求。等待上限 10 分钟，关闭窗口 / Esc / 取消登录均可取消；错误提示不回显原始地址或授权码。
4. 使用 code、verifier、redirect URI 换取临时 STS 和 refresh token。临时 STS 仅在内存，按 token 响应的 `expires_in` 计算有效期。
5. refresh token 与随机登录绑定写入 `%LOCALAPPDATA%\ark_left\direct-session.dat`，使用 Windows DPAPI CurrentUser 加密，临时文件 + Replace / Move 原子保存。保存失败不视为登录成功；文件不含 STS、授权码或原始账号信息。
6. 每 30 秒检查 STS，到期前 2 分钟续期；查询前也检查。进程重启后从加密 refresh token 恢复，不持久化 STS。轮转的 refresh token 原子替换，服务端未返回新 refresh token 时保留已有值。
7. 刷新失败清除旧会话并重新打开手动回调窗口和网页登录。续期与查询/显式登录共用 semaphore，避免并发刷新或重复登录；退出取消活动请求并关闭登录窗口。

每次重新网页登录建立新的随机缓存绑定，成功续期保持绑定。额度由该会话的 STS 签名取得，因此直接归属该绑定，不依赖 CLI 的 auth/viewer。绑定仅以 SHA256 指纹进入额度快照；旧 CLI scope 缓存不能混用。

“设置 → 登出”先暂停查询并取消显式登录，再取消当前会话的所有服务操作（包括后台续期拉起的登录窗口）；等待持有 semaphore 的操作结束后清除内存 STS、refresh token 和持久会话。旧展示与额度缓存同时清除，迟到的查询不得重新提交。登出后轮询只返回未登录，不自动续期或打开浏览器；显式重新登录可建立新会话。凭据删除失败返回 `CredentialStorageFailed` 并提示重试，不报告成功登出。

## 查询与映射

`POST https://ark.cn-beijing.volcengineapi.com/?Action=GetAFPUsage&Version=2024-01-01`，body 为 UTF8 `{}`，region 为 `cn-beijing`，service 为 `ark`。

V4/HMAC-SHA256 的 SignedHeaders 为 `content-type;host;x-content-sha256;x-date;x-security-token`，包含临时 STS 的 `X-Security-Token`。请求禁止自动重定向，每次网络请求限时 30 秒，响应限 1 MiB。

| 接口字段 | 现有模型 |
| --- | --- |
| `PlanType` | `ProductQuota.Tier`，product=`agent-plan`，edition=`personal` |
| `AFPFiveHour` | `5h` / 5 小时 |
| `AFPDaily` | `daily` / 每日 |
| `AFPWeekly` | `weekly` / 每周 |
| `AFPMonthly` | `monthly` / 每月 |
| 每窗口 `Used` / `Quota` | `Used` / `Total`；按现有规则计算剩余百分比和剩余额度 |
| `ResetTime` | epoch 毫秒转本地重置时间，不推断额度恢复 |

左侧详情弹窗隐藏个人 Agent Plan 的 daily / 每日 AFP 行，只展示 5h、weekly、monthly；原始解析与缓存仍保留四窗口。

缺少或非法数值保持未知，缺窗口为部分错误，`Result=null` 为未订阅，缺少 Result 或套餐档位为格式错误。

失败分类：`LoginFailed`、`TokenRefreshFailed`、`Unauthorized`（401）、`Forbidden`（403 / AccessDenied）、`SignatureFailed`、`NoSubscription`、`NetworkError`、`CredentialStorageFailed`、`FormatError`、`Cancelled`。签名错误优先于 HTTP 403；不展示响应中的原始错误信息。日志不输出 SK、SessionToken、refresh token 或授权码。

## 检查

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -OutputDir bin-direct
$env:ARK_LEFT_STATE_DIR = "$PWD\bin-direct\test-state"
.\bin-direct\ark_left-tests.exe
```

真实登录检查应在新的 PowerShell 会话执行，或先恢复 `ARK_LEFT_STATE_DIR`，以免把测试状态当作正式登录：

```powershell
.\bin-direct\ark_left-check.exe --login --refresh-session
.\bin-direct\ark_left-tests.exe --direct-live-ui
```

`--refresh-session` 在首次查询后重新创建服务，丢弃内存 STS，以实际 refresh grant 取得新 STS 并继续查询。它验证真实续期接口；时间推进测试另行验证到期前和到期后的调度，不声称等待了真实 STS 到期。

ArkCLI 仅用作人工对照：`arkcli usage plan --product agent-plan --format json`。对照 5h / weekly / monthly 的 used、total、百分比和 reset_at；直连额外保留 daily。
