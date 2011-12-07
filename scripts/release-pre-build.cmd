@echo off
setlocal

cd "%~dp0\..\binaries"
echo Y | erase *.*

git reset --hard HEAD

xcopy /Q /D /Y "%~dp0\..\binaries\*.dll" "%~dp0\..\output\any\release\bin"
xcopy /Q /D /Y "%~dp0\..\binaries\*.exe" "%~dp0\..\output\any\release\bin"