@echo off
chcp 65001 >nul
echo ========================================
echo   发布 Git Tools WPF
echo ========================================
echo.

cd GitToolsWPF

set OUTPUT_DIR=publish
set RUNTIME=win-x64

echo [1/3] 清理旧的发布文件...
if exist %OUTPUT_DIR% (
    rmdir /s /q %OUTPUT_DIR%
)

echo.
echo [2/3] 开始发布...
echo 目标平台: %RUNTIME%
echo 输出目录: %OUTPUT_DIR%
echo.

dotnet publish GitToolsWPF.csproj --configuration Release --runtime %RUNTIME% --self-contained true --output %OUTPUT_DIR% /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

if errorlevel 1 (
    echo.
    echo 发布失败！
    cd ..
    pause
    exit /b 1
)

echo.
echo [3/3] 清理不必要的文件...
cd %OUTPUT_DIR%
del *.pdb 2>nul
del *.xml 2>nul
cd ..

cd ..

echo.
echo ========================================
echo   发布成功！
echo ========================================
echo.
echo 发布文件位置: GitToolsWPF\%OUTPUT_DIR%\GitToolsWPF.exe
echo.
echo 提示：
echo - 包含所有依赖项，无需安装 .NET
echo - 可直接分发给其他用户使用
echo.
pause
