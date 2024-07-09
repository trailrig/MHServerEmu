@echo off
set APACHE_SERVER_ROOT=%cd%\Apache24
start /min "" "%APACHE_SERVER_ROOT%\bin\httpd.exe"
start "" "%cd%\MHServerEmu\MHServerEmu.exe"