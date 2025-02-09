@echo off
echo Setting up test environment variables...
set BLAZOR_FRIDA_USB=false

echo Installing Python requirements...
pushd ..\BlazorFridaApp\MemoryScanner\Server
pip install -r requirements.txt

echo.
echo Generating gRPC code...
python generate_protos.py
popd

echo.
echo Environment setup complete.
pause 