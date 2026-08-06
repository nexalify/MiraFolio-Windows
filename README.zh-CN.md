# MiraFolio Windows 版

<p align="center">
  <img src="src/MiraFolio.App/Resources/app-icon.png" alt="MiraFolio 图标" width="112" />
</p>

<p align="center">
  面向 Windows 10/11 的本地优先、多显示器独立壁纸轮替工具。
</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="docs/privacy.md">隐私说明</a> ·
  <a href="CONTRIBUTING.md">参与贡献</a> ·
  <a href="SECURITY.md">安全策略</a>
</p>

## 主要功能

- 读取 Windows 当前活动显示器拓扑，并按真实布局展示。
- 每台显示器可独立配置壁纸目录、轮替间隔、播放顺序和启用状态。
- 支持无重复随机、正序和倒序播放。
- 可按横屏/竖屏智能匹配图片，并过滤低分辨率图片。
- 后台递归扫描大型本地图库，持久化图片尺寸缓存。
- 指定显示器上出现全屏窗口时，可暂停该屏幕的轮替。
- 可排除不喜欢的图片而不删除源文件，并支持还原或显式永久删除。
- 支持系统托盘、开机自启和桌面快捷操作。
- 单个失效或幽灵显示器不会再导致其余在线显示器全部消失。

MiraFolio 不需要账号或云服务。壁纸、设置、缓存和日志都保留在本机，详情见
[隐私说明](docs/privacy.md)。

## 下载

项目目前处于私有发布 Review 阶段。首个候选版本完成 Windows 10/11 安装、升级和卸载验证后，
会通过 [GitHub Releases](https://github.com/luogreen/MiraFolio-Windows/releases) 提供已签名下载。

计划提供：

- `MiraFolio-Setup-<version>-win-x64.exe`
- `MiraFolio-<version>-win-x64-portable.zip`
- `SHA256SUMS.txt`
- `MiraFolio-<version>-sbom.spdx.json`

除非文件已附加到正式发布的 Release，否则不要把 CI 临时产物视为官方版本。

## 环境要求

普通用户：Windows 10 / 11 x64；自包含发布版无需单独安装 .NET。

构建项目需要 Windows 10 / 11、[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，
构建安装包还需要 Inno Setup 6 或 7。

## 构建与测试

```powershell
dotnet restore MiraFolio.sln
dotnet build MiraFolio.sln -c Release --no-restore
dotnet test src/MiraFolio.Tests/MiraFolio.Tests.csproj -c Release --no-build
```

运行 `publish.bat` 可生成自包含单文件程序；运行 `build-installer.bat 1.0.0` 可生成安装包。
产物分别位于 `publish/` 和 `dist/`，不会提交到 Git。签名和发布验证详见
[Windows 发布指南](docs/windows-release.md)。

## 项目结构

```text
src/
├── MiraFolio.App/    WPF 应用、界面、ViewModel、托盘和桌面集成
├── MiraFolio.Core/   模型、显示器和壁纸服务、调度与图片选择
└── MiraFolio.Tests/  核心行为的 xUnit 测试
```

项目使用 `net10.0-windows`、WPF、`IDesktopWallpaper`、CommunityToolkit.Mvvm、
H.NotifyIcon.Wpf、Microsoft.Extensions.Hosting 和 xUnit。

## 本地数据

MiraFolio 将数据保存在 `%APPDATA%\MiraFolio`：

| 文件 | 用途 |
| --- | --- |
| `settings.json` | 全局设置、逐显示器配置和排除图片清单 |
| `state.json` | 当前壁纸、近期历史和随机播放状态 |
| `image_dim_cache.json` | 后台图片索引使用的尺寸缓存 |
| `mirafolio.log` | 轮转日志，可能包含本地路径和显示器标识 |

## 贡献、安全与许可

提交改动前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。安全问题请根据 [SECURITY.md](SECURITY.md)
通过 GitHub 私密漏洞报告渠道提交，不要创建公开 Issue。

源代码采用 [MIT License](LICENSE)。第三方组件保留各自许可证，详见
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。MIT 许可证不授予把修改版包装成官方 MiraFolio
产品的权利；名称、Logo 和图标规则见 [TRADEMARKS.md](TRADEMARKS.md)。
