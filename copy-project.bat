@echo off
chcp 65001 >nul
echo ========================================
echo   复制 Git Tools WPF 项目
echo ========================================
echo.

set SOURCE_DIR=C:\chat\GitToolsWPF
set TARGET_DIR=%~dp0GitToolsWPF

echo 源文件夹: %SOURCE_DIR%
echo 目标文件夹: %TARGET_DIR%
echo.

:: 检查源文件夹是否存在
if not exist "%SOURCE_DIR%" (
    echo 错误：源文件夹 %SOURCE_DIR% 不存在！
    pause
    exit /b 1
)

:: 如果目标文件夹存在，询问是否覆盖
if exist "%TARGET_DIR%" (
    echo 警告：目标文件夹已存在！
    choice /C YN /M "是否删除并重新复制"
    if errorlevel 2 (
        echo 操作已取消。
        pause
        exit /b 0
    )
    echo 正在删除旧文件夹...
    rmdir /s /q "%TARGET_DIR%"
)

echo.
echo [1/3] 复制项目文件...
xcopy "%SOURCE_DIR%" "%TARGET_DIR%" /E /I /H /Y >nul
if errorlevel 1 (
    echo 复制失败！
    pause
    exit /b 1
)
echo ✓ 项目文件复制完成

echo.
echo [2/3] 清理编译缓存...
if exist "%TARGET_DIR%\bin" rmdir /s /q "%TARGET_DIR%\bin"
if exist "%TARGET_DIR%\obj" rmdir /s /q "%TARGET_DIR%\obj"
if exist "%TARGET_DIR%\publish" rmdir /s /q "%TARGET_DIR%\publish"
echo ✓ 编译缓存清理完成

echo.
echo [3/3] 复制批处理文件（从源文件夹）...

:: 获取目标文件夹的父目录
for %%I in ("%TARGET_DIR%\..") do set "PARENT_DIR=%%~fI"

:: 获取源文件夹的父目录
for %%I in ("%SOURCE_DIR%\..") do set "SOURCE_PARENT_DIR=%%~fI"

:: 复制 run.bat
if exist "%SOURCE_PARENT_DIR%\run.bat" (
    copy /Y "%SOURCE_PARENT_DIR%\run.bat" "%PARENT_DIR%\run.bat" >nul
    echo ✓ run.bat 复制完成
) else (
    echo ✗ 警告：源文件夹中未找到 run.bat
)

:: 复制 build.bat
if exist "%SOURCE_PARENT_DIR%\build.bat" (
    copy /Y "%SOURCE_PARENT_DIR%\build.bat" "%PARENT_DIR%\build.bat" >nul
    echo ✓ build.bat 复制完成
) else (
    echo ✗ 警告：源文件夹中未找到 build.bat
)

:: 复制 publish.bat
if exist "%SOURCE_PARENT_DIR%\publish.bat" (
    copy /Y "%SOURCE_PARENT_DIR%\publish.bat" "%PARENT_DIR%\publish.bat" >nul
    echo ✓ publish.bat 复制完成
) else (
    echo ✗ 警告：源文件夹中未找到 publish.bat
)

echo.
echo ========================================
echo   复制完成！
echo ========================================
echo.
echo 新项目位置: %TARGET_DIR%
echo.
echo 可用的批处理文件（在项目文件夹外）：
echo   - run.bat     : 运行应用程序
echo   - build.bat   : 编译项目
echo   - publish.bat : 发布为独立可执行文件
echo.
pause
