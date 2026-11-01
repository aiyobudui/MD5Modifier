@echo off
setlocal
chcp 65001 >nul
title MD5Modifier - .NET 8 Build (Single File)
color 0A

echo ============================================
echo        MD5Modifier - .NET 8 Build
echo ============================================
echo.

:: 检查 dotnet SDK
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] 未找到 dotnet SDK！请安装 .NET 8 SDK。
    pause
    exit /b 1
)
echo [OK] dotnet found:
for /f "tokens=*" %%i in ('dotnet --version') do echo   %%i
echo.

:: ========================================
:: 1/4 构建前清理：删除所有临时打包文件
:: ========================================
echo [1/4] 清理临时打包文件（bin / obj / publish）...
if exist "bin" rmdir /s /q "bin" 2>nul
if exist "obj" rmdir /s /q "obj" 2>nul
if exist "publish" rmdir /s /q "publish" 2>nul
echo [OK] 清理完成
echo.

:: ========================================
:: 2/4 发布单文件（框架依赖，需目标机安装 .NET 8 运行时）
:: ========================================
echo [2/4] 发布单文件（框架依赖，需目标机安装 .NET 8 运行时）...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o publish
if errorlevel 1 (
    echo ============================================
    echo [ERROR] 发布失败！
    pause
    exit /b 1
)
echo [OK] 发布完成
echo.

:: ========================================
:: 3/4 构建后清理：删除中间临时文件（bin / obj），仅保留 publish
:: ========================================
echo [3/4] 清理构建中间文件（bin / obj）...
if exist "bin" rmdir /s /q "bin" 2>nul
if exist "obj" rmdir /s /q "obj" 2>nul
echo [OK] 清理完成
echo.

:: ========================================
:: 4/4 收尾：确认产物
:: ========================================
echo [4/4] 输出文件：
if exist "publish\MD5Modifier.exe" (
    for %%i in ("publish\MD5Modifier.exe") do echo   publish\MD5Modifier.exe  大小: %%~zi bytes
) else (
    echo [ERROR] 未找到发布产物！
    pause
    exit /b 1
)
echo.
echo ============================================
echo [完成] 构建成功！
echo 说明：框架依赖单文件需目标机安装 .NET 8 运行时。
echo 如需完全独立（无需安装运行时），改用自包含发布：
echo   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
echo ============================================
echo.
pause
