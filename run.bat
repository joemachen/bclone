@echo off
setlocal EnableDelayedExpansion

REM =============================================================
REM  run.bat - build and launch bclone locally
REM  Captures a timestamped log to logs\ AND shows output live.
REM
REM  Godot lives outside the repo, so its path comes from the
REM  environment. Override it if yours is somewhere else:
REM    set GODOT=C:\path\to\Godot_v4.7.1-stable_mono_win64.exe
REM =============================================================

if not exist logs mkdir logs
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set STAMP=%%i
set LOGFILE=logs\run_!STAMP!.log

if "!GODOT!"=="" set GODOT=D:\Projects\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe

if not exist "!GODOT!" (
  echo  ** Godot not found at: !GODOT!
  echo  ** Set GODOT to your editor executable and run this again.
  pause
  exit /b 1
)

REM ---- Build the VIEW explicitly, every time ----------------------
REM bclone.sln deliberately excludes src\Bclone.Game (D11), so a
REM solution build compiles the sim and the tests and leaves the
REM game's assembly untouched. Skipping this is how a build menu was
REM written, wired up, and silently never appeared: Godot ran a
REM day-old DLL. This is not optional and it is why run.bat exists
REM rather than a bare godot command.
echo Building the view...
dotnet build src\Bclone.Game\Bclone.Game.csproj --nologo -v q
if not "!ERRORLEVEL!"=="0" (
  echo  ** The view did not build - not launching a stale assembly. **
  pause
  exit /b 1
)

set CMD=^& '!GODOT!' --path src/Bclone.Game
REM --------------------------------------------------------------

echo ================================================================
echo  bclone - RUN
echo  Godot: !GODOT!
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
