@echo off
rem Builds LabLock.exe WITHOUT Visual Studio, using the C# compiler that is part of Windows.
rem Double-click this file; LabLock.exe appears next to it. That one file is the whole program:
rem the school image and the push script are built into it.
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto nocompiler

pushd "%~dp0LabLock"
"%CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ /out:"%~dp0LabLock.exe" /win32manifest:app.manifest /win32icon:app.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll /resource:LabLock-school.png,LabLock.school.png /resource:..\Push-LabLock.ps1,LabLock.push.ps1 *.cs Properties\*.cs > "%~dp0build-log.txt" 2>&1
set "RESULT=%ERRORLEVEL%"
popd
type "%~dp0build-log.txt"

echo.
if not "%RESULT%"=="0" goto failed
echo Built: "%~dp0LabLock.exe"
if not defined CI pause
exit /b 0

:failed
echo The build FAILED. The error lines above are also in build-log.txt - send that file back.
if not defined CI pause
exit /b 1

:nocompiler
echo The C# compiler of .NET Framework 4 was not found on this PC.
if not defined CI pause
exit /b 1
