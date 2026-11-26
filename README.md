# 造梦工坊 0.6.1

Windows 和 macOS 上的造梦西游 3 游戏工作台。两个客户端都打开 4399 官方页面，用原账号读取云存档。

## 开始使用

从 [GitHub Releases 下载造梦工坊](https://github.com/Chty-syq/zmxy3/releases)。展开对应版本的 **Assets**，Windows 选 `.exe`，Mac 选 `.dmg`。同页的 `SHA256SUMS.txt` 可用于校验下载。

| 平台 | 安装与启动 |
| --- | --- |
| Windows 10/11 x64 | 运行 `ZmxyStudio-版本-Windows-x64-Setup.exe`，安装后从开始菜单打开“造梦工坊”。需要 .NET Framework 4.8、Visual C++ x64 运行库和官方 Flash PPAPI 64 位组件及配套服务。安装包从系统的 Flash 安装目录寻找插件。 |
| macOS 13 及以上 | 打开 `ZmxyStudio-版本-macOS-universal.dmg`，将“造梦工坊”拖入“应用程序”。同一安装包支持 Apple 芯片和 Intel Mac，内置本地 Ruffle 播放器。Mac 功能键默认控制媒体时，可用 fn + F7 / F8 或窗口按钮。 |

Windows 便携版仍可双击根目录的 `启动造梦工坊.cmd` 或 `artifacts/demo/ZmxyDemo.exe`。从源码运行、制作安装包和正式签名见[双平台构建与发布](docs/RELEASE.md)。

- **游戏页面**：左侧新建、切换页面，最多四个。新页面继承当前登录状态；A 常驻，B–D 可关闭且始终静音。关闭或刷新前请在游戏内保存。
- **变速齿轮**：1×、2×、3×、100×，设置对全部页面生效。
- **键盘连点**：默认 S / S / J，1 ms，全部关闭。点击按键框后直接按键，可添加或逐行删除。打开 Num Lock 可以录入右侧小键盘 0–9。例如“1”和“小键盘 1”是两个独立的按键，可以排在同一个循环里。
- **F7**：开始或停止当前页连点。切页会停止；默认切到其他应用也停止，可用开关允许后台继续。
- **F8**：停止全部连点、释放按键，并恢复 1×。
- **游戏音量**：滑杆控制页面 A；0% 静音。滑杆也支持方向键、Home 和 End。
- **网页链接**：手动点击的链接在系统默认浏览器打开。

更多交互说明在窗口右上角“使用说明”。间隔是松开按键后的等待时间；运行中修改，在下次启动连点时生效。

## 0.6.0 界面更新

暖白与青绿色的轻量界面，统一页面卡片、圆角按钮、键盘开关和音量滑杆；主界面移除 Demo 标记、输入验证、性能采样和重复提示。加入独立应用图标及中文启动入口。

保留此前确认有效的流畅度修复：网页辅助接口默认关闭，避免浏览器主线程被自动查询阻塞；游戏内网络直接连接，不走系统代理。未增加界面动画或后台采样。

## Windows 本地构建和诊断

```powershell
./build.ps1 -Test
./启动造梦工坊.cmd
```

首次准备编译工具见 `bootstrap.ps1`，技术记录及历史验收见源码目录的 `docs/DEVELOPMENT.md`。

诊断仅按需通过启动参数运行：

- `start-demo.cmd --test-page`：本地输入验证，使用隔离测试资料。
- `artifacts/demo/ZmxyDemo.exe --integration-test`：运行页面集成检查并自动退出。
- `start-demo.cmd --performance-capture`：启动约 20 秒后自动采样约 15 秒，结果写入 `data/A/performance`。采样时保持窗口在前台并正常操作。
- `start-demo.cmd --enable-web-accessibility`：恢复网页读屏接口。
- `start-demo.cmd --software-rendering`：显卡问题排查。

默认连接不改变 Windows 的系统代理或 VPN/TUN 路由。Windows 客户端沿用 CEF/Flash 运行环境。Windows 便携版资料保存在程序目录的 `data`，安装版保存在 `%LOCALAPPDATA%\ZmxyStudio\data`。Mac 版资料位于 `~/Library/Application Support/造梦工坊`。更新时请保留对应目录。客户端不自动保存游戏，也未实现多个页面同时写同一存档的冲突保护。
