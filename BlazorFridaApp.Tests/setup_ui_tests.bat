@echo off
echo Installing Playwright browsers...
pwsh -Command "& { $env:PLAYWRIGHT_BROWSERS_PATH='%~dp0playwright-browsers'; npx playwright install chromium }"

echo Creating test artifacts directories...
if not exist "%~dp0TestResults" mkdir "%~dp0TestResults"
if not exist "%~dp0TestResults\Screenshots" mkdir "%~dp0TestResults\Screenshots"
if not exist "%~dp0TestResults\Traces" mkdir "%~dp0TestResults\Traces"

echo.
echo Running component tests...
dotnet test --filter "FullyQualifiedName~BlazorFridaApp.Tests.Components" --results-directory "%~dp0TestResults"

echo.
echo Running UI integration tests...
dotnet test --filter "FullyQualifiedName~BlazorFridaApp.Tests.UI" --results-directory "%~dp0TestResults"

echo.
echo Generating test report...
pwsh -Command "& { npx playwright show-report '%~dp0TestResults' }"

echo.
echo All tests completed. Results available in TestResults directory.