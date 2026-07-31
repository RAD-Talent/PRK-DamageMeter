@echo off
title PRK Damage Meter - installer
echo.
echo  PRK Damage Meter - building from source using the compiler included with Windows
echo.
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find the .NET Framework compiler. It ships with Windows 10/11 -
  echo if this fails, install ".NET Framework 4.8" from Microsoft and re-run.
  pause
  exit /b 1
)
rem close a running meter so the compiler can overwrite the exe
taskkill /IM PRK-DamageMeter.exe /F >nul 2>&1
timeout /t 1 /nobreak >nul
set ICON=
if exist prkdm.ico set ICON=/win32icon:prkdm.ico
"%CSC%" /nologo /target:winexe %ICON% /out:"PRK-DamageMeter.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll PRKDamageMeter.cs
if errorlevel 1 (
  echo.
  echo Build failed - ping .everkill on Discord with a screenshot of this window.
  pause
  exit /b 1
)
echo.
echo  Done! PRK-DamageMeter.exe is ready in this folder.
echo  Starting it now... Right-click the meter for all options.
echo.
start "" "PRK-DamageMeter.exe"
timeout /t 3 >nul
