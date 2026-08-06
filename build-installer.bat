@echo off
setlocal

set "APP_VERSION=%~1"
if not defined APP_VERSION set "APP_VERSION=1.0.0"

set "REPO_DIR=%~dp0"
set "PUBLISH_DIR=%REPO_DIR%publish"
set "DIST_DIR=%REPO_DIR%dist"
set "APP_EXE=%PUBLISH_DIR%\MiraFolio.exe"
set "SETUP_EXE=%DIST_DIR%\MiraFolio-Setup-%APP_VERSION%-win-x64.exe"

echo Building MiraFolio %APP_VERSION% for Windows x64...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
dotnet publish "%REPO_DIR%src\MiraFolio.App\MiraFolio.App.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:Version=%APP_VERSION% ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o "%PUBLISH_DIR%"
if errorlevel 1 exit /b 1

call :sign_file "%APP_EXE%"
if errorlevel 1 exit /b 1

call :find_iscc
if not defined ISCC_EXE (
  echo.
  echo Inno Setup compiler was not found.
  echo Install Inno Setup 7 from https://jrsoftware.org/isdl.php and run this script again.
  exit /b 1
)

echo Building installer with "%ISCC_EXE%"...
if defined MIRAFOLIO_SIGNTOOL (
  "%ISCC_EXE%" ^
    "/DMyAppVersion=%APP_VERSION%" ^
    "/DEnableSigning=1" ^
    "/SMiraFolioAuthenticode=$q%MIRAFOLIO_SIGNTOOL%$q sign /sha1 $q%MIRAFOLIO_CERT_SHA1%$q /fd SHA256 /tr $q%MIRAFOLIO_TIMESTAMP_URL%$q /td SHA256 /d $qMiraFolio$q $f" ^
    "%REPO_DIR%installer\MiraFolio.iss"
) else (
  "%ISCC_EXE%" "/DMyAppVersion=%APP_VERSION%" "%REPO_DIR%installer\MiraFolio.iss"
)
if errorlevel 1 exit /b 1

echo.
echo Installer created: "%SETUP_EXE%"
if not defined MIRAFOLIO_SIGNTOOL echo INFO: The installer is unsigned, as expected for the current GitHub release flow.
exit /b 0

:find_iscc
set "ISCC_EXE="
where ISCC.exe >nul 2>nul
if not errorlevel 1 set "ISCC_EXE=ISCC.exe"
if defined ISCC_EXE exit /b 0

if exist "%ProgramFiles%\Inno Setup 7\ISCC.exe" set "ISCC_EXE=%ProgramFiles%\Inno Setup 7\ISCC.exe"
if defined ISCC_EXE exit /b 0
if exist "%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe" set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
if defined ISCC_EXE exit /b 0
if exist "%LocalAppData%\Programs\Inno Setup 7\ISCC.exe" set "ISCC_EXE=%LocalAppData%\Programs\Inno Setup 7\ISCC.exe"
if defined ISCC_EXE exit /b 0
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
exit /b 0

:sign_file
if not defined MIRAFOLIO_SIGNTOOL exit /b 0
if not defined MIRAFOLIO_CERT_SHA1 (
  echo MIRAFOLIO_CERT_SHA1 is required when MIRAFOLIO_SIGNTOOL is set.
  exit /b 1
)
if not defined MIRAFOLIO_TIMESTAMP_URL (
  echo MIRAFOLIO_TIMESTAMP_URL is required when MIRAFOLIO_SIGNTOOL is set.
  exit /b 1
)

echo Signing "%~1"...
"%MIRAFOLIO_SIGNTOOL%" sign ^
  /sha1 "%MIRAFOLIO_CERT_SHA1%" ^
  /fd SHA256 ^
  /tr "%MIRAFOLIO_TIMESTAMP_URL%" ^
  /td SHA256 ^
  /d "MiraFolio" ^
  "%~1"
exit /b %errorlevel%
