@echo off
setlocal 
cd %~dp0

for /d %%v in (ext\*) do ( 
    pushd %%v
    call git reset --hard HEAD    
    call git pull 
    popd
)