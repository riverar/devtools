@echo off
setlocal

\cd "%~dp0\..\ext\binaries" || goto failed
echo Y | erase *.exe || goto failed
echo Y | erase *.dll || goto failed

git reset --hard HEAD || goto failed

xcopy /Q /D /Y "%~dp0\..\ext\binaries\*.dll" "%~dp0\..\output\any\release\bin" || goto failed
xcopy /Q /D /Y "%~dp0\..\ext\binaries\*.exe" "%~dp0\..\output\any\release\bin" || goto failed

REM Everything went ok!
exit /b 0

REM Something not ok :(
:failed
echo ERROR: Failure in script. aborting.
exit /b 1