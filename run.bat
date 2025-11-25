@echo off
chcp 65001 >nul
echo ========================================
echo   运行 Git Tools WPF
echo ========================================
echo.

echo 正在启动应用程序...
dotnet run --project GitToolsWPF\GitToolsWPF.csproj --configuration Debug

if errorlevel 1 (
    echo.
    echo 运行失败！
    pause
    exit /b 1
)
