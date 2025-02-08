@echo off
echo Installing Playwright browsers...
pwsh -Command "& { $env:PLAYWRIGHT_BROWSERS_PATH='%~dp0playwright-browsers'; npx playwright install chromium }"

echo.
echo Running component tests...
dotnet test --filter "FullyQualifiedName~BlazorFridaApp.Tests.Components"

echo.
echo Running UI integration tests...
dotnet test --filter "FullyQualifiedName~BlazorFridaApp.Tests.UI"

echo.
echo All tests completed.