# 双平台构建与发布

| 平台 | 客户端和数据 | 安装产物 |
| --- | --- | --- |
| Windows x64 | 原 WinForms、CEF 84、系统安装的官方 Flash PPAPI。便携版沿用原资料目录，安装版使用每用户资料目录。 | `artifacts/windows/ZmxyStudio-0.6.1-Windows-x64-Setup.exe` |
| macOS 13+ | Electron 工作台和随应用分发的 Ruffle。各页使用独立的持久化浏览器资料，新建页面通过内存继承当前 4399 登录 Cookie。 | `artifacts/macos/ZmxyStudio-0.6.1-macOS-universal.dmg` |

官方游戏地址、账号验证和云存档服务由 4399 提供。Mac 和 Windows 的本地浏览器资料分别保存在各自系统上。使用相同账号即可选择同一云存档。

## macOS

源码构建需要 Node.js 22.12+ 和 npm，已使用 Node 24 验证。在仓库根目录运行。

```sh
cd macos
npm ci
npm start
```

`npm start`、`npm test` 和打包命令都会准备锁定版本的播放器。JavaScript 和 WASM 从应用资源目录加载。变速通过 Ruffle 的 WASM 时钟读取、前后台帧时间戳实现，切换时保留时间连续性。连点按真实时间调度。关卡实际帧率受模拟器和机器性能影响。

```sh
npm start -- --test-page           # 与账号资料隔离的输入验证页
npm test                           # 在临时资料中运行离线集成测试
npm start -- --online-smoke-test   # 匿名打开官方入口，检查游戏影片元数据
npm start -- --software-rendering
npm start -- --enable-web-accessibility
npm start -- --performance-capture
npm run dist                       # 本地 ad-hoc 签名的通用 DMG
```

首次加载时，如果 4399 的检测页在播放器就绪前给出兼容性提示，客户端会在官方检测影片真正响应后自动重试一次。

性能采样在约 20 秒后开始，持续约 15 秒，将页面进程往返时间和窗口前台状态写入用户资料目录的 `performance/`。采样无需操作账号。

分发给公众时，在发布 Mac 的钥匙串中配置 Developer ID Application 身份，给 electron-builder 配置公证凭据，例如 `APPLE_ID`、`APPLE_APP_SPECIFIC_PASSWORD`、`APPLE_TEAM_ID`。在 `macos/` 运行 `npm run dist:signed`。检查构建日志中的公证结果，再对镜像中的应用运行 `xcrun stapler validate` 和 `spctl --assess --type execute --verbose`。本地测试签名只用于测试构建。

## Windows

在 x64 Windows 上安装 Node.js 和 NSIS 3 Unicode，另按需准备官方运行时。安装 64 位 Visual C++ 2015–2022 或兼容的更新版运行库。官方 Flash 64 位 PPAPI 安装程序还负责配套服务。[Microsoft Visual C++ 运行库](https://learn.microsoft.com/zh-cn/cpp/windows/latest-supported-vc-redist) · [Flash 中国官方下载](https://www.flash.cn/download-wins)

在仓库根目录运行下列 PowerShell 命令。

```powershell
./bootstrap.ps1
./build.ps1 -Restore -Test
$compiler = Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'
node scripts/package-windows.mjs artifacts/demo "$compiler"
```

安装器在当前用户的 `%LOCALAPPDATA%\Programs\ZmxyStudio` 安装程序，添加开始菜单快捷方式，并注册 Windows 卸载入口。运行中升级或卸载时，请先在游戏内保存并退出。账号资料位于 `%LOCALAPPDATA%\ZmxyStudio\data`，卸载时保留。现有便携版仍从原目录使用，原目录的 `data` 可以在程序退出后复制到安装版的资料目录。

打包脚本根据文件清单取用程序、原生 DLL、资源与许可，另从所用 CEF 提取 Chromium notices。Flash 由用户另行通过官方安装器安装。安装包不收集构建机器的插件、登录资料、缓存或诊断日志。

公开 Windows 包可在有 Authenticode 证书的发布机上先签署本项目的 EXE 和 DLL，再运行打包命令，最后用 Windows SDK 的 `signtool` 签署安装器并验证签名。保留供应商 DLL 原签名。Windows 客户端为兼容 PPAPI 使用旧版 CefSharp，NuGet 会报告已知安全公告。

## CI 和发版验收

在 GitHub 网页打开 [Build installers](https://github.com/Chty-syq/zmxy3/actions/workflows/installers.yml)，选择 **Run workflow** 和要构建的分支。默认只构建和验收，两个安装包可在本次运行的 **Artifacts** 下载。勾选 **Publish both installers to GitHub Releases after testing** 会在验收后自动创建版本标签并发布到 [Releases](https://github.com/Chty-syq/zmxy3/releases)。

也可从命令行发版。例如当前源码版本为 `0.6.1`，运行 `git push origin HEAD:refs/heads/release/v0.6.1` 即可开始。构建失败时可修复后继续推送此分支。已有版本标签的项目也支持用 `git push origin v0.6.1` 触发。新版本须同步更新 `src/ZmxyDemo/ZmxyDemo.csproj` 与 `macos/package.json`，并在 `macos/` 运行 `npm install --package-lock-only`。工作流会核对两端的版本号、锁文件和发布分支或标签名。

Windows 在安装后的程序上运行原生自测和页面集成检查，并验证卸载保留用户资料。云端 Windows 缺少播放设备时，会在临时测试机上安装 VB-CABLE，用 Windows 音频会话实测音量和静音。Mac 在 Apple 芯片和 Intel 机器上分别挂载同一份通用 DMG 并启动测试。全部通过后，工作流把这次生成的 `.exe`、`.dmg` 和 `SHA256SUMS.txt` 上传到 Release 草稿，核对 GitHub 返回的三个文件摘要，再公开下载。已公开版本保留原有附件。发布使用 GitHub 自带的工作流令牌。

自动发布的 Mac 应用使用临时签名，未经 Apple 公证，Windows 安装包未做 Authenticode 签名。玩家可以在 macOS 上用 `shasum -a 256 文件名.dmg`，或在 Windows PowerShell 中用 `Get-FileHash 文件名.exe -Algorithm SHA256`，与发布页的 `SHA256SUMS.txt` 对照。

离线集成测试覆盖 A–D、新页面账号交接、按键顺序、小键盘、切页释放、音量、外链和 Flash 内部时钟。线上验收使用正式账号进入旧档地图。需要验证写回时，选择可用于测试的存档，保存后重新打开确认。

### 2026-11-25 本地验收

- Apple M5、macOS 26.6。从最终 DMG 启动 arm64 和 x86_64 两个切片，离线集成检查均通过。x86_64 在 Rosetta 下运行。实测 Flash 字节码能接收连点，并读取按设定倍率前进的时钟。
- 从镜像内的客户端匿名加载官方 940 × 590、24 fps 的 ActionScript 3 游戏影片。用户在这台 Mac 的工作台内完成 4399 登录，并从已有云存档进入地图。
- Windows x64 主程序、子进程及变速 DLL 交叉编译通过。NSIS 安装包通过归档完整性和内容检查。本次未在 Windows 真机执行安装、卸载或游戏回归。
- 本地 Mac 包采用 ad-hoc 签名，Windows 包未签名。正式发行签名、公证和云存档写回未在本次验收中执行。
