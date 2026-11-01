@echo off
setlocal enabledelayedexpansion
chcp 936 >nul

title Video MD5 Modifier - Build Script
color 0A
mode con cols=90 lines=30

echo ================================================
echo         MD5Modifier - Build Script
echo ================================================
echo.

:: Set MSYS2 paths
set "MSYS2_PATH=C:\msys64"
set "MINGW_PATH=%MSYS2_PATH%\ucrt64"
set "PATH=%MINGW_PATH%\bin;%MSYS2_PATH%\usr\bin;%PATH%"

:: Check compiler
where g++ >nul 2>&1
if errorlevel 1 (
    echo [ERROR] g++ not found!
    echo Please install MSYS2 to C:\msys64
    pause
    exit /b 1
)

where qmake >nul 2>&1
if errorlevel 1 (
    echo [ERROR] qmake not found!
    pause
    exit /b 1
)

echo [OK] Compiler found
echo.

:: ========================================
:: Clean all cache and build files
:: ========================================
echo [1/4] Cleaning cache and build files...

:: Remove Makefiles
if exist "Makefile" del /f /q Makefile 2>nul
if exist "Makefile.Release" del /f /q Makefile.Release 2>nul
if exist "Makefile.Debug" del /f /q Makefile.Debug 2>nul
if exist ".qmake.stash" del /f /q .qmake.stash 2>nul
if exist "MD5Modifier_resource.rc" del /f /q MD5Modifier_resource.rc 2>nul

:: Remove object files
del /f /q *.o 2>nul
del /f /q *.obj 2>nul

:: Remove directories
if exist "release" rmdir /s /q release 2>nul
if exist "debug" rmdir /s /q debug 2>nul
if exist "obj" rmdir /s /q obj 2>nul
if exist "moc" rmdir /s /q moc 2>nul

:: Remove old executable
if exist "MD5Modifier.exe" del /f /q MD5Modifier.exe 2>nul

echo [OK] Clean completed
echo.

:: ========================================
:: Check icon
:: ========================================
echo [2/4] Checking icon file...

if exist "assets\app.ico" (
    echo [Icon] Found: assets\app.ico
) else (
    echo [Icon] Not found, using default
)
echo.

:: ========================================
:: Build
:: ========================================
echo [3/4] Building...
echo ========================================

qmake MD5Modifier.pro -spec win32-g++ "CONFIG+=release"
if errorlevel 1 (
    echo ========================================
    echo [ERROR] qmake failed!
    pause
    exit /b 1
)

mingw32-make -j4
if errorlevel 1 (
    echo ========================================
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo ========================================
echo [OK] Build completed
echo.

:: ========================================
:: Copy exe and cleanup
:: ========================================
echo [4/4] Finalizing...

if exist "release\MD5Modifier.exe" (
    copy /y "release\MD5Modifier.exe" "MD5Modifier.exe" >nul
)

:: Clean build files
if exist "Makefile" del /f /q Makefile 2>nul
if exist "Makefile.Release" del /f /q Makefile.Release 2>nul
if exist "Makefile.Debug" del /f /q Makefile.Debug 2>nul
if exist ".qmake.stash" del /f /q .qmake.stash 2>nul
if exist "MD5Modifier_resource.rc" del /f /q MD5Modifier_resource.rc 2>nul
del /f /q *.o 2>nul
del /f /q *.obj 2>nul
if exist "release" rmdir /s /q release 2>nul
if exist "debug" rmdir /s /q debug 2>nul
if exist "obj" rmdir /s /q obj 2>nul
if exist "moc" rmdir /s /q moc 2>nul

echo [OK] Cleanup completed
echo.

:: ========================================
:: Result
:: ========================================
if exist "MD5Modifier.exe" (
    echo ================================================
    echo [SUCCESS] Build complete!
    echo Output: %CD%\MD5Modifier.exe
    for %%i in ("MD5Modifier.exe") do echo Size: %%~zi bytes
    echo ================================================
) else (
    echo ================================================
    echo [ERROR] EXE not found!
    echo ================================================
)

echo.
echo Press any key to exit...
pause >nul
