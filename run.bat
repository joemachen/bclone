@echo off
setlocal EnableDelayedExpansion

REM =============================================================
REM  run.bat - build and launch bclone locally
REM  Captures a timestamped log to logs\ AND shows output live.
REM =============================================================

if not exist logs mkdir logs
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set STAMP=%%i
set LOGFILE=logs\run_!STAMP!.log

REM ---- Stack: C# / .NET 8 + Godot 4 (DESIGN.md decision D1) ----
REM There is nothing to run yet: the Godot view project (src\Bclone.Game)
REM arrives with Phase 0, since the tick loop is a headless library and is
REM exercised by test.bat instead. Fill this in at that point:
REM   set CMD=godot --path src\Bclone.Game
set CMD=echo [run.bat] Nothing to run yet - the Godot project arrives with Phase 0. Use test.bat.
REM --------------------------------------------------------------

echo ================================================================
echo  bclone - RUN
echo  Log: !LOGFILE!
echo ================================================================
echo.

REM Tee: show output live in the console AND write it to the log file.
powershell -NoProfile -Command "& { !CMD! 2>&1 | Tee-Object -FilePath '!LOGFILE!' }"
set EXITCODE=!ERRORLEVEL!

echo.
echo ----------------------------------------------------------------
echo  Exit code: !EXITCODE!    (full log: !LOGFILE!)
if not "!EXITCODE!"=="0" echo  ** RUN FAILED - check the log above. **
echo ----------------------------------------------------------------
pause
endlocal
