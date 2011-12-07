@echo off
setlocal

copy "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.dll" "%~dp0\..\binaries"
copy "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.Debug.dll" "%~dp0\..\binaries"
copy "%~dp0\..\..\coapp\output\any\debug\bin\CoApp.Toolkit.Engine.Client.dll" "%~dp0\..\binaries"
