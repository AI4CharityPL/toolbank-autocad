@echo off
REM Launcher for acad-validators. See rules 33-validators-rule-format.mdc + 34-validators-engine-traps.mdc.
REM The exe path is resolved relative to repo root; override with %ACADMCP_BACKEND_EXE%.
setlocal
if defined ACADMCP_BACKEND_EXE (
    set "EXE=%ACADMCP_BACKEND_EXE%"
) else (
    set "EXE=%~dp0..\src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe"
)
"%EXE%" --category validators %*
