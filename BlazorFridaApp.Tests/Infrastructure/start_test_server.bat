@echo off
echo Starting Python gRPC server for tests...

REM Set environment variables
set PYTHONPATH=%~dp0..\..\BlazorFridaApp\MemoryScanner;%~dp0..\..\BlazorFridaApp\MemoryScanner\Native

REM Change to the server directory
cd %~dp0..\..\BlazorFridaApp\MemoryScanner\Server

REM Check if Python is available
python --version > nul 2>&1
if errorlevel 1 (
    echo Error: Python is not available in PATH
    echo Please install Python and make sure it's added to PATH
    pause
    exit /b 1
)

REM Install requirements if needed
echo Checking/installing Python requirements...
python -m pip install -r ..\Native\requirements.txt
if errorlevel 1 (
    echo Error: Failed to install Python requirements
    pause
    exit /b 1
)

REM Start the server
echo Starting server on port 50051...
python main.py --port 50051

echo Server stopped
pause 