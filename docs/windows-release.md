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

当前 GitHub Release 采用未签名发布。未签名不会影响 MiraFolio 的安装和功能，但 Windows 可能显示
“未知发布者”或 SmartScreen 警告，受企业安全策略管理的电脑也可能禁止运行。README 和 Release
说明必须明确提示用户只从官方 Releases 页面下载并核对 `SHA256SUMS.txt`。

如果以后取得代码签名服务或证书，`build-installer.bat` 仍支持使用 Windows 证书存储区中的证书：

```bat
set "MIRAFOLIO_SIGNTOOL=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
set "MIRAFOLIO_CERT_SHA1=<certificate thumbprint>"
set "MIRAFOLIO_TIMESTAMP_URL=<RFC 3161 timestamp URL>"
build-installer.bat 1.0.0
```

脚本使用 SHA-256 文件摘要和 SHA-256 RFC 3161 时间戳：先签名 `MiraFolio.exe`，再让 Inno Setup
在编译阶段签名安装器和内置卸载程序。证书私钥、PFX 文件和密码不应提交到仓库。当前 GitHub
Actions 工作流不读取或保存签名证书。

## GitHub Release 工作流

`.github/workflows/release.yml` 支持两种方式：

- 推送 `v1.0.0` 形式的标签：构建并创建 Draft Release；
- 手动运行：默认只上传保留 14 天的 Actions 候选产物，也可选择同时创建 Draft Release。手动创建时，
  标签会指向启动工作流时选择的准确提交。

GitHub Release 继承仓库的可见性。仓库为 Private 时，Release 只对有仓库访问权限的账号可见；面向普通
用户公开下载前，需要先检查提交历史、Actions 日志和仓库设置中没有敏感信息，再将仓库设为 Public。

工作流会构建并测试项目，然后生成以下 Release assets：

- `MiraFolio-Setup-<version>-win-x64.exe`：安装包；
- `MiraFolio-<version>-win-x64-portable.zip`：便携版；
- `SHA256SUMS.txt`：安装包和便携版的 SHA-256；
- `MiraFolio-<version>-sbom.spdx.json`：SPDX SBOM。

仓库公开后还会为 EXE 和 ZIP 生成 GitHub artifact attestation；它能证明产物来自该工作流，但不等同于
Windows Authenticode 代码签名。

### 使用版本标签发布

确认 `main` 上的版本可以发布且 CI 通过后：

```powershell
git switch main
git pull --ff-only
git tag -a v1.0.0 -m "MiraFolio 1.0.0"
git push origin v1.0.0
```

然后：

1. 打开仓库的 **Actions** 页面，进入 **Build GitHub release**，等待工作流成功。
2. 打开 **Releases** 页面中的 `MiraFolio 1.0.0` 草稿。
3. 下载并测试安装包与便携版，核对 `SHA256SUMS.txt`、版本号和自动生成的 Release notes。
4. 确认未签名提示位于 Release notes 顶部，然后点击 **Publish release**。

草稿发布后，用户才能在 Releases 页面和 README 的下载链接中看到这个版本。不要在草稿验证完成前移动
或重复使用同一个版本标签。

### 手动生成候选产物

在 **Actions** → **Build GitHub release** → **Run workflow** 中填写不带 `v` 的版本号：

- `create_draft_release=false`：只生成 Actions artifact，适合发布前测试；
- `create_draft_release=true`：同时建立对应版本标签和 Draft Release，然后按上面的步骤检查并发布。

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
5. 重新计算安装包和便携 ZIP 的 SHA-256，并与 `SHA256SUMS.txt` 对照。
6. 在开启 SmartScreen 的干净系统上确认未签名提示与 README 中的操作说明一致。
7. 如果将来启用签名，再对安装包执行 `signtool verify /pa /v <setup.exe>`。
