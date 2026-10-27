@echo off
rem Plays the generated city preview (traffic, people, phone) without opening the editor.
rem Usage: play_city.bat            the city preview
rem        play_city.bat morning    the morning (home and street)
rem        play_city.bat res://path/to/scene.tscn
rem Set GODOT_BIN to your Godot .NET executable if it isn't C:\Godot\Godot.exe.

setlocal
if "%GODOT_BIN%"=="" set "GODOT_BIN=C:\Godot\Godot.exe"

set "SCENE=res://scenes/stages/city/city_preview.tscn"
if /i "%~1"=="morning" set "SCENE=res://scenes/main/morning.tscn"
if /i "%~1"=="city" set "SCENE=res://scenes/stages/city/city_preview.tscn"
if /i "%~1" neq "" if /i "%~1" neq "morning" if /i "%~1" neq "city" set "SCENE=%~1"

if not exist "%GODOT_BIN%" (
    echo Godot not found at "%GODOT_BIN%". Set GODOT_BIN to your Godot .NET executable.
    pause
    exit /b 1
)

cd /d "%~dp0"
start "" "%GODOT_BIN%" --path . "%SCENE%"
