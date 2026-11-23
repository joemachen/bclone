@echo off
rem The title's four seasons (A189, specs\title-screen-art.md section 3): one command, no manual steps.
rem   tools\title-capture\capture.bat [out_dir]
rem Puts village.save in a scratch user folder as its only save (full screen off), opens the game in a window for a few
rem seconds, and writes spring.png, summer.png, autumn.png, winter.png (3840 x 2160) to out_dir (default %TEMP%\kith-title-capture).
setlocal
if "%GODOT%"=="" set "GODOT=D:\Projects\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
set "HERE=%~dp0"
set "OUT=%~1"
if "%OUT%"=="" set "OUT=%TEMP%\kith-title-capture"
set "USERDIR=%TEMP%\kith-title-capture-user-%RANDOM%"
set "SAVES=%USERDIR%\Godot\app_userdata\bclone\saves\title"
mkdir "%SAVES%" 2>nul
copy /y "%HERE%village.save" "%SAVES%\autosave-1.save" >nul
> "%USERDIR%\Godot\app_userdata\bclone\settings.json" echo { "version": 1, "toggles": { "full_screen": false } }
mkdir "%OUT%" 2>nul
set "APPDATA=%USERDIR%"
set "TITLE_OUT=%OUT%"
pushd "%HERE%..\.."
dotnet build src\Kith.Game\Kith.Game.csproj -nologo -v q
rem Joe: "can we pop all test windows onto my 2nd monitor?" - the capture window opens on screen 1.
"%GODOT%" --screen 1 --path src\Kith.Game -s "%HERE%capture.gd"
set "CODE=%ERRORLEVEL%"
popd
rmdir /s /q "%USERDIR%"
exit /b %CODE%
