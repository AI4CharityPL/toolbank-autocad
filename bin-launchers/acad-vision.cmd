@echo off
REM Launcher for acad-vision. Spawns the Python sidecar (idempotent) BEFORE the
REM .NET MCP host, so the sidecar HTTP API is reachable when the first tool fires.
REM Override with %ACADMCP_BACKEND_EXE% / %ACADMCP_VISION_PORT%.
setlocal

REM 1) Ensure the Python vision sidecar is up (idempotent - won't double-start).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts\start-vision.ps1" -EnsureRunning -WaitHealthy

if defined ACADMCP_BACKEND_EXE (
    set "EXE=%ACADMCP_BACKEND_EXE%"
) else (
    set "EXE=%~dp0..\src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe"
)

REM 2) Start the .NET MCP host bound to the 'vision' category.
"%EXE%" --category vision %*
