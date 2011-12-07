REM wmic product get name 
c:\apps\bin\coapp.service.exe --uninstall
sc delete "CoApp Package Installer Service"

wmic product where vendor='OUTERCURVE FOUNDATION' call uninstall
rmdir /s /q c:\apps
