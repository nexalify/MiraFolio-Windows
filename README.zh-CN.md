# MiraFolio Windows 版

<p align="center">
  <img src="src/MiraFolio.App/Resources/app-icon.png" alt="MiraFolio 图标" width="112" />
</p>

<p align="center">
  <strong>让每一台显示器都有自己的壁纸节奏。</strong>
</p>

<p align="center">
  专为 Windows 多显示器和海量本地图库打造的壁纸轮替工具。
</p>

<p align="center">
  <a href="https://github.com/luogreen/MiraFolio-Windows/releases">
    <img src="https://img.shields.io/badge/下载-GitHub%20Releases-7C3AED?style=for-the-badge&logo=github" alt="打开 GitHub Releases" />
  </a>
</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="docs/privacy.md">隐私说明</a> ·
  <a href="https://github.com/luogreen/MiraFolio-Windows/issues">问题反馈</a>
</p>

<p align="center">
  <img src="docs/images/mirafolio-overview.png" alt="MiraFolio 为两台显示器管理不同壁纸和轮替设置" width="1000" />
</p>

> [!NOTE]
> MiraFolio 目前处于私有发布 Review 阶段，暂时没有公开下载版本。官方安装包和便携版只会发布在
> 上方链接的 GitHub Releases 页面。

## 更适合多显示器的壁纸体验

- **每台显示器独立设置。** 分别选择图片文件夹、轮替间隔、播放顺序，并决定是否启用轮替。
- **面向数万张以上的大型图库。** 在后台分批建立图片索引并缓存尺寸信息，后续启动不必重新读取
  每一张图片的尺寸，让海量本地壁纸也能方便地参与轮播。
- **边看边筛，一键淘汰。** 遇到不喜欢的壁纸可以立即移出后续轮播，但不会删除原文件；之后还能
  从回收站恢复。
- **显示器变化互不影响。** 少接一台显示器不会隐藏或中断其他屏幕；Windows 识别到新显示器后，
  也可以立即为它配置壁纸。
- **更聪明地选择图片。** 横图匹配横屏、竖图匹配竖屏，还可以自动跳过尺寸过小的图片。
- **隐私优先。** 无需账号、云服务或联网同步，不包含分析统计，也不会上传壁纸。

## 你可以用它做什么

- 支持无重复随机、正序和倒序轮替。
- 游戏、演示或视频进入全屏时，可只暂停对应显示器的壁纸轮替。
- 通过快捷操作立即换图、在文件管理器中定位当前壁纸或一键淘汰不喜欢的图片。
- 在回收站集中查看和恢复已淘汰图片；永久删除必须经过明确确认。
- 支持开机启动，日常操作可以在系统托盘完成。
- 在一个可视化布局中管理横屏、竖屏等混合显示器组合。

## 下载与安装

打开 [MiraFolio Releases 页面](https://github.com/luogreen/MiraFolio-Windows/releases)，选择最新版本，
然后展开 **Assets**。

| 下载文件 | 适合谁 | 使用方式 |
| --- | --- | --- |
| `MiraFolio-Setup-<version>-win-x64.exe` | 推荐大多数用户使用 | 下载并运行安装程序，然后从开始菜单打开 MiraFolio。 |
| `MiraFolio-<version>-win-x64-portable.zip` | 需要免安装使用 | 下载并解压 ZIP，然后运行其中的 `MiraFolio.exe`。 |

GitHub 自动生成的 **Source code (zip)** 和 **Source code (tar.gz)** 是源码压缩包，不是 Windows
安装程序。请只从本仓库的官方 Releases 页面下载；如果页面中还没有 Release，就表示目前还没有
可供普通用户下载的官方版本。

## 快速开始

1. 打开 MiraFolio，在显示器布局中选择一台屏幕。
2. 为这台显示器选择壁纸文件夹。
3. 设置轮替间隔、播放顺序和智能匹配选项。
4. 开启壁纸轮替，之后 MiraFolio 会在系统托盘中持续运行。

如果需要自定义其他显示器，重复前三步即可。

## 系统要求

- Windows 10 或 Windows 11
- x64 PC
- 官方自包含版本无需另外安装 .NET

## 隐私、帮助与项目信息

壁纸、设置、缓存和日志都保留在你的电脑上，详情见 [隐私说明](docs/privacy.md)。Bug 和功能建议可提交到
[GitHub Issues](https://github.com/luogreen/MiraFolio-Windows/issues)，安全问题请按照
[SECURITY.md](SECURITY.md) 中的流程报告。

开发与发布资料位于 [CONTRIBUTING.md](CONTRIBUTING.md)、[docs/architecture.md](docs/architecture.md)
和 [docs/windows-release.md](docs/windows-release.md)。

MiraFolio 源代码采用 [MIT License](LICENSE)，产品名称、Logo 和图标遵循
[品牌资产政策](TRADEMARKS.md)。
