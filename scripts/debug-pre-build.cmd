@echo off
setlocal

if exist "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.dll" xcopy  /Q /D /Y "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.dll" "%~dp0\..\binaries"
if exist "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.Debug.dll" xcopy /Q /D /Y "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.Debug.dll" "%~dp0\..\binaries"
if exist "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.Engine.Client.dll" xcopy /Q /D /Y "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.Engine.Client.dll" "%~dp0\..\binaries"

xcopy /Q /D /Y "%~dp0\..\ext\binaries\*.dll" "%~dp0\..\output\any\debug\bin"
xcopy /Q /D /Y "%~dp0\..\ext\binaries\*.exe" "%~dp0\..\output\any\debug\bin"