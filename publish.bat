@echo off
setlocal
echo Building MiraFolio for Windows (Release, win-x64, self-contained)...

if exist publish rmdir /s /q publish

dotnet publish src\MiraFolio.App\MiraFolio.App.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o publish
if errorlevel 1 exit /b 1

echo.
echo Published to: publish\MiraFolio.exe
exit /b 0
