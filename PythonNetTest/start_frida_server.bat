@echo off
echo Starting Frida server...
cd /d "%~dp0"
.venv\Scripts\frida-server.exe 