# Repository Guidelines

## Project Structure & Module Organization
`MiraFolio.sln` contains three projects under `src/`:

- `src/MiraFolio.App`: WPF desktop app, tray integration, views, and view models.
- `src/MiraFolio.Core`: shared models, services, interop, and utility code.
- `src/MiraFolio.Tests`: xUnit tests for core scheduling, settings, image selection, and monitor filtering.

Assets live in `src/MiraFolio.App/Resources`. Release notes or design docs belong in `docs/`. Keep Windows-specific shell and publish helpers at the repository root.

## Build, Test, and Development Commands
- `dotnet build`: builds the full solution for local development.
- `dotnet test src/MiraFolio.Tests/MiraFolio.Tests.csproj`: runs the automated test suite.
- `publish.bat`: publishes a self-contained Windows x64 executable to `publish/MiraFolio.exe`.
- `build-installer.bat 1.0.0`: publishes the app and builds a per-user Inno Setup installer in `dist/`.
- `dotnet publish src/MiraFolio.App/MiraFolio.App.csproj -c Release -r win-x64 --self-contained true`: use this when you need the publish flow without the batch script.

This project targets `.NET 10` and `net10.0-windows`, so build and runtime validation should happen on Windows 10/11.

## Coding Style & Naming Conventions
Use standard C# conventions: 4-space indentation, PascalCase for public types and members, camelCase for locals and private fields with `_fieldName` style. Prefer file-scoped namespaces, nullable-aware code, and small service classes with focused responsibilities. Keep UI state in `ViewModels/`, XAML views in `Views/`, and reusable logic in `MiraFolio.Core`.

No repository-wide formatter config is checked in, so match the existing code style and run your editor’s C# formatter before submitting.

## Testing Guidelines
Tests use `xunit` with `coverlet.collector`. Name test files after the class under test, for example `RotationSchedulerTests.cs`, and use method names in the `Scenario_Expectation` style already used in the suite. Add or update tests for any behavior change in `MiraFolio.Core`. There is no enforced coverage threshold in the repo; keep `dotnet test` passing and manually verify WPF or tray behavior when app code changes.

## Commit & Pull Request Guidelines
Recent commits use short, imperative summaries such as `Refactor SettingsWindow layout...` or `Add immediate selection retry logic...`. Follow that pattern: lead with the primary change, keep the subject specific, and avoid vague messages like `fix stuff`.

Pull requests should include a brief problem statement, the chosen approach, test evidence, and screenshots for UI changes in `src/MiraFolio.App/Views`. Link the related issue when one exists and call out any Windows-only validation steps.

## Configuration & Runtime Notes
User configuration is stored outside the repo in `%APPDATA%\MiraFolio\settings.json` and `%APPDATA%\MiraFolio\state.json`. Do not commit machine-specific settings, generated publish output, or local cache files.
