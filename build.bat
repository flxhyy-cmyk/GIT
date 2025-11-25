@echo off
chcp 65001 >nul
echo ========================================
echo   编译 Git Tools WPF
echo ========================================
echo.

echo [1/2] 清理旧的编译文件...
dotnet clean GitToolsWPF\GitToolsWPF.csproj --configuration Release
if errorlevel 1 (
    echo 清理失败！
    pause
    exit /b 1
)

echo.
echo [2/2] 开始编译...
dotnet build GitToolsWPF\GitToolsWPF.csproj --configuration Release
if errorlevel 1 (
    echo 编译失败！
    pause
    exit /b 1
)

echo.
echo ========================================
echo   编译成功！
echo ========================================
echo.
echo 输出目录: GitToolsWPF\bin\Release\net9.0-windows\
echo.
pause
