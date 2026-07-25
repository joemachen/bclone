@echo off
setlocal EnableDelayedExpansion

REM =============================================================
REM  test.bat - run the bclone test suite locally
REM  Includes unit tests + the determinism test (see METHODOLOGY.md).
REM =============================================================

if not exist logs mkdir logs
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set STAMP=%%i
set LOGFILE=logs\test_!STAMP!.log

REM ---- Stack: C# / .NET 8 + Godot 4 (DESIGN.md decision D1) ----
REM Runs the whole solution, which is the sim library and its tests.
REM The determinism suite is part of this run - see METHODOLOGY.md §3.
set CMD=dotnet test bclone.sln --nologo
REM --------------------------------------------------------------

echo ================================================================
echo  bclone - TEST
echo  Log: !LOGFILE!
echo ================================================================
echo.

powershell -NoProfile -Command "& { !CMD! 2>&1 | Tee-Object -FilePath '!LOGFILE!' }"
set EXITCODE=!ERRORLEVEL!

echo.
echo ----------------------------------------------------------------
if "!EXITCODE!"=="0" (
  echo  ALL TESTS PASSED    (full log: !LOGFILE!)
) else (
  echo  ** TESTS FAILED (exit !EXITCODE!) - check the log above. **
)
echo ----------------------------------------------------------------
pause
endlocal
