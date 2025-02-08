@echo off
echo.
echo Starting Python gRPC server in a new window...
cd %~dp0
set PYTHONPATH=%PYTHONPATH%;%~dp0..;%~dp0..\Native
start "Memory Scanner gRPC Server" cmd /k python main.py

echo.
echo Waiting for server to start...
timeout /t 5