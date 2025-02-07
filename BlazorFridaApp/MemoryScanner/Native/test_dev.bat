@echo off
echo Installing Python requirements...
pip install -r requirements.txt

echo.
echo Generating gRPC code...
python generate_protos.py

echo Starting Python gRPC server in a new window...
start "Memory Scanner gRPC Server" cmd /k python ../Server/main.py

echo.
echo Waiting for server to start...
timeout /t 5

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
echo Development tests complete. Check the output above for results.
pause