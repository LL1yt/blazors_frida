@echo off
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