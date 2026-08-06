# Contributing to MiraFolio for Windows

Thanks for helping improve MiraFolio. The project is a Windows-only WPF application, so
runtime and UI changes must be validated on Windows 10 or Windows 11.

## Before opening a change

- Search existing issues before filing a duplicate.
- Use a focused branch and keep unrelated changes separate.
- For behavior changes, describe the user-visible problem before the implementation.
- Do not include personal settings, wallpapers, logs, crash dumps, signing material, or
  generated binaries.

## Development setup

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet restore
dotnet build MiraFolio.sln -c Release --no-restore
dotnet test src/MiraFolio.Tests/MiraFolio.Tests.csproj -c Release --no-build
```

To build the self-contained Windows executable:

```powershell
dotnet publish src/MiraFolio.App/MiraFolio.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish
```

## Code and tests

- Follow standard C# naming and the style in the surrounding files.
- Keep UI state in `ViewModels`, WPF layout in `Views`, and reusable behavior in
  `MiraFolio.Core`.
- Add xUnit coverage for changes in core behavior.
- Use test names in the `Scenario_Expectation` style.
- Include screenshots for changes under `src/MiraFolio.App/Views`.

## Pull requests

Describe what changed, why it changed, user impact, and the commands used to validate it.
Windows-only manual checks should be listed explicitly. By contributing, you agree that your
contribution is licensed under the repository's MIT License.
