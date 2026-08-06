# Windows 发布指南

## 推荐产物

MiraFolio 保留两种发布形式：

- `publish\MiraFolio.exe`：自包含单文件便携版，用户无需安装 .NET。
- `dist\MiraFolio-Setup-<version>-win-x64.exe`：面向普通用户的标准安装包。

公开发布建议以安装包为主，便携版作为备选。安装包使用 Inno Setup，按当前用户安装到 `%LOCALAPPDATA%\Programs\MiraFolio`，不需要管理员权限，也不依赖 Microsoft Store。

## 构建安装包

在 Windows 上安装：

1. .NET 10 SDK（仓库通过 `global.json` 固定 SDK 功能带）。
2. [Inno Setup 6 或 7](https://jrsoftware.org/isdl.php)。

若用于商业发布，请同时阅读 Inno Setup 安装目录中的许可说明；官方请求商业用户购买商业许可。

然后在仓库根目录运行：

```bat
build-installer.bat 1.0.0
```

脚本会先生成 `win-x64` 自包含单文件 EXE，再编译安装包。固定的 Inno Setup `AppId` 使后续版本识别为同一个应用，可直接覆盖升级。

安装器会创建开始菜单快捷方式，并提供可选的桌面快捷方式。升级或卸载时如果 MiraFolio 正在运行，安装器会要求先关闭应用。

## 代码签名

对外发布前应同时签名应用 EXE 和最终安装器。不签名的程序仍然可安装，但 Windows 可能显示“未知发布者”或 SmartScreen 警告。

`build-installer.bat` 支持使用 Windows 证书存储区中的代码签名证书：

```bat
set "MIRAFOLIO_SIGNTOOL=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
set "MIRAFOLIO_CERT_SHA1=<certificate thumbprint>"
set "MIRAFOLIO_TIMESTAMP_URL=<RFC 3161 timestamp URL>"
build-installer.bat 1.0.0
```

脚本使用 SHA-256 文件摘要和 SHA-256 RFC 3161 时间戳：先签名 `MiraFolio.exe`，再让 Inno Setup 在编译阶段签名安装器和内置卸载程序。证书私钥、PFX 文件和密码不应提交到仓库。

GitHub Actions 的 Release 工作流支持以下仓库 Secrets：

- `WINDOWS_SIGNING_PFX_BASE64`：PFX 文件的 Base64 内容；
- `WINDOWS_SIGNING_PFX_PASSWORD`：PFX 密码；
- `WINDOWS_SIGNING_TIMESTAMP_URL`：RFC 3161 时间戳地址。

没有配置这些 Secrets 时，工作流仍可生成供私有 Review 使用的未签名候选产物，但不应将其作为公开正式版本发布。签名 Secrets 只会在标签或手动 Release 工作流中使用，不会暴露给外部 PR。

## GitHub Release 工作流

`.github/workflows/release.yml` 支持两种方式：

- 推送 `v1.0.0` 形式的标签：构建并创建 Draft Release；
- 手动运行：默认只上传保留 14 天的 Actions 候选产物，可选择同时创建 Draft Release。

工作流会生成安装包、便携 ZIP、`SHA256SUMS.txt` 和 SPDX SBOM。仓库公开后还会生成 GitHub artifact attestation；私有仓库是否支持构建证明取决于 GitHub 计划，因此私有 Review 阶段会自动跳过。

## 可选发布渠道

等安装包有了稳定的 HTTPS 下载地址后，可以再向 Windows Package Manager Community Repository 提交 manifest。这不需要上架 Microsoft Store；`winget` 只会根据 manifest 下载并静默运行同一个 Inno Setup 安装包。首版先使用 GitHub Releases 直下即可，不必在本轮引入自动更新基础设施。

## 升级与卸载

- 新版本直接运行新安装包即可覆盖升级。
- 卸载时会移除程序、快捷方式和开机启动项。
- `%APPDATA%\MiraFolio` 中的设置、轮替状态、图片缓存和日志默认保留，重新安装后可继续使用。

## 发布前检查

1. 在干净的 Windows 10 和 Windows 11 环境各安装一次。
2. 验证首次启动、开始菜单和桌面快捷方式。
3. 从上一版本覆盖升级，确认 `%APPDATA%\MiraFolio` 数据保留。
4. 验证卸载后开机启动项已移除。
5. 对安装包执行 `signtool verify /pa /v <setup.exe>`。
