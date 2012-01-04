@echo off
cd %~dp0

erase *.msi 
erase *.wixpdb

..\output\any\Release\bin\autopackage outercurve.autopkg coapp.devtools.autopkg  || goto EOF:

for %%v  in (*.msi) do curl -T  %%v http://coapp.org/upload/ || goto EOF:
echo "Uploaded to repository"