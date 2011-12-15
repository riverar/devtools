@echo off
erase *.msi 
erase *.wixpdb
autopackage template.autopkg outercurve.autopkg coapp.devtools.autopkg 
