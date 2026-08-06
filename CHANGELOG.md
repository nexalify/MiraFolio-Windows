# Changelog

All notable changes to MiraFolio for Windows will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Multi-monitor wallpaper rotation with independent folders, intervals, and playback order.
- Orientation and minimum-resolution filtering for local image libraries.
- Persistent shuffle queues, image exclusion, and an in-app recycle bin.
- Tray controls, startup registration, full-screen pause, and desktop quick actions.
- Localized UI resources for English, Simplified and Traditional Chinese, German, Spanish,
  French, Japanese, Korean, and Russian.
- Windows CI and draft release automation.

### Changed

- Monitor discovery now isolates stale Windows wallpaper targets so one disconnected display
  cannot hide the remaining active displays.
- Display hot-plug refresh is debounced and retried while Windows updates its topology.

## [1.0.0] - Unreleased

Initial public release planned after private review, signing, and Windows 10/11 validation.
