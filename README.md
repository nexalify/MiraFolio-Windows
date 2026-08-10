# MiraFolio for Windows

<p align="center">
  <img src="src/MiraFolio.App/Resources/app-icon.png" alt="MiraFolio icon" width="112" />
</p>

<p align="center">
  <strong>Give every display its own wallpaper rhythm.</strong>
</p>

<p align="center">
  A local-first wallpaper rotation app for real multi-monitor setups and very large collections.
</p>

<p align="center">
  <a href="https://github.com/nexalify/MiraFolio-Windows/releases/latest">
    <img src="https://img.shields.io/badge/Downloads-GitHub%20Releases-7C3AED?style=for-the-badge&logo=github" alt="Open GitHub Releases" />
  </a>
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="https://www.mirafolio.app/?utm_source=github&utm_medium=readme&utm_campaign=windows_mobile_cross_promo&utm_content=en_top_website">Official Website</a> ·
  <a href="docs/privacy.md">Privacy</a> ·
  <a href="https://github.com/nexalify/MiraFolio-Windows/issues">Support</a>
</p>

<p align="center">
  <img src="docs/images/mirafolio-overview.png" alt="MiraFolio managing different wallpapers and rotation settings across two displays" width="1000" />
</p>

> [!NOTE]
> **MiraFolio 1.0.0 is now available.** Download the portable Windows x64 build from the
> [official GitHub Release](https://github.com/nexalify/MiraFolio-Windows/releases/latest).

## A better wallpaper experience for every display

- **Independent display control.** Give each monitor its own image folder, rotation interval,
  playback order, and enabled state.
- **Built for tens of thousands of wallpapers.** Folders are indexed in the background and image
  metadata is cached, so image dimensions do not need to be reread on every start.
- **Curate your collection as you watch.** Dismiss a wallpaper you do not like with one click. It
  leaves future rotation without deleting the source file and can be restored from the Recycle Bin.
- **Reliable when your setup changes.** Disconnecting one display does not hide or interrupt the
  others, and newly connected displays can be configured as soon as Windows detects them.
- **Smarter image choices.** Match landscape images to landscape displays, portrait images to
  portrait displays, and skip images that are too small.
- **Private by design.** MiraFolio works locally without an account, cloud service, analytics, or
  wallpaper uploads.

## What you can do

- Rotate without repeats, sequentially, or in reverse order.
- Pause rotation on an individual display while a game, presentation, or video is fullscreen.
- Change, reveal, or dismiss the current wallpaper with quick actions.
- Review dismissed images in the Recycle Bin, restore them later, or permanently delete them only
  after explicit confirmation.
- Start with Windows and keep everyday controls in the notification area.
- Manage mixed landscape and portrait displays from one visual layout.

## Download and run

Open the [latest MiraFolio Release](https://github.com/nexalify/MiraFolio-Windows/releases/latest),
expand **Assets**, and download `MiraFolio-<version>-win-x64-portable.zip`. For the current release,
the file is `MiraFolio-1.0.0-win-x64-portable.zip`. Extract the ZIP to a permanent folder, then run
`MiraFolio.exe`. No installation or separate .NET runtime is required.

The automatically generated **Source code (zip)** and **Source code (tar.gz)** files are source
archives, not Windows applications. Only download MiraFolio from this repository's official
Releases page.

## MiraFolio on mobile

Take the same photo-first wallpaper experience beyond your desktop. MiraFolio is designed natively
for each mobile platform, with controls and automation that fit the operating system.

### iPhone & iPad — Available now

Turn favorite photos into a no-repeat wallpaper rotation, curate the collection with a swipe,
automate changes with Apple Shortcuts, and keep private photos in a protected album.

[Explore MiraFolio for iPhone & iPad](https://www.mirafolio.app/ios/?utm_source=github&utm_medium=readme&utm_campaign=windows_mobile_cross_promo&utm_content=en_ios) ·
[Download on the App Store](https://apps.apple.com/app/mirafolio-wallpaper-shuffle/id6791570584)

### Android — In release testing

Preview first, then apply wallpapers directly to the home screen, lock screen, or both. Add
scheduled rotation and quick actions without giving up control of the collection.

[Explore MiraFolio for Android](https://www.mirafolio.app/android/?utm_source=github&utm_medium=readme&utm_campaign=windows_mobile_cross_promo&utm_content=en_android)

**Discover MiraFolio across every screen:**
[Visit the official MiraFolio website](https://www.mirafolio.app/?utm_source=github&utm_medium=readme&utm_campaign=windows_mobile_cross_promo&utm_content=en_mobile_website).

## Get started

1. Open MiraFolio and select a display in the visual monitor layout.
2. Choose the wallpaper folder for that display.
3. Set the rotation interval, playback order, and any smart matching options.
4. Enable rotation. MiraFolio continues running from the notification area.

Repeat the first three steps for any other display you want to customize.

## System requirements

- Windows 10 or Windows 11
- x64 PC
- No separate .NET installation for official self-contained releases

## Privacy, help, and project information

Wallpapers, settings, caches, and logs stay on your computer. Read the [privacy notes](docs/privacy.md)
for details. For bugs or feature ideas, use [GitHub Issues](https://github.com/nexalify/MiraFolio-Windows/issues).
Please report security concerns through the process described in [SECURITY.md](SECURITY.md).

Developer and release documentation is available in [CONTRIBUTING.md](CONTRIBUTING.md),
[docs/architecture.md](docs/architecture.md), and [docs/windows-release.md](docs/windows-release.md).

MiraFolio source code is available under the [MIT License](LICENSE). The product name, logo, and
icon are covered by the [brand asset policy](TRADEMARKS.md).
