@echo off
echo Installing Python requirements...
pip install -r requirements.txt

echo.
echo Generating gRPC code...
python generate_protos.py

echo.
echo Starting Python gRPC server in a new window...
start "Memory Scanner gRPC Server" cmd /k python memory_scanner_server.py

echo.
echo Waiting for server to start...
timeout /t 5

echo.
echo Starting .NET application in a new window...
pushd ..\..
start "BlazorFridaApp" cmd /k dotnet run
popd

echo.
echo Waiting for .NET application to start...
timeout /t 10

echo.
echo Running health check tests with .NET...
python test_health.py --with-dotnet

echo.
echo Running server stability test with Frida...
python test_health.py --stability

echo.
echo Running health check tests after Frida with .NET...
python test_health.py --with-dotnet

echo.
echo Running state management tests...
python test_state.py

echo.
echo Running pattern scanner tests...
python test_metrics.py --pattern-scanner

echo.
echo Running value freezer tests...
python test_metrics.py --value-freezer

echo.
echo Running cache system tests...
python test_metrics.py --cache-system

echo.
echo Running integrated functionality tests...
python test_metrics.py --integrated

echo.
echo Test complete. Check the output above for results.
pause