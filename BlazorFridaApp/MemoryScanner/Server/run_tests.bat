@echo off
echo.
echo Starting Python gRPC server for integration tests...

REM Check if Python is available
python --version > nul 2>&1
if errorlevel 1 (
    echo Error: Python is not available in PATH
    echo Please install Python and make sure it's added to PATH
    pause
    exit /b 1
)

echo Server will be started on port 50051
echo Please keep this window open while running tests
echo.

cd %~dp0
set PYTHONPATH=%PYTHONPATH%;%~dp0..;%~dp0..\Native


echo.
echo Generating gRPC code...
python ..\Native\generate_protos.py
popd
echo.

REM Install requirements if needed
echo Checking/installing Python requirements...
python -m pip install -r ..\Native\requirements.txt
if errorlevel 1 (
    echo Error: Failed to install Python requirements
    pause
    exit /b 1
)

echo Starting server...
start "Memory Scanner gRPC Server" cmd /k python main.py --port 50051

echo.
echo Server window has been opened
echo Waiting for server to initialize (5 seconds)...
timeout /t 5 > nul
echo.
echo Server should be ready now
echo You can now run the tests in a separate window
echo To stop the server, close this window or the server window
echo.