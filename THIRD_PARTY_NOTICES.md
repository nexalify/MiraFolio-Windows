# Third-party notices

MiraFolio for Windows uses third-party packages distributed under their own licenses. NuGet
restore retrieves the applicable license metadata and package contents. The principal direct
dependencies are:

| Component | Purpose | License/source |
| --- | --- | --- |
| CommunityToolkit.Mvvm | MVVM source generators and primitives | [Microsoft repository](https://github.com/CommunityToolkit/dotnet) |
| H.NotifyIcon.Wpf | Windows notification-area integration | [project repository](https://github.com/HavenDV/H.NotifyIcon) |
| Microsoft.Extensions.* | Hosting, dependency injection, and logging | [dotnet/runtime](https://github.com/dotnet/runtime) |
| Microsoft.Windows.CsWin32 | Windows API source generation | [microsoft/CsWin32](https://github.com/microsoft/CsWin32) |
| xUnit.net | Automated testing | [xunit/xunit](https://github.com/xunit/xunit) |
| coverlet | Test coverage collection | [coverlet-coverage/coverlet](https://github.com/coverlet-coverage/coverlet) |

This notice is informational and does not replace the license files shipped with each package.
Run `dotnet list package --include-transitive` for the exact dependency graph of a checkout.
