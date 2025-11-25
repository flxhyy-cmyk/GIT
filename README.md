# GitToolsWPF

一个现代化的 Git 图形界面工具，基于 WPF 开发，提供简洁易用的 Git 操作体验。

## 功能特性

### 核心功能
- **提交历史查看** - 图形化展示提交历史，支持分支筛选
- **代码推送** - 支持常规推送和强制推送
- **代码更新** - 从远程仓库拉取最新代码
- **版本发布** - 创建和管理 Git 标签版本
- **分支管理** - 查看和切换分支，处理游离 HEAD 状态
- **项目克隆** - 克隆远程仓库到本地

### 界面特性
- **多主题支持** - 浅色、深色、跟随系统主题
- **实时日志** - 显示 Git 操作的详细输出
- **状态监控** - 实时显示当前分支和仓库状态
- **悬浮通知** - 操作结果即时反馈

## 系统要求

- Windows 10/11
- .NET 6.0 或更高版本
- Git 已安装并配置在系统 PATH 中

## 快速开始

### 安装

1. 下载最新版本的 `GitToolsWPF.exe`
2. 双击运行即可使用

### 配置

首次使用需要配置以下信息：

1. **本地文件夹** - 选择 Git 仓库所在的本地文件夹
2. **GitHub 用户名** - 你的 GitHub 用户名
3. **GitHub Token** - 用于身份验证的个人访问令牌
4. **仓库地址** - 远程仓库的 URL（支持自动检测）

#### 如何获取 GitHub Token

1. 登录 GitHub
2. 进入 Settings → Developer settings → Personal access tokens → Tokens (classic)
3. 点击 "Generate new token"
4. 勾选 `repo` 权限
5. 生成并复制 Token

## 使用说明

### 查看提交历史
- 点击"本地提交"按钮
- 支持按分支筛选（全部分支/本地分支/远程分支）
- 双击提交记录可切换到该提交

### 推送代码
- **初始推送** - 首次推送到新仓库
- **更新推送** - 推送本地更改到已有仓库
- 支持自动检测游离 HEAD 状态并创建分支

### 版本管理
- 查看所有版本标签
- 创建新版本（支持自动递增版本号）
- 删除指定版本

### 主题切换
- 在设置页面选择主题
- 支持浅色、深色、跟随系统

## 项目结构

```
GitToolsWPF/
├── Models/              # 数据模型
│   ├── AppSettings.cs   # 应用设置
│   └── GitOperation.cs  # Git 操作模型
├── Services/            # 服务层
│   ├── GitService.cs    # Git 命令执行
│   └── SettingsService.cs # 设置管理
├── ViewModels/          # 视图模型
│   ├── MainViewModel.cs # 主窗口视图模型
│   ├── CommitHistoryViewModel.cs # 提交历史视图模型
│   └── RelayCommand.cs  # 命令实现
├── Views/               # 视图
│   ├── CommitHistoryDialog.xaml # 提交历史对话框
│   ├── CreateBranchDialog.xaml  # 创建分支对话框
│   └── VerificationDialog.xaml  # 验证对话框
├── Themes/              # 主题资源
│   ├── DarkTheme.xaml   # 深色主题
│   ├── LightTheme.xaml  # 浅色主题
│   └── SystemTheme.xaml # 系统主题
├── MainWindow.xaml      # 主窗口
└── App.xaml             # 应用程序入口
```

## 构建项目

### 开发环境
- Visual Studio 2022 或更高版本
- .NET 6.0 SDK

### 构建步骤

```bash
# 克隆仓库
git clone <repository-url>

# 进入项目目录
cd GitToolsWPF

# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行项目
dotnet run
```

### 发布

使用提供的批处理文件：

```bash
# 构建项目
build.bat

# 发布单文件可执行程序
publish.bat

# 运行程序
run.bat
```

## 技术栈

- **框架**: .NET 6.0 / WPF
- **架构**: MVVM
- **版本控制**: Git
- **UI**: XAML

## 注意事项

- 确保 Git 已正确安装并配置
- GitHub Token 需要有 `repo` 权限
- 强制推送会覆盖远程仓库，请谨慎使用
- 建议定期备份重要数据

## 许可证

本项目采用 MIT 许可证。

## 贡献

欢迎提交 Issue 和 Pull Request！

## 联系方式

如有问题或建议，请通过 GitHub Issues 联系。
