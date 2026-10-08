# PocketDeck VR

把手机屏幕悬浮在 SteamVR 里，并用手柄直接操作它。

PocketDeck VR 通过 adb + patched scrcpy 把 Android 手机画面（H.264）送到 PC，用
Media Foundation / D3D11 硬解后作为 OpenVR 浮窗提交给 SteamVR；音频走 Opus → WASAPI；
手柄输入通过 SteamVR Input 动作集映射为手机触摸、拖拽与系统操作。

## 特性

- **手机浮窗**：把手机屏幕固定在 VR 世界或跟随头部，分辨率可到手机原生（如 1260×2800）
- **手柄控制**：抓握拖动、触控点击、返回 / 桌面 / 最近任务 / 控制栏 / 截屏，全部走 SteamVR 动作集
- **连接自动化**：插上数据线即自动开通 WiFi 无线调试并记住地址，之后可拔线；USB 与 WiFi 各自独立显示链路状态
- **画质探测**：一键验证 scrcpy → 硬解 → 纹理 整条链路
- **现代界面**：自绘 UI 框架（无第三方控件库），双色矢量图标、60fps 动画、可缩放窗口、Toast 与状态栏
- **更新检查**：读取 GitHub Releases，稳定 / 测试双通道，只提示不自动安装

## 环境要求

- Windows 10/11 x64
- .NET 10 SDK（构建用）
- SteamVR（运行时必需，用于浮窗与手柄输入）
- Android 手机，开启 USB 调试并授权本机

## 构建

```powershell
dotnet build src\src.sln -c Release
```

自包含发布（推荐，产物不依赖目标机安装 .NET）：

```powershell
dotnet publish src\VRPhoneScreenOverlay\PocketDeck.csproj -c Release -r win-x64 --self-contained true
```

## 目录结构

```
src/
  VRPhoneScreenOverlay/     主程序（含自绘 UI 框架 App/Ui3）
  PocketDeck.Android/       adb / scrcpy / 设备发现
  PocketDeck.Media/         H.264 硬解、音频
  PocketDeck.Session/       浮窗会话、控制通道、媒体会话
  PocketDeck.SteamVR/       OpenVR 互操作、动作绑定、空间拖拽
  PocketDeck.SteamVR.BindingTool/  本地绑定准备工具（独立进程）
  PocketDeck.Settings/      设置模型与持久化
  PocketDeck.Setup/         安装器
  PocketDeck.Input/         输入映射
  PocketDeck.Contracts/     跨模块契约
  PocketDeck.Core/          基础设施
deploy.ps1                  部署到本地测试目录
pack.ps1                    打包安装器（7-Zip + 自包含发布）
uitest.ps1                  截图 / 点击的自动化测试脚本
```

## 第三方运行时

仓库只包含自研源码与构建脚本。打包/运行所需的第三方运行时不在本仓库内，需要另行准备：

- Android Platform Tools（`adb`）与 scrcpy（含 `scrcpy-server`）
- OpenVR 运行时（`openvr_api.dll`）
- NuGet 依赖：Vortice.Direct3D11 / SharpGen、NAudio、Concentus 等（构建时自动还原）
- 各组件许可见发行包内 `licenses/` 目录

## 更新检查

应用读取本仓库的 GitHub Releases：

- 稳定通道：只取正式发布；测试通道：包含预发布
- 发现有新版时展示更新内容并提供「前往下载」，**不会自动下载或安装**
- 自动检查开关、通道切换、更新源（`owner/repo`）保存在 `%LOCALAPPDATA%\PocketDeck\update-prefs.json`

## 许可

本项目基于 **Boost Software License 1.0** 开源，全文见 [LICENSE](LICENSE)。

## 免责声明

本项目为独立第三方工具，与 Valve、HTC、Meta 及任何手机厂商均无关联。
SteamVR、Android 等商标归各自所有者所有。
