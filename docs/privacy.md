# Privacy

MiraFolio for Windows is designed as a local-first desktop application.

## Data processed

The application reads image paths and basic image dimensions from folders selected by the user.
It stores monitor-specific configuration, recent wallpaper history, shuffle state, excluded-image
records, an image-dimension cache, and operational logs under `%APPDATA%\MiraFolio`.

## Network and telemetry

The application does not include analytics, advertising, accounts, cloud synchronization,
telemetry upload, or an update service. It does not upload wallpapers, file names, settings, or
logs. Normal Windows behavior and third-party distribution channels may perform their own network
requests independently of MiraFolio.

## Retention and deletion

Uninstalling the application leaves `%APPDATA%\MiraFolio` in place so settings survive upgrades.
Users may delete that directory after exiting MiraFolio to remove all application-managed local
data. Removing an image in MiraFolio normally adds it to the application's exclusion list and does
not delete the source file; permanent deletion is an explicit, confirmed action.

## Diagnostic reports

Logs can contain local file paths and monitor identifiers. Review and redact `mirafolio.log`,
screenshots, and crash data before attaching them to an issue or security report.
