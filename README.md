# MiraFolio for Windows

<p align="center">
  <img src="src/MiraFolio.App/Resources/app-icon.png" alt="MiraFolio icon" width="112" />
</p>

<p align="center">
  A local-first, multi-monitor wallpaper rotation app for Windows 10 and Windows 11.
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="docs/privacy.md">Privacy</a> ·
  <a href="CONTRIBUTING.md">Contributing</a> ·
  <a href="SECURITY.md">Security</a>
</p>

## Highlights

- Discover the active Windows display topology and visualize the real monitor layout.
- Configure a different image folder, interval, playback order, and enabled state per display.
- Rotate randomly without repeats, sequentially, or in reverse order.
- Match landscape and portrait images to each display and filter low-resolution files.
- Recursively index large local libraries in the background and persist an image-dimension cache.
- Pause rotation when a full-screen window is present on a display.
- Exclude unwanted images without deleting source files, with restore and explicit permanent delete.
- Run quietly in the notification area with startup and desktop quick actions.
- Recover from stale Windows wallpaper targets so a disconnected display does not hide active ones.

MiraFolio does not require an account or cloud service. Wallpapers, settings, caches, and logs stay
on the local machine. See the [privacy notes](docs/privacy.md) for details.

## Downloads

The project is currently under private release review. Signed installer and portable downloads will
be published through [GitHub Releases](https://github.com/luogreen/MiraFolio-Windows/releases) after
the first release candidate passes Windows 10/11 installation, upgrade, and uninstall checks.

Planned release files:

- `MiraFolio-Setup-<version>-win-x64.exe`
- `MiraFolio-<version>-win-x64-portable.zip`
- `SHA256SUMS.txt`
- `MiraFolio-<version>-sbom.spdx.json`

Do not treat CI artifacts as official downloads unless they are attached to a published release.

## Requirements

For users:

- Windows 10 or Windows 11, x64
- No separate .NET installation for self-contained releases

For contributors:

- Windows 10 or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Inno Setup 6 or 7 when building the installer

## Build and test

```powershell
dotnet restore MiraFolio.sln
dotnet build MiraFolio.sln -c Release --no-restore
dotnet test src/MiraFolio.Tests/MiraFolio.Tests.csproj -c Release --no-build
```

Create the self-contained executable with `publish.bat`, or create the installer with:

```bat
build-installer.bat 1.0.0
```

Outputs are written to `publish/` and `dist/`; both directories are ignored by Git. Detailed
signing and validation steps are in the [Windows release guide](docs/windows-release.md).

## Repository layout

```text
src/
├── MiraFolio.App/    WPF application, views, view models, tray, and desktop integration
├── MiraFolio.Core/   Models, monitor and wallpaper services, scheduling, and image selection
└── MiraFolio.Tests/  xUnit tests for core behavior
```

The application targets `net10.0-windows` and uses WPF, `IDesktopWallpaper`,
CommunityToolkit.Mvvm, H.NotifyIcon.Wpf, Microsoft.Extensions.Hosting, and xUnit.

## Local data

MiraFolio stores its data under `%APPDATA%\MiraFolio`:

| File | Purpose |
| --- | --- |
| `settings.json` | Global options, per-display assignments, and excluded-image records |
| `state.json` | Current wallpapers, recent history, and random playback state |
| `image_dim_cache.json` | Persistent image dimensions used by the background index |
| `mirafolio.log` | Rotating operational log; may contain local paths and display identifiers |

## Contributing and security

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Please report suspected
vulnerabilities through GitHub's private vulnerability reporting flow described in
[SECURITY.md](SECURITY.md), not through a public issue.

## License and trademarks

Source code is available under the [MIT License](LICENSE). Third-party components remain under
their respective licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

The MIT License does not grant rights to present modified builds as official MiraFolio products.
See [TRADEMARKS.md](TRADEMARKS.md) for the name, logo, and icon policy.
