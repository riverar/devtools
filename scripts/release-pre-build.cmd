@echo off
setlocal

cd "%~dp0\..\binaries"
echo Y | erase *.*

git reset --hard HEAD