@echo off
setlocal

echo [1/3] Restoring packages...
dotnet restore IProSensorPanel.csproj || goto :fail

echo [2/3] Building Release...
dotnet build IProSensorPanel.csproj -c Release || goto :fail

echo [3/3] Publishing single-file EXE (self-contained)...
dotnet publish IProSensorPanel.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false || goto :fail

echo.
echo DONE
echo EXE path:
echo bin\Release\net8.0-windows\win-x64\publish\IProSensorPanel.exe
exit /b 0

:fail
echo.
echo FAILED - check errors above.
exit /b 1
