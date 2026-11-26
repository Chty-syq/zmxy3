# 第三方组件

| 组件 | 版本 | 来源 | 许可 |
| --- | --- | --- | --- |
| CefSharp | 84.4.10 | https://github.com/cefsharp/CefSharp/tree/cefsharp/84 | BSD，见 CefSharp.txt |
| Chromium Embedded Framework | NuGet cef.redist.x64 所带版本 | https://github.com/chromiumembedded/cef | BSD，见 CEF.txt；Chromium 及其依赖另有 notices |
| MinHook | 1.3.4 | https://github.com/TsudaKageyu/minhook | BSD，见 MinHook.txt |
| Newtonsoft.Json | 13.0.3 | https://github.com/JamesNK/Newtonsoft.Json | MIT，见 Newtonsoft.Json.md |
| .NET SDK | 10.0.404，构建工具 | https://dotnet.microsoft.com/ | 下载目录包含原厂许可文件 |
| Zig | 0.13.0，构建工具 | https://ziglang.org/ | 下载目录包含原厂许可文件 |
| Flash Player PPAPI | 34.0.0.384 | https://www.flash.cn/ | 专有组件，适用原厂条款；未授予随本项目再分发的许可 |

本地 `runtime/` 和构建目录不纳入源码版本控制。Windows 安装包另附从当前 CEF 的 `cef.pak` 提取的完整 `Chromium-credits.html`，仅使用另行安装的官方 Flash。

macOS 客户端使用 Electron 45.5.0 和 Ruffle 0.8.0。应用包的 `Contents/Resources` 内包含 Electron 许可及 Chromium notices，`player/` 内包含 Ruffle 随包提供的 MIT、Apache 2.0 许可和本项目时钟适配说明。游戏内容由 4399 在线提供。
