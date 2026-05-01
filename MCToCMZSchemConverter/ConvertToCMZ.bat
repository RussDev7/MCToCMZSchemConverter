@echo off
setlocal

if "%~1"=="" (
    echo Drag a Minecraft .schem file onto this batch file.
    pause
    exit /b 1
)

set "INPUT=%~1"
set "EXE=%~dp0MCToCMZSchemConverter.exe"
set "MAP=%~dp0block-map.json"

REM Output folder next to this batch/exe.
set "OUTPUT_DIR=%~dp0output"

REM Converted file name.
set "OUTPUT=%OUTPUT_DIR%\%~n1_cmz.schem"

REM Create output folder if it does not exist.
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

"%EXE%" "%INPUT%" "%OUTPUT%" "%MAP%" --save-air

echo.
echo Converted schematic saved to:
echo "%OUTPUT%"
echo.
pause