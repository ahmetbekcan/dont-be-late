@echo off
rem Plays the game (the morning) without opening the editor.
rem Usage: play_game.bat          the game
rem        play_game.bat looks    the game, straight into the character creator to try clothes and looks
rem In the game: Tab opens the inventory ("Change look" under the figure), or type "creator" in the console (`).
rem In the creator: drag to turn, scroll (or pinch) to zoom to the face, drag up and down while zoomed to look along the body.
rem Set GODOT_BIN to your Godot .NET executable if it isn't C:\Godot\Godot.exe.

setlocal
if "%GODOT_BIN%"=="" set "GODOT_BIN=C:\Godot\Godot.exe"

if not exist "%GODOT_BIN%" (
    echo Godot not found at "%GODOT_BIN%". Set GODOT_BIN to your Godot .NET executable.
    pause
    exit /b 1
)

set "EXTRA="
if /i "%~1"=="looks" set "EXTRA=-- --creator"
if /i "%~1"=="creator" set "EXTRA=-- --creator"

cd /d "%~dp0"
start "" "%GODOT_BIN%" --path . res://scenes/main/morning.tscn %EXTRA%
