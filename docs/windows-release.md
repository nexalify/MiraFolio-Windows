# Windows 发布指南

## 官方产物

MiraFolio 仅发布 Windows x64 便携版：

- `MiraFolio-<version>-win-x64-portable.zip`：自包含应用，用户无需安装 .NET；
- `SHA256SUMS.txt`：便携 ZIP 的 SHA-256；
- `MiraFolio-<version>-sbom.spdx.json`：SPDX SBOM。

ZIP 内包含 `MiraFolio.exe`、MIT License 和中英文 README。用户应将整个 ZIP 解压到固定文件夹，
然后运行 `MiraFolio.exe`。GitHub 自动生成的 Source code 压缩包不是 Windows 应用。

仓库中的安装器脚本仅作为以后可能恢复安装版时的开发资料；GitHub Actions 不构建或上传安装包。

## 本地构建便携版

在 Windows 10 或 Windows 11 上安装仓库 `global.json` 指定的 .NET 10 SDK，然后运行：

```bat
publish.bat
```

也可以直接运行：

```powershell
dotnet publish src/MiraFolio.App/MiraFolio.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:Version=1.0.0 `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

生成的入口文件为 `publish\MiraFolio.exe`。

## 代码签名

当前 GitHub Release 的 `MiraFolio.exe` 未进行 Authenticode 签名。Windows SmartScreen 可能在首次
运行时显示“未知发布者”或无法识别应用的提示，企业安全策略也可能阻止运行。

README 和 Release 说明必须提示用户只从官方 Releases 页面下载，并使用 `SHA256SUMS.txt` 核对
便携 ZIP。证书私钥、PFX 文件和密码不得提交到仓库或写入 Actions 日志。

## GitHub Release 工作流

`.github/workflows/release.yml` 支持两种方式：

- 推送 `v1.0.0` 形式的标签：构建并创建 Draft Release；
- 手动运行：默认只上传保留 14 天的候选产物，也可同时创建 Draft Release。

工作流会恢复依赖、构建、测试、生成自包含单文件应用、打包便携 ZIP、生成 SHA-256 和 SBOM，
并为公开仓库中的 ZIP 生成 GitHub artifact attestation。Attestation 能证明产物来自该工作流，
但不等同于 Windows Authenticode 代码签名。

### 使用版本标签发布

确认 `main` 上的版本可以发布且 CI 通过后：

```powershell
git switch main
git pull --ff-only
git tag -a v1.0.0 -m "MiraFolio 1.0.0"
git push origin v1.0.0
```

然后：

1. 在 **Actions** 中等待 **Build GitHub release** 成功；
2. 打开对应 Draft Release；
3. 下载并解压便携 ZIP，在 Windows 10/11 上启动 `MiraFolio.exe`；
4. 使用 `SHA256SUMS.txt` 核对 ZIP；
5. 确认 SBOM、未签名提示和 Release notes 后发布草稿。

不要在草稿验证完成前移动或重复使用同一个版本标签。

### 手动生成候选产物

在 **Actions** → **Build GitHub release** → **Run workflow** 中填写不带 `v` 的版本号：

- `create_draft_release=false`：只生成 Actions artifact；
- `create_draft_release=true`：同时创建对应标签和 Draft Release。

## 升级与移除

- 升级前退出 MiraFolio，将新 ZIP 解压到原文件夹并替换旧文件；
- 若希望保留旧版以便回退，可将新版本解压到另一个固定文件夹；
- 移除应用前先在设置中关闭开机启动，再删除程序文件夹；
- `%APPDATA%\MiraFolio` 中的设置、轮替状态、图片缓存和日志不会随程序文件夹删除。

## 发布前检查

1. 在干净的 Windows 10 和 Windows 11 x64 环境分别解压并启动；
2. 验证首次启动、托盘、壁纸轮替和开机启动开关；
3. 从上一版覆盖升级，确认 `%APPDATA%\MiraFolio` 数据保留；
4. 核对 ZIP 内容只包含预期文件；
5. 重新计算 ZIP 的 SHA-256，并与 `SHA256SUMS.txt` 对照；
6. 在开启 SmartScreen 的干净系统上确认未签名提示与 Release 说明一致。
