@echo off
setlocal enabledelayedexpansion
chcp 936 >nul

title Qt6 Icon Generator
color 0A

echo ================================================
echo           Qt6 Icon Generator
echo ================================================
echo.

set "MSYS2_PATH=C:\msys64"
set "MINGW_PATH=%MSYS2_PATH%\ucrt64"
set "PATH=%MINGW_PATH%\bin;%MSYS2_PATH%\usr\bin;%PATH%"

echo [1/2] Compiling icon generator...
echo.

g++ -std=c++17 -finput-charset=UTF-8 -fexec-charset=GBK -o ico_qt.exe src/create_icon_qt.cpp -I%MINGW_PATH%\include\qt6 -I%MINGW_PATH%\include\qt6\QtCore -I%MINGW_PATH%\include\qt6\QtGui -L%MINGW_PATH%\lib -lQt6Core -lQt6Gui

if errorlevel 1 (
    echo [ERROR] Compilation failed!
    pause
    exit /b 1
)

echo [OK] Compiled successfully
echo.

echo [2/2] Generating icon...
echo.

ico_qt.exe

if errorlevel 1 (
    echo [ERROR] Icon generation failed!
    pause
    exit /b 1
)

echo.
echo ================================================
echo [SUCCESS] Icon generated!
echo ================================================
echo.

del ico_qt.exe 2>nul

pause
