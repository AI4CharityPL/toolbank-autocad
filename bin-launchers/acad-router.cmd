@echo off
REM AutoCAD MCP - router launcher.
REM Thin wrapper. Edit only the binary path / dotnet runtime if needed.
set "ACADMCP_ROOT=%~dp0.."
dotnet "%ACADMCP_ROOT%\src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.dll" --category router %*
