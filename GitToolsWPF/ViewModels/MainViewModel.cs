using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GitToolsWPF.Models;
using GitToolsWPF.Services;
using Microsoft.Win32;

namespace GitToolsWPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly GitService _gitService;
        private readonly SettingsService _settingsService;
        private AppSettings _settings;
        private string _selectedPage = "History";
        private string _commitMessage = "Update: 更新代码";
        private string _versionNumber = "";
        private string _versionNote = "";
        private bool _isExecuting = false;
        private string _currentVersion = "无版本";
        private VersionInfo? _selectedVersion;
        private string _currentBranchName = "";
        private bool _isDetachedHead = false;
        private string _selectedBranchFilter = "全部分支";
        private System.Threading.Timer? _themeMonitorTimer;
        private string _windowTitle = "Git Tools WPF";

        public ObservableCollection<string> LogMessages { get; } = new();
        public ObservableCollection<VersionInfo> VersionHistory { get; } = new();
        public ObservableCollection<CommitInfo> CommitHistory { get; } = new();
        public ObservableCollection<BranchInfo> BranchList { get; } = new();
        public ObservableCollection<string> BranchFilterOptions { get; } = new() { "全部分支", "本地分支", "远程分支" };
        
        // 悬浮通知事件
        public event Action<string, string>? ShowNotificationRequested;
        
        // 主题变化事件
        public event Action? ThemeChanged;

        public AppSettings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        public string SelectedPage
        {
            get => _selectedPage;
            set { _selectedPage = value; OnPropertyChanged(); }
        }

        public string CommitMessage
        {
            get => _commitMessage;
            set { _commitMessage = value; OnPropertyChanged(); }
        }

        public string VersionNumber
        {
            get => _versionNumber;
            set { _versionNumber = value; OnPropertyChanged(); }
        }

        public string VersionNote
        {
            get => _versionNote;
            set { _versionNote = value; OnPropertyChanged(); }
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set { _isExecuting = value; OnPropertyChanged(); }
        }

        public string CurrentVersion
        {
            get => _currentVersion;
            set { _currentVersion = value; OnPropertyChanged(); }
        }

        public VersionInfo? SelectedVersion
        {
            get => _selectedVersion;
            set { _selectedVersion = value; OnPropertyChanged(); }
        }

        public string CurrentBranchName
        {
            get => _currentBranchName;
            set { _currentBranchName = value; OnPropertyChanged(); }
        }

        public bool IsDetachedHead
        {
            get => _isDetachedHead;
            set { _isDetachedHead = value; OnPropertyChanged(); }
        }

        public string SelectedBranchFilter
        {
            get => _selectedBranchFilter;
            set 
            { 
                _selectedBranchFilter = value; 
                OnPropertyChanged();
                _ = ExecuteAsync(ViewCommitHistoryAsync);
            }
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set { _windowTitle = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand ViewHistoryCommand { get; }
        public ICommand ViewLocalChangesCommand { get; }
        public ICommand ViewLocalCommitsCommand { get; }
        public ICommand ViewLocalBranchesCommand { get; }
        public ICommand PushCommand { get; }
        public ICommand ForcePushCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand ReleaseCommand { get; }
        public ICommand ViewVersionCommand { get; }
        public ICommand ViewCommitHistoryCommand { get; }
        public ICommand ViewSyncStatusCommand { get; }
        public ICommand ViewBranchesCommand { get; }
        public ICommand ViewTagsCommand { get; }
        public ICommand CleanGitHubCommand { get; }
        public ICommand LoadVersionHistoryCommand { get; }
        public ICommand AutoIncrementVersionCommand { get; }
        public ICommand DeleteVersionCommand { get; }
        public ICommand CloneProjectCommand { get; }
        public ICommand InitHistoryCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand ChangeThemeCommand { get; }
        public ICommand SwitchToCommitCommand { get; }
        public ICommand CheckCurrentStatusCommand { get; }

        public MainViewModel()
        {
            _gitService = new GitService();
            _settingsService = new SettingsService();
            _settings = _settingsService.LoadSettings();

            _gitService.OnOutput += AddLog;

            // 初始化命令
            ViewHistoryCommand = new RelayCommand(async () => await ExecuteAsync(ViewHistoryAsync));
            ViewLocalChangesCommand = new RelayCommand(async () => await ExecuteAsync(ViewLocalChangesAsync));
            ViewLocalCommitsCommand = new RelayCommand(async () => await ExecuteAsync(ViewLocalCommitsAsync));
            ViewLocalBranchesCommand = new RelayCommand(async () => await ExecuteAsync(ViewLocalBranchesAsync));
            PushCommand = new RelayCommand(async () => await ExecuteAsync(PushAsync));
            ForcePushCommand = new RelayCommand(async () => await ExecuteAsync(ForcePushAsync));
            UpdateCommand = new RelayCommand(async () => await ExecuteAsync(UpdateAsync));
            ReleaseCommand = new RelayCommand(async () => await ExecuteAsync(ReleaseAsync));
            ViewVersionCommand = new RelayCommand(async () => await ExecuteAsync(ViewVersionAsync));
            ViewCommitHistoryCommand = new RelayCommand(async () => await ExecuteAsync(ViewCommitHistoryAsync));
            ViewSyncStatusCommand = new RelayCommand(async () => await ExecuteAsync(ViewSyncStatusAsync));
            ViewBranchesCommand = new RelayCommand(async () => await ExecuteAsync(ViewBranchesAsync));
            ViewTagsCommand = new RelayCommand(async () => await ExecuteAsync(ViewTagsAsync));
            CleanGitHubCommand = new RelayCommand(async () => await ExecuteAsync(CleanGitHubAsync));
            LoadVersionHistoryCommand = new RelayCommand(async () => await ExecuteAsync(LoadVersionHistoryAsync));
            AutoIncrementVersionCommand = new RelayCommand(AutoIncrementVersion);
            DeleteVersionCommand = new RelayCommand(async () => await ExecuteAsync(DeleteVersionAsync));
            CloneProjectCommand = new RelayCommand(async () => await ExecuteAsync(CloneProjectAsync));
            InitHistoryCommand = new RelayCommand(async () => await ExecuteAsync(InitHistoryAsync));
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ChangeThemeCommand = new RelayCommand<string>(ChangeTheme);
            SwitchToCommitCommand = new RelayCommand<CommitInfo>(async (commit) => await ExecuteAsync(async () => await SwitchToCommitAsync(commit)));
            CheckCurrentStatusCommand = new RelayCommand(async () => await CheckCurrentStatusAsync());

            ApplyTheme();
            
            // 初始化窗口标题
            UpdateWindowTitle();
            
            // 初始化时检查当前状态
            _ = CheckCurrentStatusAsync();
            
            // 启动系统主题监听
            StartSystemThemeMonitoring();
        }

        private async Task ExecuteAsync(Func<Task> action)
        {
            if (IsExecuting) return;
            
            IsExecuting = true;
            try
            {
                await action();
            }
            finally
            {
                IsExecuting = false;
            }
        }

        private void AddLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            });
        }

        private void InitializeGitService()
        {
            _gitService.Initialize(Settings.LocalFolder, Settings.GitHubToken, Settings.RepoUrl);
        }

        private async Task CheckCurrentStatusAsync()
        {
            try
            {
                // 检查本地文件夹是否配置
                if (string.IsNullOrWhiteSpace(Settings.LocalFolder))
                {
                    CurrentBranchName = "未配置";
                    IsDetachedHead = false;
                    return;
                }

                // 检查是否是 Git 仓库
                var gitPath = System.IO.Path.Combine(Settings.LocalFolder, ".git");
                if (!System.IO.Directory.Exists(gitPath))
                {
                    CurrentBranchName = "非 Git 仓库";
                    IsDetachedHead = false;
                    return;
                }

                InitializeGitService();

                // 检查游离 HEAD 状态
                var (isDetached, currentHash, commitMessage) = await _gitService.CheckDetachedHeadAsync();

                if (isDetached)
                {
                    CurrentBranchName = $"游离 HEAD ({currentHash})";
                    IsDetachedHead = true;
                }
                else
                {
                    // 获取当前分支名
                    var (_, branchOutput) = await _gitService.ExecuteGitCommandAsync("branch --show-current");
                    var branchName = branchOutput?.Trim() ?? "未知";
                    CurrentBranchName = string.IsNullOrEmpty(branchName) ? "未知分支" : branchName;
                    IsDetachedHead = false;
                }
            }
            catch
            {
                CurrentBranchName = "检测失败";
                IsDetachedHead = false;
            }
        }

        private async Task ViewHistoryAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  📊 仓库状态（本地）- 全部信息");
            AddLog("========================================");

            InitializeGitService();

            AddLog("\n========== 本地仓库状态 ==========");
            await _gitService.ExecuteGitCommandAsync("status");

            AddLog("\n========== 未提交的更改 ==========");
            await _gitService.ExecuteGitCommandAsync("diff --stat");

            AddLog("\n========== 最近 10 次提交 ==========");
            await _gitService.ExecuteGitCommandAsync("log --oneline -10");

            AddLog("\n========== 本地分支 ==========");
            await _gitService.ExecuteGitCommandAsync("branch -a");

            AddLog("\n✓ 完成！");
        }

        private async Task ViewLocalChangesAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  📝 本地未提交更改");
            AddLog("========================================");

            InitializeGitService();

            AddLog("\n========== 本地仓库状态 ==========");
            await _gitService.ExecuteGitCommandAsync("status");

            AddLog("\n========== 未提交的更改详情 ==========");
            await _gitService.ExecuteGitCommandAsync("diff --stat");

            AddLog("\n✓ 完成！");
        }

        private Task ViewLocalCommitsAsync()
        {
            // 打开图形化提交历史对话框
            InitializeGitService();
            
            var viewModel = new CommitHistoryViewModel(_gitService, Settings.LocalFolder, Settings.GitHubToken, Settings.RepoUrl);
            
            // 订阅日志事件
            viewModel.OnLog += AddLog;
            
            // 订阅提交切换事件，用于刷新主窗口状态
            viewModel.OnCommitSwitched += () =>
            {
                // 在 UI 线程上刷新主窗口的当前状态显示
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await CheckCurrentStatusAsync();
                });
            };
            
            var dialog = new Views.CommitHistoryDialog(viewModel);
            dialog.ShowDialog();
            
            return Task.CompletedTask;
        }

        private async Task ViewLocalBranchesAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🌿 本地分支信息");
            AddLog("========================================");

            InitializeGitService();

            AddLog("\n========== 本地分支列表 ==========");
            await _gitService.ExecuteGitCommandAsync("branch -a");

            AddLog("\n========== 当前分支详情 ==========");
            await _gitService.ExecuteGitCommandAsync("branch -vv");

            AddLog("\n✓ 完成！");
        }

        private async Task PushAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  ⬆️ 常规推送到 GitHub");
            AddLog("========================================");

            InitializeGitService();

            // 检查是否已有 Git 仓库和远程配置
            if (await CheckIfAlreadyInitialized())
            {
                return;
            }

            AddLog("\n[1/6] 初始化 Git 仓库...");
            await _gitService.ExecuteGitCommandAsync("init");

            AddLog("\n[2/6] 配置用户信息...");
            await _gitService.ExecuteGitCommandAsync($"config user.name \"{Settings.GitHubUser}\"");
            await _gitService.ExecuteGitCommandAsync($"config user.email \"{Settings.GitHubUser}@users.noreply.github.com\"");

            AddLog("\n[3/6] 添加所有文件...");
            var (success1, _) = await _gitService.ExecuteGitCommandAsync("add .");
            if (!success1)
            {
                AddLog("✗ 添加文件失败");
                return;
            }

            AddLog("\n[4/6] 提交更改...");
            await _gitService.ExecuteGitCommandAsync("commit -m \"Initial commit\"");

            AddLog("\n[5/6] 设置远程仓库...");
            // 先尝试删除（忽略错误），再添加
            await _gitService.ExecuteGitCommandAsync("remote remove origin");
            var (addRemoteSuccess, addRemoteOutput) = await _gitService.ExecuteGitCommandAsync($"remote add origin {_gitService.GetRemoteUrl()}");
            
            if (!addRemoteSuccess)
            {
                // 如果添加失败，可能是已存在，尝试更新
                AddLog("⚠️ 远程仓库已存在，尝试更新地址...");
                await _gitService.ExecuteGitCommandAsync($"remote set-url origin {_gitService.GetRemoteUrl()}");
            }

            AddLog("\n[6/7] 推送到 GitHub（常规模式）...");
            await _gitService.ExecuteGitCommandAsync("branch -M main");
            var (success2, pushOutput) = await _gitService.ExecuteGitCommandAsync("push -u origin main");

            if (success2)
            {
                AddLog("\n[7/7] 设置默认分支...");
                // 推送 HEAD 引用，确保 GitHub 识别默认分支
                await _gitService.ExecuteGitCommandAsync("symbolic-ref HEAD refs/heads/main");
                await _gitService.ExecuteGitCommandAsync("push origin HEAD");
                
                AddLog("\n========================================");
                AddLog("  ✓ 推送成功！");
                AddLog($"  仓库地址: {Settings.RepoUrl}");
                AddLog("========================================");
                
                // 使用悬浮通知代替弹窗
                ShowNotificationRequested?.Invoke("✓ 推送成功！", "#28A745");
            }
            else
            {
                AddLog("\n✗ 推送失败");
                AnalyzeInitialPushError(pushOutput ?? "");
            }
        }

        private async Task ForcePushAsync()
        {
            var result = MessageBox.Show(
                "⚠️ 警告：强制推送将覆盖远程仓库的所有内容！\n\n" +
                "此操作会：\n" +
                "• 覆盖远程仓库的所有文件\n" +
                "• 覆盖远程仓库的所有历史记录\n\n" +
                "确定要继续吗？",
                "⚠️ 确认强制推送",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  ⬆️ 强制推送到 GitHub");
            AddLog("========================================");

            InitializeGitService();

            // 检查是否已有 Git 仓库和远程配置
            if (await CheckIfAlreadyInitialized())
            {
                return;
            }

            AddLog("\n[1/6] 初始化 Git 仓库...");
            await _gitService.ExecuteGitCommandAsync("init");

            AddLog("\n[2/6] 配置用户信息...");
            await _gitService.ExecuteGitCommandAsync($"config user.name \"{Settings.GitHubUser}\"");
            await _gitService.ExecuteGitCommandAsync($"config user.email \"{Settings.GitHubUser}@users.noreply.github.com\"");

            AddLog("\n[3/6] 添加所有文件...");
            var (success1, _) = await _gitService.ExecuteGitCommandAsync("add .");
            if (!success1)
            {
                AddLog("✗ 添加文件失败");
                return;
            }

            AddLog("\n[4/6] 提交更改...");
            await _gitService.ExecuteGitCommandAsync("commit -m \"Initial commit\"");

            AddLog("\n[5/6] 设置远程仓库...");
            // 先尝试删除（忽略错误），再添加
            await _gitService.ExecuteGitCommandAsync("remote remove origin");
            var (addRemoteSuccess, addRemoteOutput) = await _gitService.ExecuteGitCommandAsync($"remote add origin {_gitService.GetRemoteUrl()}");
            
            if (!addRemoteSuccess)
            {
                // 如果添加失败，可能是已存在，尝试更新
                AddLog("⚠️ 远程仓库已存在，尝试更新地址...");
                await _gitService.ExecuteGitCommandAsync($"remote set-url origin {_gitService.GetRemoteUrl()}");
            }

            AddLog("\n[6/7] 强制推送到 GitHub...");
            await _gitService.ExecuteGitCommandAsync("branch -M main");
            var (success2, pushOutput) = await _gitService.ExecuteGitCommandAsync("push -u origin main --force");

            if (success2)
            {
                AddLog("\n[7/7] 设置默认分支...");
                // 推送 HEAD 引用，确保 GitHub 识别默认分支
                await _gitService.ExecuteGitCommandAsync("symbolic-ref HEAD refs/heads/main");
                await _gitService.ExecuteGitCommandAsync("push origin HEAD --force");
                
                AddLog("\n========================================");
                AddLog("  ✓ 强制推送成功！");
                AddLog($"  仓库地址: {Settings.RepoUrl}");
                AddLog("  提示: 如果 GitHub 页面显示异常，请刷新页面");
                AddLog("========================================");
                
                // 使用悬浮通知代替弹窗
                ShowNotificationRequested?.Invoke("✓ 强制推送成功！", "#28A745");
            }
            else
            {
                AddLog("\n✗ 推送失败");
                AnalyzeInitialPushError(pushOutput ?? "");
            }
        }

        private async Task UpdateAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🔄 更新推送");
            AddLog("========================================");

            InitializeGitService();

            // [0/6] 检查游离 HEAD 状态
            AddLog("\n[0/6] 检查当前状态...");
            var (isDetached, currentHash, commitMessage) = await _gitService.CheckDetachedHeadAsync();
            
            if (isDetached)
            {
                AddLog("⚠️ 检测到游离 HEAD 状态");
                AddLog($"   当前位置: {currentHash}");
                AddLog($"   提交信息: {commitMessage}");
                
                // 生成建议的分支名
                var suggestedBranchName = _gitService.GenerateSuggestedBranchName();
                
                // 显示创建分支对话框
                var dialog = new Views.CreateBranchDialog(
                    $"游离 HEAD",
                    currentHash,
                    commitMessage,
                    suggestedBranchName);
                
                var result = dialog.ShowDialog();
                
                if (result == true && dialog.IsConfirmed)
                {
                    var branchName = dialog.BranchName;
                    
                    // 检查分支是否已存在
                    if (await _gitService.BranchExistsAsync(branchName))
                    {
                        AddLog($"⚠️ 分支 {branchName} 已存在");
                        
                        var overwriteResult = MessageBox.Show(
                            $"分支 {branchName} 已存在。\n\n" +
                            "是否使用新名称？\n\n" +
                            "• 点击「是」- 添加时间戳后缀\n" +
                            "• 点击「否」- 取消操作",
                            "分支已存在",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        
                        if (overwriteResult == MessageBoxResult.Yes)
                        {
                            branchName = $"{branchName}-{DateTime.Now:HHmmss}";
                            AddLog($"使用新名称: {branchName}");
                        }
                        else
                        {
                            AddLog("✗ 操作已取消");
                            return;
                        }
                    }
                    
                    // 创建并切换到新分支
                    var createSuccess = await _gitService.CreateAndCheckoutBranchAsync(branchName);
                    
                    if (!createSuccess)
                    {
                        AddLog("✗ 创建分支失败，无法继续推送");
                        MessageBox.Show(
                            "创建分支失败！\n\n" +
                            "请查看日志了解详细错误信息。",
                            "错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                    
                    AddLog($"✓ 已创建并切换到分支: {branchName}");
                    AddLog("继续执行推送操作...");
                }
                else
                {
                    AddLog("✗ 用户取消操作");
                    return;
                }
            }
            else
            {
                AddLog("✓ 当前在正常分支上");
            }

            // [1/6] 检查同步状态
            AddLog("\n[1/6] 检查同步状态...");
            
            // 先检查是否配置了远程仓库
            var (hasOrigin, originUrl) = await _gitService.ExecuteGitCommandAsync("remote get-url origin");
            
            if (!hasOrigin)
            {
                AddLog("✗ 未配置远程仓库");
                MessageBox.Show(
                    "❌ 未配置远程仓库\n\n" +
                    "检测到：\n" +
                    "• 本地仓库存在\n" +
                    "• 但未配置远程仓库（origin）\n\n" +
                    "这不是「更新推送」的使用场景。\n\n" +
                    "建议操作：\n" +
                    "1. 使用「初始推送」配置远程仓库并推送\n" +
                    "2. 或手动配置：\n" +
                    "   git remote add origin <仓库地址>",
                    "未配置远程仓库",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            var configuredOrigin = originUrl?.Trim();
            AddLog($"✓ 已配置远程仓库: {configuredOrigin}");
            
            // 检查配置的远程仓库地址是否与设置中的一致
            var expectedOrigin = _gitService.GetRemoteUrl();
            if (configuredOrigin != expectedOrigin && !string.IsNullOrEmpty(expectedOrigin))
            {
                AddLog($"⚠️ 远程仓库地址不匹配");
                AddLog($"   当前配置: {configuredOrigin}");
                AddLog($"   设置中的: {expectedOrigin}");
                
                var result = MessageBox.Show(
                    "⚠️ 远程仓库地址不匹配\n\n" +
                    $"本地配置的远程仓库：\n{configuredOrigin}\n\n" +
                    $"设置中配置的仓库：\n{expectedOrigin}\n\n" +
                    "可能的原因：\n" +
                    "• 这是不同的项目\n" +
                    "• 复制了项目但未更新远程地址\n" +
                    "• 设置中的仓库地址配置错误\n\n" +
                    "是否使用设置中的地址更新本地配置？\n\n" +
                    "• 点击「是」- 更新为设置中的地址\n" +
                    "• 点击「否」- 取消操作，手动检查",
                    "远程仓库地址不匹配",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    AddLog("\n正在更新远程仓库地址...");
                    await _gitService.ExecuteGitCommandAsync($"remote set-url origin {expectedOrigin}");
                    AddLog($"✓ 已更新为: {expectedOrigin}");
                }
                else
                {
                    AddLog("\n✗ 操作已取消");
                    return;
                }
            }
            
            // 获取远程最新信息
            var (fetchSuccess, fetchOutput) = await _gitService.ExecuteGitCommandAsync("fetch origin");
            
            if (!fetchSuccess)
            {
                AddLog("✗ 无法连接到远程仓库");
                
                // 网络错误
                if (fetchOutput?.Contains("Could not resolve host") == true ||
                    fetchOutput?.Contains("Failed to connect") == true ||
                    fetchOutput?.Contains("Connection timed out") == true ||
                    fetchOutput?.Contains("Connection was reset") == true)
                {
                    MessageBox.Show(
                        "❌ 网络连接失败\n\n" +
                        "无法连接到 GitHub 服务器。\n\n" +
                        "建议操作：\n" +
                        "1. 检查网络连接是否正常\n" +
                        "2. 检查代理设置\n" +
                        "3. 稍后重试",
                        "网络错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                // 权限错误
                else if (fetchOutput?.Contains("Authentication failed") == true ||
                         fetchOutput?.Contains("Permission denied") == true ||
                         fetchOutput?.Contains("403") == true)
                {
                    MessageBox.Show(
                        "❌ 权限验证失败\n\n" +
                        "无法访问远程仓库。\n\n" +
                        "可能的原因：\n" +
                        "• GitHub Token 无效或过期\n" +
                        "• Token 权限不足\n" +
                        "• 仓库是私有的但没有权限\n\n" +
                        "建议操作：\n" +
                        "1. 前往「设置」页面检查 Token\n" +
                        "2. 确保 Token 有 repo 权限\n" +
                        "3. 重新生成 Token（如果过期）",
                        "权限错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                // 仓库不存在
                else if (fetchOutput?.Contains("not found") == true ||
                         fetchOutput?.Contains("404") == true ||
                         fetchOutput?.Contains("does not appear to be a git repository") == true)
                {
                    MessageBox.Show(
                        "❌ 仓库不存在或地址错误\n\n" +
                        $"远程仓库地址：\n{configuredOrigin}\n\n" +
                        "可能的原因：\n" +
                        "• 仓库地址配置错误\n" +
                        "• 仓库已被删除\n" +
                        "• 这是不同项目的仓库地址\n" +
                        "• 仓库名或用户名拼写错误\n\n" +
                        "建议操作：\n" +
                        "1. 前往「设置」页面检查仓库地址\n" +
                        "2. 确认仓库在 GitHub 上存在\n" +
                        "3. 检查地址格式：\n" +
                        "   https://github.com/用户名/仓库名.git\n" +
                        "4. 如果是不同项目，使用「初始推送」",
                        "仓库不存在",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                // 其他错误
                else
                {
                    MessageBox.Show(
                        "❌ 无法连接到远程仓库\n\n" +
                        $"远程仓库地址：\n{configuredOrigin}\n\n" +
                        $"错误信息：\n{fetchOutput}\n\n" +
                        "建议操作：\n" +
                        "1. 检查网络连接\n" +
                        "2. 检查仓库地址是否正确\n" +
                        "3. 检查权限设置\n" +
                        "4. 查看日志了解详细错误",
                        "连接失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                return;
            }
            
            // 获取当前分支名
            var (_, branchNameOutput) = await _gitService.ExecuteGitCommandAsync("branch --show-current");
            var currentBranchName = branchNameOutput?.Trim() ?? "main";
            
            if (string.IsNullOrEmpty(currentBranchName))
            {
                AddLog("⚠️ 当前处于游离HEAD状态");
                MessageBox.Show(
                    "⚠️ 游离HEAD状态\n\n" +
                    "当前不在任何分支上，无法进行更新推送。\n\n" +
                    "建议操作：\n" +
                    "1. 切换到一个分支（如 main）\n" +
                    "2. 或创建新分支保存当前更改",
                    "游离HEAD状态",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            AddLog($"当前分支：{currentBranchName}");
            
            // 检查远程是否领先（使用当前分支）
            var (checkSuccess, remoteAheadOutput) = await _gitService.ExecuteGitCommandAsync($"rev-list HEAD..origin/{currentBranchName} --count");
            var remoteAheadCount = 0;
            
            if (checkSuccess && int.TryParse(remoteAheadOutput?.Trim(), out var count))
            {
                remoteAheadCount = count;
            }

            if (remoteAheadCount > 0)
            {
                AddLog($"⚠️ GitHub 领先本地 {remoteAheadCount} 个提交");
                
                // 检查本地是否有未提交的更改
                var (_, statusOutput) = await _gitService.ExecuteGitCommandAsync("status --short");
                var hasLocalChanges = !string.IsNullOrWhiteSpace(statusOutput);
                
                if (hasLocalChanges)
                {
                    AddLog("⚠️ 检测到本地有未提交的更改");
                    
                    // 有本地更改，显示三选项对话框
                    var choice = ShowLocalChangesDialog(remoteAheadCount);
                    
                    if (choice == LocalChangesChoice.KeepLocal)
                    {
                        // 保留本地更改
                        AddLog("\n用户选择：保留本地更改");
                        AddLog("正在提交本地更改...");
                        
                        await _gitService.ExecuteGitCommandAsync("add .");
                        var (commitSuccess, _) = await _gitService.ExecuteGitCommandAsync("commit -m \"保存本地更改\"");
                        
                        if (!commitSuccess)
                        {
                            AddLog("✗ 提交本地更改失败");
                            MessageBox.Show(
                                "提交本地更改失败！\n\n" +
                                "请检查是否有需要提交的更改。",
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return;
                        }
                        
                        AddLog("✓ 本地更改已提交");
                        AddLog("\n正在拉取并合并远程更改...");
                        
                        var (pullSuccess, pullOutput) = await _gitService.ExecuteGitCommandAsync("pull origin main --no-edit");
                        
                        if (!pullSuccess)
                        {
                            HandlePullError(pullOutput ?? "");
                            return;
                        }
                        
                        AddLog("✓ 远程更改已合并");
                    }
                    else if (choice == LocalChangesChoice.DiscardLocal)
                    {
                        // 放弃本地更改
                        AddLog("\n用户选择：放弃本地更改");
                        
                        // 二次确认
                        var confirmResult = MessageBox.Show(
                            "⚠️ 确认放弃本地更改？\n\n" +
                            "此操作将永久删除：\n" +
                            "• 所有未提交的更改\n" +
                            "• 所有未跟踪的文件\n\n" +
                            "⚠️ 此操作不可恢复！\n\n" +
                            "确定要继续吗？",
                            "⚠️ 确认危险操作",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        
                        if (confirmResult != MessageBoxResult.Yes)
                        {
                            AddLog("✗ 用户取消操作");
                            return;
                        }
                        
                        AddLog("正在放弃本地更改...");
                        await _gitService.ExecuteGitCommandAsync("reset --hard HEAD");
                        await _gitService.ExecuteGitCommandAsync("clean -fd");
                        AddLog("✓ 本地更改已放弃");
                        
                        AddLog("\n正在拉取远程更改...");
                        var (pullSuccess, pullOutput) = await _gitService.ExecuteGitCommandAsync("pull origin main");
                        
                        if (!pullSuccess)
                        {
                            HandlePullError(pullOutput ?? "");
                            return;
                        }
                        
                        AddLog("✓ 远程更改已拉取");
                    }
                    else
                    {
                        // 取消操作
                        AddLog("\n✗ 操作已取消");
                        return;
                    }
                }
                else
                {
                    // 无本地更改，直接询问是否拉取
                    AddLog("✓ 本地无未提交的更改");
                    
                    var result = MessageBox.Show(
                        $"⚠️ GitHub 领先本地\n\n" +
                        $"GitHub 上有 {remoteAheadCount} 个新提交。\n" +
                        "为了避免冲突，建议先拉取远程更改。\n\n" +
                        "是否自动拉取并合并？\n\n" +
                        "• 点击「是」- 自动拉取并继续推送（推荐）\n" +
                        "• 点击「否」- 取消操作，稍后手动处理",
                        "需要同步",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        AddLog("\n正在拉取远程更改...");
                        var (pullSuccess, pullOutput) = await _gitService.ExecuteGitCommandAsync("pull origin main --no-edit");
                        
                        if (!pullSuccess)
                        {
                            HandlePullError(pullOutput ?? "");
                            return;
                        }
                        
                        AddLog("✓ 远程更改已合并");
                    }
                    else
                    {
                        AddLog("\n✗ 操作已取消");
                        return;
                    }
                }
            }
            else
            {
                AddLog("✓ 本地和远程同步");
            }

            // [2/6] 添加所有更改
            AddLog("\n[2/6] 添加所有更改...");
            await _gitService.ExecuteGitCommandAsync("add .");

            // [3/6] 提交更改
            AddLog("\n[3/6] 提交更改...");
            var (commitSuccess2, commitOutput) = await _gitService.ExecuteGitCommandAsync($"commit -m \"{CommitMessage}\"");
            if (!commitSuccess2)
            {
                if (commitOutput?.Contains("nothing to commit") == true || 
                    commitOutput?.Contains("no changes added") == true)
                {
                    AddLog("ℹ 没有需要提交的更改");
                    MessageBox.Show(
                        "没有需要提交的更改\n\n" +
                        "工作区是干净的，无需推送。",
                        "提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    AddLog("✗ 提交失败");
                }
                return;
            }

            // [4/6] 更新远程仓库地址
            AddLog("\n[4/6] 更新远程仓库地址...");
            await _gitService.ExecuteGitCommandAsync($"remote set-url origin {_gitService.GetRemoteUrl()}");

            // [5/6] 推送到 GitHub
            AddLog("\n[5/6] 推送到 GitHub...");
            
            // 获取当前分支名
            var (_, currentBranchOutput) = await _gitService.ExecuteGitCommandAsync("branch --show-current");
            var currentBranch = currentBranchOutput?.Trim();
            
            if (string.IsNullOrEmpty(currentBranch))
            {
                AddLog("✗ 无法获取当前分支名（可能处于游离HEAD状态）");
                MessageBox.Show(
                    "无法推送！\n\n" +
                    "当前处于游离HEAD状态，无法推送。\n" +
                    "请先切换到一个分支。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            
            AddLog($"当前分支：{currentBranch}");
            var (pushSuccess, pushOutput) = await _gitService.ExecuteGitCommandAsync($"push origin {currentBranch}");

            if (pushSuccess)
            {
                AddLog("\n[6/6] 验证推送结果...");
                AddLog("\n========================================");
                AddLog("  ✓ 更新成功！");
                AddLog("========================================");
                
                // 清除提交历史缓存（因为有新的提交）
                CommitHistoryViewModel.ClearCache();
                
                // 使用悬浮通知显示成功消息
                ShowNotificationRequested?.Invoke("✓ 更新推送成功！您的更改已成功推送到 GitHub", "#28A745");
                
                // 刷新当前状态
                await CheckCurrentStatusAsync();
            }
            else
            {
                // 智能错误分析
                AddLog("\n✗ 推送失败");
                AnalyzePushError(pushOutput ?? "");
                
                // 刷新当前状态
                await CheckCurrentStatusAsync();
            }
        }

        private enum LocalChangesChoice
        {
            KeepLocal,
            DiscardLocal,
            Cancel
        }

        /// <summary>
        /// 显示带滚动条的消息对话框，适用于长内容
        /// </summary>
        private void ShowScrollableMessageBox(string title, string message, string icon = "ℹ️")
        {
            var screenHeight = SystemParameters.WorkArea.Height;
            var dialogHeight = Math.Min(screenHeight * 0.6, 500);
            
            var dialog = new Window
            {
                Title = title,
                Width = 600,
                Height = dialogHeight,
                MinHeight = 300,
                MaxHeight = screenHeight * 0.8,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                ResizeMode = ResizeMode.CanResize,
                Background = System.Windows.Media.Brushes.White
            };

            var mainGrid = new System.Windows.Controls.Grid
            {
                Margin = new Thickness(20)
            };
            
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // 内容区域（带滚动条）
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 15)
            };
            System.Windows.Controls.Grid.SetRow(scrollViewer, 0);

            var contentPanel = new System.Windows.Controls.StackPanel();

            // 图标和标题
            var titlePanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var iconText = new System.Windows.Controls.TextBlock
            {
                Text = icon,
                FontSize = 32,
                Margin = new Thickness(0, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titlePanel.Children.Add(iconText);

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.Black
            };
            titlePanel.Children.Add(titleText);
            contentPanel.Children.Add(titlePanel);

            // 消息内容
            var messageText = new System.Windows.Controls.TextBlock
            {
                Text = message,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Foreground = System.Windows.Media.Brushes.Black
            };
            contentPanel.Children.Add(messageText);

            scrollViewer.Content = contentPanel;
            mainGrid.Children.Add(scrollViewer);

            // 确定按钮
            var okButton = new System.Windows.Controls.Button
            {
                Content = "确定",
                Width = 100,
                Height = 38,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 123, 255)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            okButton.Click += (s, e) => dialog.Close();
            System.Windows.Controls.Grid.SetRow(okButton, 1);
            mainGrid.Children.Add(okButton);

            dialog.Content = mainGrid;
            dialog.ShowDialog();
        }

        private LocalChangesChoice ShowLocalChangesDialog(int remoteAheadCount)
        {
            // 获取屏幕高度用于自适应
            var screenHeight = SystemParameters.WorkArea.Height;
            var dialogHeight = Math.Min(screenHeight * 0.6, 450);
            
            var dialog = new Window
            {
                Title = "需要处理本地更改",
                Width = 550,
                Height = dialogHeight,
                MinHeight = 350,
                MaxHeight = screenHeight * 0.8,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                ResizeMode = ResizeMode.CanResize,
                Background = System.Windows.Media.Brushes.White
            };

            // 使用 ScrollViewer 包裹内容
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                Padding = new Thickness(20)
            };

            var stackPanel = new System.Windows.Controls.StackPanel();

            // 标题
            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "⚠️ 需要处理本地更改",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7))
            };
            stackPanel.Children.Add(titleText);

            // 说明
            var descText = new System.Windows.Controls.TextBlock
            {
                Text = $"检测到同步冲突：\n\n• GitHub 领先本地 {remoteAheadCount} 个提交\n• 本地有未提交的更改\n\n为了拉取远程数据，需要先处理本地更改。",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = System.Windows.Media.Brushes.Black
            };
            stackPanel.Children.Add(descText);

            // 分隔线
            var separator = new System.Windows.Controls.Separator
            {
                Margin = new Thickness(0, 0, 0, 15),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(222, 226, 230))
            };
            stackPanel.Children.Add(separator);

            // 选项说明
            var optionTitle = new System.Windows.Controls.TextBlock
            {
                Text = "请选择处理方式：",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 15),
                Foreground = System.Windows.Media.Brushes.Black
            };
            stackPanel.Children.Add(optionTitle);

            // 选项1：保留本地（推荐）
            var keepBorder = new System.Windows.Controls.Border
            {
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 123, 255)),
                BorderThickness = new Thickness(2),
                CornerRadius = new System.Windows.CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 12),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 244, 253))
            };
            
            var keepButton = new System.Windows.Controls.Button
            {
                Content = "✓ 保留本地更改（推荐）\n\n先提交本地更改，再拉取并合并远程更改",
                MinHeight = 70,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(15, 12, 15, 12),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = LocalChangesChoice.KeepLocal
            };
            keepButton.Click += (s, e) => { dialog.Tag = LocalChangesChoice.KeepLocal; dialog.Close(); };
            keepBorder.Child = keepButton;
            stackPanel.Children.Add(keepBorder);

            // 选项2：放弃本地（危险）
            var discardBorder = new System.Windows.Controls.Border
            {
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                BorderThickness = new Thickness(2),
                CornerRadius = new System.Windows.CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 12),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 215, 218))
            };
            
            var discardButton = new System.Windows.Controls.Button
            {
                Content = "⚠️ 放弃本地更改（危险！）\n\n永久删除本地更改，使用远程数据覆盖",
                MinHeight = 70,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(15, 12, 15, 12),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = LocalChangesChoice.DiscardLocal
            };
            discardButton.Click += (s, e) => { dialog.Tag = LocalChangesChoice.DiscardLocal; dialog.Close(); };
            discardBorder.Child = discardButton;
            stackPanel.Children.Add(discardBorder);

            // 底部按钮栏
            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            // 取消按钮
            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "取消",
                Width = 100,
                Height = 38,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelButton.Click += (s, e) => { dialog.Tag = LocalChangesChoice.Cancel; dialog.Close(); };
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(buttonPanel);

            scrollViewer.Content = stackPanel;
            dialog.Content = scrollViewer;
            dialog.ShowDialog();

            return dialog.Tag is LocalChangesChoice choice ? choice : LocalChangesChoice.Cancel;
        }

        private void HandlePullError(string pullOutput)
        {
            if (pullOutput.Contains("conflict") || pullOutput.Contains("CONFLICT"))
            {
                AddLog("\n✗ 拉取失败：存在冲突");
                MessageBox.Show(
                    "拉取失败：存在冲突！\n\n" +
                    "本地和远程修改了相同的文件。\n\n" +
                    "请手动解决冲突：\n" +
                    "1. 打开冲突文件\n" +
                    "2. 解决冲突标记（<<<<<<< ======= >>>>>>>）\n" +
                    "3. git add .\n" +
                    "4. git commit\n" +
                    "5. 再次使用「更新推送」",
                    "冲突",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else if (pullOutput.Contains("overwritten by merge") || pullOutput.Contains("would be overwritten"))
            {
                AddLog("\n✗ 拉取失败：本地有未提交的更改会被覆盖");
                MessageBox.Show(
                    "拉取失败：本地更改会被覆盖！\n\n" +
                    "请先提交或暂存本地更改。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else if (pullOutput.Contains("Failed to connect") || pullOutput.Contains("unable to access"))
            {
                AddLog("\n✗ 拉取失败：网络连接失败");
                MessageBox.Show(
                    "拉取失败：网络连接失败！\n\n" +
                    "请检查网络连接或稍后重试。",
                    "网络错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                AddLog("\n✗ 拉取失败");
                MessageBox.Show(
                    "拉取失败！\n\n" +
                    $"错误信息：\n{pullOutput}\n\n" +
                    "请检查网络连接或手动执行：\n" +
                    "git pull origin main",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task<bool> CheckIfAlreadyInitialized()
        {
            // 检查是否已有 .git 文件夹
            var gitPath = System.IO.Path.Combine(Settings.LocalFolder, ".git");
            var hasGitFolder = System.IO.Directory.Exists(gitPath);
            
            if (!hasGitFolder)
            {
                // 没有 .git 文件夹，可以继续
                return false;
            }

            AddLog("\n⚠️ 检测到已有 Git 仓库");

            // 检查是否有提交历史
            var (hasCommits, _) = await _gitService.ExecuteGitCommandAsync("rev-parse HEAD");
            
            // 检查是否已配置远程仓库
            var (hasRemote, remoteUrl) = await _gitService.ExecuteGitCommandAsync("remote get-url origin");
            
            // 情况1：有远程配置 + 有提交历史 → 应该用"更新推送"
            if (hasRemote && hasCommits)
            {
                AddLog($"⚠️ 已配置远程仓库: {remoteUrl?.Trim()}");
                AddLog("⚠️ 已有提交历史");
                
                var result = MessageBox.Show(
                    "⚠️ 检测到已有 Git 仓库\n\n" +
                    "当前文件夹已经是 Git 仓库，并且已配置远程仓库。\n\n" +
                    "「初始推送」适用于第一次推送到 GitHub。\n" +
                    "您应该使用「更新推送」功能。\n\n" +
                    "是否前往「更新推送」页面？\n\n" +
                    "• 点击「是」- 前往「更新推送」页面（推荐）\n" +
                    "• 点击「否」- 取消操作",
                    "已有 Git 仓库",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // 切换到更新推送页面
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedPage = "Update";
                    });
                }
                
                AddLog("✗ 操作已取消：请使用「更新推送」功能");
                return true; // 阻止继续执行
            }
            
            // 情况2：没有提交历史（刚执行过"初始化历史"）→ 直接继续，不弹窗
            if (!hasCommits)
            {
                AddLog("✓ 检测到全新的 Git 仓库（无提交历史）");
                AddLog("✓ 这是正常的初始推送场景，继续执行");
                return false; // 允许继续执行，不弹窗
            }
            
            // 情况3：有提交历史但没有远程配置 → 询问用户
            if (hasCommits && !hasRemote)
            {
                AddLog("⚠️ 检测到本地 Git 仓库有提交历史，但未配置远程仓库");
                
                var result = MessageBox.Show(
                    "⚠️ 检测到本地 Git 仓库\n\n" +
                    "当前文件夹已经是 Git 仓库，并且有提交历史，但未配置远程仓库。\n\n" +
                    "继续执行将：\n" +
                    "• 保留现有的提交历史\n" +
                    "• 配置远程仓库\n" +
                    "• 推送所有提交到 GitHub\n\n" +
                    "是否继续？\n\n" +
                    "• 点击「是」- 继续推送（会保留历史）\n" +
                    "• 点击「否」- 取消操作",
                    "确认操作",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    AddLog("✗ 操作已取消");
                    return true; // 阻止继续执行
                }
                
                AddLog("✓ 用户确认继续");
                return false; // 允许继续执行
            }
            
            // 其他情况：允许继续
            return false;
        }

        private void AnalyzeInitialPushError(string errorOutput)
        {
            string title = "推送失败";
            string message = "";
            MessageBoxImage icon = MessageBoxImage.Error;

            // 分支不存在错误
            if (errorOutput.Contains("src refspec") && errorOutput.Contains("does not match any"))
            {
                title = "❌ 分支不存在";
                icon = MessageBoxImage.Error;
                message = "推送失败：本地没有 main 分支\n\n" +
                         "错误原因：\n" +
                         "• 本地仓库没有创建 main 分支\n" +
                         "• 可能是提交步骤失败\n" +
                         "• 或者没有文件可以提交\n\n" +
                         "建议操作：\n" +
                         "1. 检查项目文件夹是否有文件\n" +
                         "2. 确保提交步骤成功执行\n" +
                         "3. 查看日志中的提交步骤是否有错误\n" +
                         "4. 重新执行「初始推送」\n\n" +
                         "💡 提示：\n" +
                         "Git 分支只有在第一次提交后才会被创建。\n" +
                         "如果提交失败（例如没有文件），就不会创建分支。\n\n" +
                         "💾 数据说明：\n" +
                         "\"本地提交已保存\"指的是提交保存在本地 .git 文件夹中。\n" +
                         "即使推送失败，你的代码和提交历史都安全地保存在本地。";
                
                AddLog("\n" + message);
            }
            // 网络错误
            else if (errorOutput.Contains("Failed to connect") || 
                errorOutput.Contains("Could not resolve host") ||
                errorOutput.Contains("Connection timed out") ||
                errorOutput.Contains("Connection was reset") ||
                errorOutput.Contains("Recv failure") ||
                errorOutput.Contains("unable to access"))
            {
                title = "❌ 网络连接失败";
                icon = MessageBoxImage.Warning;
                message = "无法连接到 GitHub 服务器\n\n" +
                         "可能的原因：\n" +
                         "• 网络不稳定或连接被重置\n" +
                         "• 防火墙或代理阻止连接\n" +
                         "• GitHub 服务暂时不可用\n" +
                         "• SSL/TLS 握手失败\n\n" +
                         "建议操作：\n" +
                         "1. 检查网络连接是否正常\n" +
                         "2. 如果使用代理，配置 Git 代理：\n" +
                         "   git config --global http.proxy http://127.0.0.1:7890\n" +
                         "3. 尝试配置 Git 使用更稳定的协议：\n" +
                         "   git config --global http.postBuffer 524288000\n" +
                         "4. 稍后重试\n\n" +
                         "💡 好消息：本地提交已保存，修复网络后可直接重试";
                
                AddLog("\n" + message);
            }
            // 远程仓库已有内容
            else if (errorOutput.Contains("rejected") || 
                     errorOutput.Contains("non-fast-forward") ||
                     errorOutput.Contains("Updates were rejected"))
            {
                title = "⚠️ 远程仓库已有内容";
                icon = MessageBoxImage.Warning;
                message = "推送被拒绝\n\n" +
                         "原因：\n" +
                         "• 远程仓库已有提交记录\n" +
                         "• 无法进行快进合并\n\n" +
                         "建议操作：\n" +
                         "使用「强制推送」覆盖远程仓库\n" +
                         "（会删除远程的所有内容和历史）";
                
                AddLog("\n" + message);
            }
            // 权限错误
            else if (errorOutput.Contains("Permission denied") || 
                     errorOutput.Contains("Authentication failed") ||
                     errorOutput.Contains("Invalid username or password") ||
                     errorOutput.Contains("403"))
            {
                title = "❌ 权限验证失败";
                icon = MessageBoxImage.Error;
                message = "GitHub 身份验证失败\n\n" +
                         "可能的原因：\n" +
                         "• GitHub Token 无效或过期\n" +
                         "• Token 权限不足（需要 repo 权限）\n" +
                         "• 用户名或仓库地址错误\n\n" +
                         "建议操作：\n" +
                         "1. 前往「设置」页面检查 Token\n" +
                         "2. 确保 Token 有 repo 权限\n" +
                         "3. 检查仓库地址格式：\n" +
                         "   https://github.com/用户名/仓库名.git\n" +
                         "4. 重新生成 Token（如果过期）";
                
                AddLog("\n" + message);
            }
            // 仓库不存在
            else if (errorOutput.Contains("not found") || 
                     errorOutput.Contains("404"))
            {
                title = "❌ 仓库不存在";
                icon = MessageBoxImage.Error;
                message = "找不到指定的 GitHub 仓库\n\n" +
                         "可能的原因：\n" +
                         "• 仓库地址错误\n" +
                         "• 仓库已被删除\n" +
                         "• 仓库是私有的但没有权限\n\n" +
                         "建议操作：\n" +
                         "1. 检查仓库地址是否正确\n" +
                         "2. 确认仓库已在 GitHub 上创建\n" +
                         "3. 如果是私有仓库，确保 Token 有权限";
                
                AddLog("\n" + message);
            }
            // 其他错误
            else
            {
                title = "❌ 推送失败";
                icon = MessageBoxImage.Error;
                message = "推送到 GitHub 失败\n\n" +
                         "错误信息：\n" + errorOutput + "\n\n" +
                         "建议操作：\n" +
                         "1. 查看日志了解详细错误\n" +
                         "2. 检查网络和权限设置\n" +
                         "3. 确认仓库地址正确\n" +
                         "4. 稍后重试\n\n" +
                         "💡 本地提交已保存";
                
                AddLog("\n提示: 请查看错误信息并检查配置");
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }

        private void AnalyzePushError(string errorOutput)
        {
            string title = "推送失败";
            string message = "";
            MessageBoxImage icon = MessageBoxImage.Error;

            // 分支不存在错误
            if (errorOutput.Contains("src refspec") && errorOutput.Contains("does not match any"))
            {
                title = "❌ 分支不存在";
                icon = MessageBoxImage.Error;
                message = "推送失败：本地没有 main 分支\n\n" +
                         "错误原因：\n" +
                         "• 本地仓库没有创建 main 分支\n" +
                         "• 可能是没有任何提交\n" +
                         "• 或者提交失败导致分支未创建\n\n" +
                         "建议操作：\n" +
                         "1. 检查是否有文件需要提交\n" +
                         "2. 确保至少有一次成功的提交\n" +
                         "3. 使用「初始推送」重新初始化\n\n" +
                         "💡 提示：\n" +
                         "Git 分支只有在第一次提交后才会被创建。\n" +
                         "如果没有提交，就没有分支可以推送。";
                
                AddLog("\n" + message);
            }
            // 网络错误
            else if (errorOutput.Contains("Failed to connect") || 
                errorOutput.Contains("Could not resolve host") ||
                errorOutput.Contains("Connection timed out") ||
                errorOutput.Contains("unable to access"))
            {
                title = "❌ 网络连接失败";
                icon = MessageBoxImage.Warning;
                message = "无法连接到 GitHub 服务器\n\n" +
                         "可能的原因：\n" +
                         "• 网络不稳定或被防火墙阻止\n" +
                         "• 需要配置代理\n" +
                         "• GitHub 服务暂时不可用\n\n" +
                         "建议操作：\n" +
                         "1. 检查网络连接\n" +
                         "2. 如果使用代理，配置 Git 代理：\n" +
                         "   git config --global http.proxy http://127.0.0.1:7890\n" +
                         "3. 稍后重试\n\n" +
                         "💡 好消息：本地提交已保存，修复网络后可直接推送";
                
                AddLog("\n" + message);
            }
            // 历史冲突错误
            else if (errorOutput.Contains("rejected") || 
                     errorOutput.Contains("non-fast-forward") ||
                     errorOutput.Contains("Updates were rejected"))
            {
                title = "⚠️ 推送被拒绝";
                icon = MessageBoxImage.Warning;
                message = "GitHub 领先本地\n\n" +
                         "原因：\n" +
                         "• GitHub 上有新的提交\n" +
                         "• 本地历史落后于远程\n\n" +
                         "建议操作：\n" +
                         "1. 使用 git pull 拉取远程更改\n" +
                         "2. 解决可能的冲突\n" +
                         "3. 再次推送\n\n" +
                         "或者重新点击「更新推送」，系统会自动处理";
                
                AddLog("\n" + message);
            }
            // 权限错误
            else if (errorOutput.Contains("Permission denied") || 
                     errorOutput.Contains("Authentication failed") ||
                     errorOutput.Contains("Invalid username or password"))
            {
                title = "❌ 权限验证失败";
                icon = MessageBoxImage.Error;
                message = "GitHub 身份验证失败\n\n" +
                         "可能的原因：\n" +
                         "• GitHub Token 无效或过期\n" +
                         "• Token 权限不足\n" +
                         "• 用户名或仓库地址错误\n\n" +
                         "建议操作：\n" +
                         "1. 前往「设置」页面检查 Token\n" +
                         "2. 确保 Token 有 repo 权限\n" +
                         "3. 重新生成 Token（如果过期）\n" +
                         "4. 检查仓库地址是否正确";
                
                AddLog("\n" + message);
            }
            // 其他错误
            else
            {
                title = "❌ 推送失败";
                icon = MessageBoxImage.Error;
                message = "推送到 GitHub 失败\n\n" +
                         "错误信息：\n" + errorOutput + "\n\n" +
                         "建议操作：\n" +
                         "1. 查看日志了解详细错误\n" +
                         "2. 检查网络和权限设置\n" +
                         "3. 稍后重试\n\n" +
                         "💡 本地提交已保存";
                
                AddLog("\n提示: 请查看错误信息并检查配置");
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }

        private async Task ReleaseAsync()
        {
            LogMessages.Clear();

            if (string.IsNullOrWhiteSpace(VersionNumber))
            {
                AddLog("✗ 版本号不能为空");
                MessageBox.Show("请输入版本号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 验证版本号格式
            if (!IsValidVersionFormat(VersionNumber))
            {
                AddLog("✗ 版本号格式不正确");
                MessageBox.Show("版本号格式应为: v1.0.0 或 1.0.0", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AddLog("========================================");
            AddLog("  🚀 版本发布");
            AddLog("========================================");

            InitializeGitService();

            var note = string.IsNullOrWhiteSpace(VersionNote) ? $"Release {VersionNumber}" : VersionNote;

            AddLog($"\n版本号: {VersionNumber}");
            AddLog($"说明: {note}");

            AddLog("\n[1/5] 添加所有更改...");
            await _gitService.ExecuteGitCommandAsync("add .");

            AddLog("\n[2/5] 提交更改...");
            await _gitService.ExecuteGitCommandAsync($"commit -m \"Release {VersionNumber}: {note}\"");

            AddLog("\n[3/5] 创建版本标签...");
            var (success1, _) = await _gitService.ExecuteGitCommandAsync($"tag -a {VersionNumber} -m \"{note}\"");
            if (!success1)
            {
                AddLog("✗ 创建标签失败（可能标签已存在）");
                return;
            }

            AddLog("\n[4/5] 推送代码...");
            await _gitService.ExecuteGitCommandAsync($"remote set-url origin {_gitService.GetRemoteUrl()}");
            var (success2, _) = await _gitService.ExecuteGitCommandAsync("push origin main");
            if (!success2)
            {
                AddLog("✗ 推送代码失败");
                return;
            }

            AddLog("\n[5/5] 推送标签...");
            var (success3, _) = await _gitService.ExecuteGitCommandAsync($"push origin {VersionNumber}");

            if (success3)
            {
                AddLog("\n========================================");
                AddLog($"  ✓ 版本 {VersionNumber} 发布成功！");
                AddLog($"  查看: {Settings.RepoUrl.Replace(".git", "")}/releases");
                AddLog("========================================");
                
                // 刷新版本历史
                await LoadVersionHistoryAsync();
            }
            else
            {
                AddLog("\n✗ 推送标签失败");
            }
        }

        private async Task LoadVersionHistoryAsync()
        {
            InitializeGitService();
            
            AddLog("正在加载版本历史...");
            await _gitService.ExecuteGitCommandAsync("fetch origin --tags");
            
            var (success, output) = await _gitService.ExecuteGitCommandAsync("tag -l --sort=-version:refname --format=%(refname:short)|%(contents:subject)|%(creatordate:short)|%(objectname:short)");
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                VersionHistory.Clear();
                
                if (success && !string.IsNullOrWhiteSpace(output))
                {
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 4)
                        {
                            VersionHistory.Add(new VersionInfo
                            {
                                TagName = parts[0],
                                Message = parts[1],
                                Date = parts[2],
                                CommitHash = parts[3]
                            });
                        }
                    }
                    
                    if (VersionHistory.Count > 0)
                    {
                        CurrentVersion = VersionHistory[0].TagName;
                        AddLog($"✓ 当前版本: {CurrentVersion}");
                    }
                    else
                    {
                        CurrentVersion = "无版本";
                        AddLog("ℹ 暂无版本标签");
                    }
                }
                else
                {
                    CurrentVersion = "无版本";
                    AddLog("ℹ 暂无版本标签");
                }
            });
        }

        private void AutoIncrementVersion()
        {
            if (CurrentVersion == "无版本" || string.IsNullOrWhiteSpace(CurrentVersion))
            {
                VersionNumber = "v1.0.0";
                return;
            }

            var version = CurrentVersion.TrimStart('v');
            var parts = version.Split('.');
            
            if (parts.Length == 3 && 
                int.TryParse(parts[0], out int major) &&
                int.TryParse(parts[1], out int minor) &&
                int.TryParse(parts[2], out int patch))
            {
                // 默认递增补丁版本号
                patch++;
                VersionNumber = $"v{major}.{minor}.{patch}";
                AddLog($"✓ 自动生成版本号: {VersionNumber}");
            }
            else
            {
                VersionNumber = "v1.0.0";
                AddLog("⚠ 无法解析当前版本，使用默认版本号");
            }
        }

        private async Task DeleteVersionAsync()
        {
            if (SelectedVersion == null)
            {
                MessageBox.Show("请先选择要删除的版本", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除版本 {SelectedVersion.TagName} 吗？\n\n" +
                "此操作将删除本地和 GitHub 上的标签。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            LogMessages.Clear();
            AddLog($"正在删除版本 {SelectedVersion.TagName}...");

            InitializeGitService();

            // 删除本地标签
            var (success1, _) = await _gitService.ExecuteGitCommandAsync($"tag -d {SelectedVersion.TagName}");
            if (success1)
            {
                AddLog($"✓ 本地标签已删除");
            }

            // 删除远程标签
            await _gitService.ExecuteGitCommandAsync($"remote set-url origin {_gitService.GetRemoteUrl()}");
            var (success2, _) = await _gitService.ExecuteGitCommandAsync($"push origin :refs/tags/{SelectedVersion.TagName}");
            
            if (success2)
            {
                AddLog($"✓ GitHub 标签已删除");
                MessageBox.Show($"版本 {SelectedVersion.TagName} 已删除", "成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                // 刷新版本历史
                await LoadVersionHistoryAsync();
            }
            else
            {
                AddLog("✗ 删除 GitHub 标签失败");
            }
        }

        private bool IsValidVersionFormat(string version)
        {
            // 支持 v1.0.0 或 1.0.0 格式
            var v = version.TrimStart('v');
            var parts = v.Split('.');
            
            if (parts.Length != 3) return false;
            
            return parts.All(p => int.TryParse(p, out _));
        }

        private async Task ViewVersionAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🔍 仓库查看（远程）");
            AddLog("========================================");

            InitializeGitService();

            AddLog("\n========== GitHub 远程提交历史 ==========");
            await _gitService.ExecuteGitCommandAsync("fetch origin");
            await _gitService.ExecuteGitCommandAsync("log origin/main --oneline --graph -20");

            AddLog("\n========== 同步状态 ==========");
            await _gitService.ExecuteGitCommandAsync("status");
            AddLog("\n本地领先 GitHub:");
            await _gitService.ExecuteGitCommandAsync("log origin/main..HEAD --oneline");
            AddLog("\nGitHub 领先本地:");
            await _gitService.ExecuteGitCommandAsync("log HEAD..origin/main --oneline");

            AddLog("\n========== 远程分支信息 ==========");
            await _gitService.ExecuteGitCommandAsync("branch -r");

            AddLog("\n========== 远程标签信息 ==========");
            await _gitService.ExecuteGitCommandAsync("fetch origin --tags");
            await _gitService.ExecuteGitCommandAsync("tag -l -n");

            AddLog("\n✓ 完成！");
        }

        private async Task ViewCommitHistoryAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  📜 提交历史 - 图形化展示");
            AddLog("========================================");

            InitializeGitService();
            
            AddLog("\n正在打开提交历史窗口...");
            
            // 创建并显示提交历史弹窗
            Application.Current.Dispatcher.Invoke(() =>
            {
                var viewModel = new CommitHistoryViewModel(_gitService, Settings.LocalFolder, Settings.GitHubToken, Settings.RepoUrl);
                
                // 订阅日志事件
                viewModel.OnLog += (message) => AddLog(message);
                
                // 订阅提交切换事件，刷新主窗口
                viewModel.OnCommitSwitched += () =>
                {
                    AddLog("✓ 版本已切换，建议刷新查看");
                };
                
                var dialog = new Views.CommitHistoryDialog(viewModel)
                {
                    Owner = Application.Current.MainWindow
                };
                
                AddLog("✓ 提交历史窗口已打开");
                dialog.ShowDialog();
            });
            
            await Task.CompletedTask;
        }

        private async Task SwitchToCommitAsync(CommitInfo? commit)
        {
            if (commit == null) return;
            
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🔄 切换到历史版本");
            AddLog("========================================");
            
            InitializeGitService();
            
            // 检查是否已经是当前版本
            if (commit.IsCurrent)
            {
                ShowNotificationRequested?.Invoke("ℹ️ 已经是当前版本", "#17A2B8");
                return;
            }
            
            AddLog($"\n目标版本：{commit.ShortHash} - {commit.Message}");
            AddLog($"作者：{commit.Author}");
            AddLog($"时间：{commit.Date}");
            
            // 检查工作区状态
            AddLog("\n[1/3] 检查工作区状态...");
            var (_, statusOutput) = await _gitService.ExecuteGitCommandAsync("status --short");
            
            if (!string.IsNullOrWhiteSpace(statusOutput))
            {
                AddLog("⚠️ 工作区有未提交的更改");
                
                var result = MessageBox.Show(
                    "⚠️ 工作区有未提交的更改\n\n" +
                    "切换版本前需要处理未提交的更改。\n\n" +
                    "请选择操作：\n\n" +
                    "• 点击「是」- 暂存更改并切换（推荐）\n" +
                    "• 点击「否」- 取消操作",
                    "未提交的更改",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    AddLog("\n正在暂存当前更改...");
                    var (stashSuccess, _) = await _gitService.ExecuteGitCommandAsync("stash push -m \"切换版本前自动暂存\"");
                    
                    if (!stashSuccess)
                    {
                        AddLog("✗ 暂存失败");
                        MessageBox.Show(
                            "暂存失败！\n\n" +
                            "请手动处理未提交的更改后再试。",
                            "错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                    
                    AddLog("✓ 更改已暂存");
                }
                else
                {
                    AddLog("\n✗ 操作已取消");
                    return;
                }
            }
            else
            {
                AddLog("✓ 工作区干净");
            }
            
            // 切换到指定提交
            AddLog($"\n[2/3] 切换到版本 {commit.ShortHash}...");
            var (checkoutSuccess, checkoutOutput) = await _gitService.ExecuteGitCommandAsync($"checkout {commit.Hash}");
            
            if (!checkoutSuccess)
            {
                AddLog("✗ 切换失败");
                MessageBox.Show(
                    $"切换失败！\n\n错误信息：\n{checkoutOutput}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            
            AddLog("✓ 切换成功");
            
            // 显示状态
            AddLog("\n[3/3] 当前状态...");
            await _gitService.ExecuteGitCommandAsync("status");
            
            AddLog("\n========================================");
            AddLog("  ✓ 已切换到历史版本");
            AddLog("========================================");
            AddLog($"\n当前版本：{commit.ShortHash} - {commit.Message}");
            AddLog("\n⚠️ 注意：");
            AddLog("• 当前处于「分离头指针」状态");
            AddLog("• 可以查看和运行历史代码");
            AddLog("• 不建议在此状态下提交更改");
            AddLog("\n返回最新版本：");
            AddLog("git checkout main");
            
            // 显示成功通知
            ShowNotificationRequested?.Invoke($"✓ 已切换到历史版本：{commit.ShortHash} - {commit.Message}", "#28A745");
            
            // 弹窗提示
            var returnResult = MessageBox.Show(
                $"✓ 已切换到历史版本\n\n" +
                $"版本：{commit.ShortHash}\n" +
                $"信息：{commit.Message}\n" +
                $"时间：{commit.Date}\n\n" +
                "⚠️ 当前处于「分离头指针」状态\n" +
                "• 可以查看和运行历史代码\n" +
                "• 不建议在此状态下提交更改\n\n" +
                "是否立即返回最新版本？",
                "切换成功",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            
            if (returnResult == MessageBoxResult.Yes)
            {
                await ReturnToMainBranchAsync();
            }
        }
        
        private async Task ReturnToMainBranchAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🔙 返回最新版本");
            AddLog("========================================");
            
            InitializeGitService();
            
            AddLog("\n正在切换到 main 分支...");
            var (success, output) = await _gitService.ExecuteGitCommandAsync("checkout main");
            
            if (success)
            {
                AddLog("✓ 已返回最新版本");
                
                // 检查是否有暂存的更改
                var (hasStash, _) = await _gitService.ExecuteGitCommandAsync("stash list");
                if (hasStash)
                {
                    var result = MessageBox.Show(
                        "检测到之前暂存的更改\n\n" +
                        "是否恢复暂存的更改？",
                        "恢复更改",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        AddLog("\n正在恢复暂存的更改...");
                        await _gitService.ExecuteGitCommandAsync("stash pop");
                        AddLog("✓ 更改已恢复");
                    }
                }
                
                ShowNotificationRequested?.Invoke("✓ 已返回最新版本", "#28A745");
            }
            else
            {
                AddLog("✗ 返回失败");
                MessageBox.Show(
                    $"返回失败！\n\n错误信息：\n{output}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ViewSyncStatusAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🔄 同步状态");
            AddLog("========================================");

            InitializeGitService();
            
            // 获取当前分支名
            AddLog("\n正在检测当前分支...");
            var (branchSuccess, branchOutput) = await _gitService.ExecuteGitCommandAsync("branch --show-current");
            var currentBranch = branchOutput?.Trim();
            
            if (string.IsNullOrEmpty(currentBranch))
            {
                AddLog("✗ 当前处于游离 HEAD 状态，无法检查同步状态");
                MessageBox.Show(
                    "⚠️ 游离 HEAD 状态\n\n" +
                    "当前不在任何分支上，无法检查同步状态。\n\n" +
                    "建议操作：\n" +
                    "1. 切换到一个分支（如 main）\n" +
                    "2. 或创建新分支保存当前更改",
                    "无法检查同步状态",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            AddLog($"✓ 当前分支：{currentBranch}");
            
            // 先获取远程最新信息
            AddLog("\n正在从 GitHub 获取最新信息...");
            var (fetchSuccess, fetchOutput) = await _gitService.ExecuteGitCommandAsync("fetch origin");
            
            if (!fetchSuccess)
            {
                AddLog("✗ 无法连接到远程仓库");
                MessageBox.Show(
                    "❌ 无法连接到远程仓库\n\n" +
                    "请检查：\n" +
                    "1. 网络连接是否正常\n" +
                    "2. 远程仓库地址是否正确\n" +
                    "3. GitHub Token 是否有效",
                    "连接失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            
            // 检查远程分支是否存在
            var (remoteBranchExists, _) = await _gitService.ExecuteGitCommandAsync($"rev-parse --verify origin/{currentBranch}");
            
            if (!remoteBranchExists)
            {
                AddLog($"⚠️ 远程不存在分支 origin/{currentBranch}");
                AddLog("这可能是一个新建的本地分支，尚未推送到远程");
                
                // 检查本地状态
                AddLog("\n========== 本地工作区状态 ==========");
                var (statusCheckSuccess, statusCheckOutput) = await _gitService.ExecuteGitCommandAsync("status --short");
                var hasChanges = !string.IsNullOrWhiteSpace(statusCheckOutput);
                
                MessageBox.Show(
                    $"⚠️ 远程分支不存在\n\n" +
                    $"当前分支：{currentBranch}\n" +
                    $"远程分支：origin/{currentBranch} (不存在)\n\n" +
                    "这是一个新建的本地分支，尚未推送到远程。\n\n" +
                    "建议使用「更新推送」将此分支推送到 GitHub。",
                    "远程分支不存在",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            
            // 检查本地状态
            AddLog("\n========== 本地工作区状态 ==========");
            var (statusSuccess, statusOutput) = await _gitService.ExecuteGitCommandAsync("status --short");
            
            // 检查本地领先的提交
            AddLog("\n========== 本地领先 GitHub ==========");
            var (localAheadSuccess, localAheadOutput) = await _gitService.ExecuteGitCommandAsync($"log origin/{currentBranch}..HEAD --oneline");
            
            // 检查远程领先的提交
            AddLog("\n========== GitHub 领先本地 ==========");
            var (remoteAheadSuccess, remoteAheadOutput) = await _gitService.ExecuteGitCommandAsync($"log HEAD..origin/{currentBranch} --oneline");
            
            AddLog("\n✓ 完成！");
            
            // 分析同步状态
            var hasLocalChanges = !string.IsNullOrWhiteSpace(statusOutput);
            var localAheadCount = CountCommits(localAheadOutput);
            var remoteAheadCount = CountCommits(remoteAheadOutput);
            
            // 显示同步状态对话框（带操作选项）
            ShowSyncStatusDialog(hasLocalChanges, localAheadCount, remoteAheadCount);
        }
        
        private int CountCommits(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return 0;
            
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length;
        }
        
        private void ShowSyncStatusDialog(bool hasLocalChanges, int localAheadCount, int remoteAheadCount)
        {
            // 判断同步状态
            if (!hasLocalChanges && localAheadCount == 0 && remoteAheadCount == 0)
            {
                // 完全同步 - 使用悬浮通知
                ShowNotificationRequested?.Invoke(
                    "✓ 完全同步！本地和 GitHub 完全同步，无需任何操作", 
                    "#28A745");
                return;
            }
            
            // 需要操作的情况 - 显示带操作按钮的对话框
            string title = "";
            string description = "";
            bool needsUpdate = false;
            
            if (hasLocalChanges && localAheadCount == 0 && remoteAheadCount == 0)
            {
                // 只有本地未提交的更改
                title = "⚠️ 有未提交的更改";
                description = "本地有未提交的更改\n\n建议使用「更新推送」提交并推送到 GitHub。";
                needsUpdate = true;
            }
            else if (localAheadCount > 0 && remoteAheadCount == 0)
            {
                // 本地领先
                title = "⬆️ 本地领先";
                description = $"本地领先 GitHub {localAheadCount} 个提交";
                if (hasLocalChanges)
                    description += "\n还有未提交的更改";
                description += "\n\n建议使用「更新推送」推送到 GitHub。";
                needsUpdate = true;
            }
            else if (remoteAheadCount > 0 && localAheadCount == 0)
            {
                // 远程领先
                title = "⬇️ GitHub 领先";
                description = $"GitHub 领先本地 {remoteAheadCount} 个提交";
                if (hasLocalChanges)
                    description += "\n⚠️ 本地有未提交的更改";
                description += "\n\n建议使用「更新推送」自动处理同步。";
                needsUpdate = true;
            }
            else if (localAheadCount > 0 && remoteAheadCount > 0)
            {
                // 分叉了
                title = "⚠️ 历史分叉";
                description = $"本地和 GitHub 的历史已分叉！\n\n" +
                             $"• 本地领先 {localAheadCount} 个提交\n" +
                             $"• GitHub 领先 {remoteAheadCount} 个提交";
                if (hasLocalChanges)
                    description += "\n• 还有未提交的更改";
                description += "\n\n⚠️ 需要合并操作！\n建议使用「更新推送」自动处理。";
                needsUpdate = true;
            }
            
            if (needsUpdate)
            {
                ShowSyncActionDialog(title, description);
            }
        }
        
        private void ShowSyncActionDialog(string title, string description)
        {
            var dialog = new Window
            {
                Title = "同步状态",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20)
            };

            // 标题
            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(titleText);

            // 描述
            var descText = new System.Windows.Controls.TextBlock
            {
                Text = description,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 25),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };
            stackPanel.Children.Add(descText);

            // 操作按钮
            var updateButton = new System.Windows.Controls.Button
            {
                Content = "前往「更新推送」（推荐）\n自动处理同步问题",
                Height = 60,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(15, 10, 15, 10),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 123, 255)),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            updateButton.Click += (s, e) =>
            {
                dialog.Tag = "GoToUpdate";
                dialog.Close();
            };
            stackPanel.Children.Add(updateButton);

            // 手动处理按钮
            var manualButton = new System.Windows.Controls.Button
            {
                Content = "手动处理\n我会自己使用命令行处理",
                Height = 70,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(15, 10, 15, 10)
            };
            manualButton.Click += (s, e) =>
            {
                dialog.Tag = "Manual";
                dialog.Close();
            };
            stackPanel.Children.Add(manualButton);

            // 关闭按钮
            var closeButton = new System.Windows.Controls.Button
            {
                Content = "关闭",
                Width = 100,
                Height = 35,
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, e) =>
            {
                dialog.Tag = "Close";
                dialog.Close();
            };
            stackPanel.Children.Add(closeButton);

            dialog.Content = stackPanel;
            dialog.ShowDialog();

            // 根据用户选择执行操作
            if (dialog.Tag?.ToString() == "GoToUpdate")
            {
                // 跳转到更新推送页面
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SelectedPage = "Update";
                    AddLog("\n→ 已切换到「更新推送」页面");
                });
                
                MessageBox.Show(
                    "已切换到「更新推送」页面\n\n" +
                    "请点击「更新推送」按钮开始同步。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else if (dialog.Tag?.ToString() == "Manual")
            {
                // 显示手动操作提示
                var manualTips = "手动处理提示：\n\n";
                
                if (title.Contains("领先"))
                {
                    manualTips += "GitHub 领先本地：\n" +
                                 "git pull origin main\n\n" +
                                 "如果有冲突，解决后：\n" +
                                 "git add .\n" +
                                 "git commit\n" +
                                 "git push origin main";
                }
                else if (title.Contains("分叉"))
                {
                    manualTips += "历史分叉：\n" +
                                 "1. 提交本地更改（如果有）：\n" +
                                 "   git add .\n" +
                                 "   git commit -m \"本地更改\"\n\n" +
                                 "2. 拉取并合并：\n" +
                                 "   git pull origin main\n\n" +
                                 "3. 解决冲突后推送：\n" +
                                 "   git push origin main";
                }
                else
                {
                    manualTips += "本地有更改：\n" +
                                 "git add .\n" +
                                 "git commit -m \"更新\"\n" +
                                 "git push origin main";
                }
                
                MessageBox.Show(
                    manualTips,
                    "手动操作指南",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async Task ViewBranchesAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🌿 远程分支信息");
            AddLog("========================================");

            InitializeGitService();
            await _gitService.ExecuteGitCommandAsync("branch -r");
            AddLog("\n✓ 完成！");
        }

        private async Task ViewTagsAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🏷️ 远程标签信息");
            AddLog("========================================");

            InitializeGitService();
            await _gitService.ExecuteGitCommandAsync("fetch origin --tags");
            await _gitService.ExecuteGitCommandAsync("ls-remote --tags origin");
            AddLog("\n本地标签:");
            await _gitService.ExecuteGitCommandAsync("tag -l -n");
            AddLog("\n✓ 完成！");
        }

        private async Task CloneProjectAsync()
        {
            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  📦 项目克隆");
            AddLog("========================================");

            // 验证设置
            if (string.IsNullOrWhiteSpace(Settings.RepoUrl))
            {
                AddLog("✗ GitHub 仓库地址未设置");
                MessageBox.Show("请先在设置中配置 GitHub 仓库地址", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Settings.LocalFolder))
            {
                AddLog("✗ 本地文件夹路径未设置");
                MessageBox.Show("请先在设置中配置本地文件夹路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 检查目标文件夹是否为空
            if (System.IO.Directory.Exists(Settings.LocalFolder))
            {
                var allEntries = System.IO.Directory.GetFileSystemEntries(Settings.LocalFolder);
                
                // 过滤掉常见的系统文件和隐藏文件
                var systemFiles = new[] { "desktop.ini", "thumbs.db", ".ds_store" };
                var significantEntries = allEntries.Where(entry =>
                {
                    var fileName = System.IO.Path.GetFileName(entry).ToLower();
                    return !systemFiles.Contains(fileName);
                }).ToArray();

                if (significantEntries.Length > 0)
                {
                    AddLog($"✗ 目标文件夹非空（包含 {significantEntries.Length} 个文件/文件夹）");
                    
                    // 列出前5个文件/文件夹
                    var fileList = string.Join("\n", significantEntries.Take(5).Select(f => $"  • {System.IO.Path.GetFileName(f)}"));
                    if (significantEntries.Length > 5)
                    {
                        fileList += $"\n  ... 还有 {significantEntries.Length - 5} 个";
                    }
                    
                    var result = MessageBox.Show(
                        $"目标文件夹非空！\n\n" +
                        $"文件夹中包含以下内容：\n{fileList}\n\n" +
                        "为了避免覆盖已有内容，请选择一个空文件夹或新文件夹。\n\n" +
                        "是否前往设置页面选择新的文件夹？",
                        "⚠️ 目标文件夹非空",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        SelectedPage = "Settings";
                    }
                    return;
                }
            }

            var confirmResult = MessageBox.Show(
                $"确定要从 GitHub 克隆项目吗？\n\n" +
                $"仓库地址：\n{Settings.RepoUrl}\n\n" +
                $"目标文件夹：\n{Settings.LocalFolder}\n\n" +
                "此操作将从 GitHub 下载完整的项目到目标位置。",
                "确认克隆",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                AddLog($"\n仓库地址: {Settings.RepoUrl}");
                AddLog($"目标文件夹: {Settings.LocalFolder}");

                AddLog("\n[1/2] 准备克隆环境...");
                
                // 创建父目录（如果不存在）
                var parentDir = System.IO.Path.GetDirectoryName(Settings.LocalFolder);
                if (!string.IsNullOrEmpty(parentDir) && !System.IO.Directory.Exists(parentDir))
                {
                    System.IO.Directory.CreateDirectory(parentDir);
                    AddLog($"✓ 创建父目录: {parentDir}");
                }

                AddLog("\n[2/2] 从 GitHub 克隆项目...");
                AddLog("这可能需要几分钟，请耐心等待...");
                
                // 使用 GitService 执行 git clone
                var tempGitService = new GitService();
                tempGitService.OnOutput += AddLog;
                
                // 初始化到父目录
                tempGitService.Initialize(parentDir ?? Environment.CurrentDirectory, Settings.GitHubToken, Settings.RepoUrl);
                
                // 执行 git clone
                var folderName = System.IO.Path.GetFileName(Settings.LocalFolder);
                var (success, output) = await tempGitService.ExecuteGitCommandAsync($"clone {tempGitService.GetRemoteUrl()} \"{folderName}\"");

                if (success)
                {
                    AddLog("\n========================================");
                    AddLog("  ✓ 项目克隆成功！");
                    AddLog($"  目标位置: {Settings.LocalFolder}");
                    AddLog("========================================");

                    MessageBox.Show(
                        $"项目已成功从 GitHub 克隆到：\n{Settings.LocalFolder}\n\n" +
                        "您现在可以在此位置维护项目。",
                        "克隆成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    AddLog("\n✗ 克隆失败");
                    MessageBox.Show("克隆失败，请检查网络连接和仓库地址", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AddLog($"\n✗ 克隆失败: {ex.Message}");
                MessageBox.Show($"克隆失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task InitHistoryAsync()
        {
            var result = MessageBox.Show(
                "⚠️ 警告：此操作将删除本地 Git 历史！\n\n" +
                "操作说明：\n" +
                "• 删除本地 .git 文件夹\n" +
                "• 重新执行 git init\n" +
                "• 创建全新的 Git 历史\n" +
                "• 本地文件完全不受影响\n" +
                "• 远程仓库不受影响\n\n" +
                "⚠️ 本地所有提交历史、分支、标签将永久丢失！\n\n" +
                "确定要继续吗？",
                "⚠️ 确认初始化历史",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🔄 初始化历史");
            AddLog("========================================");

            InitializeGitService();

            try
            {
                AddLog("\n[1/3] 删除 .git 文件夹...");
                var success = await _gitService.DeleteGitFolderAsync();
                
                if (!success)
                {
                    AddLog("✗ 删除 .git 文件夹失败");
                    MessageBox.Show("删除 .git 文件夹失败，请检查文件夹权限", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AddLog("\n[2/3] 重新初始化 Git 仓库...");
                var (success2, _) = await _gitService.ExecuteGitCommandAsync("init");
                
                if (!success2)
                {
                    AddLog("✗ 初始化 Git 仓库失败");
                    MessageBox.Show("初始化 Git 仓库失败", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AddLog("\n[3/3] 配置用户信息...");
                await _gitService.ExecuteGitCommandAsync($"config user.name \"{Settings.GitHubUser}\"");
                await _gitService.ExecuteGitCommandAsync($"config user.email \"{Settings.GitHubUser}@users.noreply.github.com\"");

                AddLog("\n========================================");
                AddLog("  ✓ 历史初始化成功！");
                AddLog("  本地 Git 历史已重置");
                AddLog("  本地文件保持不变");
                AddLog("  提示: 现在可以重新提交并推送到 GitHub");
                AddLog("========================================");

                MessageBox.Show(
                    "历史初始化成功！\n\n" +
                    "• 本地 Git 历史已重置\n" +
                    "• 本地文件保持不变\n" +
                    "• 远程仓库不受影响\n\n" +
                    "⚠️ 重要提示：\n" +
                    "如果远程仓库已有内容，请使用「初始推送」中的\n" +
                    "「强制推送」来覆盖远程历史。\n\n" +
                    "❌ 不要使用「更新推送」，会因为历史不匹配而失败！\n\n" +
                    "下一步：\n" +
                    "1. 前往「初始推送」页面\n" +
                    "2. 选择「强制推送」（如果远程有内容）\n" +
                    "3. 或选择「常规推送」（如果远程是空的）", 
                    "完成", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"\n✗ 操作失败: {ex.Message}");
                MessageBox.Show($"操作失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CleanGitHubAsync()
        {
            var result = MessageBox.Show(
                "⚠️ 警告：此操作将彻底清空 GitHub 仓库！\n\n" +
                "操作说明：\n" +
                "• 删除 GitHub 上的所有分支\n" +
                "• 删除所有提交记录和历史\n" +
                "• 仓库将变为完全空的状态\n" +
                "• 本地文件完全不受影响\n\n" +
                "⚠️ GitHub 上的所有内容将永久丢失！\n" +
                "⚠️ 如需备份，请先手动复制本地文件夹！\n\n" +
                "确定要继续吗？",
                "⚠️ 确认清空 GitHub 仓库",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // 验证码验证
            var verificationDialog = new Views.VerificationDialog
            {
                Owner = Application.Current.MainWindow
            };

            var dialogResult = verificationDialog.ShowDialog();
            if (dialogResult != true || !verificationDialog.IsVerified)
            {
                AddLog("✗ 验证失败，操作已取消");
                return;
            }

            LogMessages.Clear();
            AddLog("========================================");
            AddLog("  🧹 清空 GitHub 仓库");
            AddLog("========================================");

            InitializeGitService();

            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"git_clean_{DateTime.Now:yyyyMMddHHmmss}");

            try
            {
                AddLog("\n[1/6] 创建临时工作目录...");
                if (System.IO.Directory.Exists(tempDir))
                {
                    await DeleteDirectoryAsync(tempDir);
                }
                System.IO.Directory.CreateDirectory(tempDir);
                AddLog($"临时目录: {tempDir}");

                AddLog("\n[2/6] 初始化新的空仓库...");
                _gitService.Initialize(tempDir, Settings.GitHubToken, Settings.RepoUrl);
                await _gitService.ExecuteGitCommandAsync("init");

                AddLog("\n[3/6] 配置用户信息...");
                await _gitService.ExecuteGitCommandAsync($"config user.name \"{Settings.GitHubUser}\"");
                await _gitService.ExecuteGitCommandAsync($"config user.email \"{Settings.GitHubUser}@users.noreply.github.com\"");

                AddLog("\n[4/9] 添加远程仓库...");
                await _gitService.ExecuteGitCommandAsync($"remote add origin {_gitService.GetRemoteUrl()}");

                AddLog("\n[5/9] 获取所有远程引用...");
                var (success1, allRefs) = await _gitService.ExecuteGitCommandAsync("ls-remote origin");
                
                if (success1 && !string.IsNullOrWhiteSpace(allRefs))
                {
                    var refLines = allRefs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var refsToDelete = new System.Collections.Generic.List<string>();
                    
                    foreach (var line in refLines)
                    {
                        var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var refName = parts[1];
                            // 收集所有需要删除的引用（分支、标签等）
                            if (refName.StartsWith("refs/heads/") || refName.StartsWith("refs/tags/"))
                            {
                                refsToDelete.Add(refName);
                            }
                        }
                    }
                    
                    if (refsToDelete.Count > 0)
                    {
                        AddLog($"\n[6/9] 删除所有远程分支和标签（共 {refsToDelete.Count} 个）...");
                        foreach (var refName in refsToDelete)
                        {
                            var displayName = refName.Replace("refs/heads/", "").Replace("refs/tags/", "");
                            var type = refName.StartsWith("refs/heads/") ? "分支" : "标签";
                            AddLog($"  删除{type}: {displayName}");
                            await _gitService.ExecuteGitCommandAsync($"push origin --delete {refName}");
                        }
                    }
                    else
                    {
                        AddLog("\n[6/9] 远程仓库已经是空的");
                    }
                }
                else
                {
                    AddLog("\n[6/9] 远程仓库已经是空的");
                }

                AddLog("\n[7/9] 创建临时分支并立即删除（清理缓存）...");
                await _gitService.ExecuteGitCommandAsync("checkout -b temp-clean-branch");
                await _gitService.ExecuteGitCommandAsync("commit --allow-empty -m 'temp'");
                await _gitService.ExecuteGitCommandAsync("push origin temp-clean-branch");
                await _gitService.ExecuteGitCommandAsync("push origin --delete temp-clean-branch");

                AddLog("\n[8/9] 清理本地引用...");
                await _gitService.ExecuteGitCommandAsync("gc --prune=now --aggressive");

                AddLog("\n[9/9] 清理临时目录...");
                await DeleteDirectoryAsync(tempDir);

                AddLog("\n========================================");
                AddLog("  ✓ GitHub 仓库已完全清空！");
                AddLog("  所有分支、标签和提交记录已删除");
                AddLog("  本地文件保持不变");
                AddLog("  提示: GitHub 页面可能需要几分钟才能更新");
                AddLog("========================================");
                MessageBox.Show(
                    "GitHub 仓库已完全清空！\n\n" +
                    "• 所有分支和标签已删除\n" +
                    "• 所有提交记录已删除\n" +
                    "• 本地文件保持不变\n\n" +
                    "注意: GitHub 页面可能需要几分钟才能完全更新，\n" +
                    "如果仍显示旧内容，请稍后刷新页面。", 
                    "完成", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"\n✗ 操作失败: {ex.Message}");
                if (System.IO.Directory.Exists(tempDir))
                {
                    try { await DeleteDirectoryAsync(tempDir); } catch { }
                }
            }
            finally
            {
                // 恢复到原始工作目录
                _gitService.Initialize(Settings.LocalFolder, Settings.GitHubToken, Settings.RepoUrl);
            }
        }

        private async Task DeleteDirectoryAsync(string path)
        {
            await Task.Run(() =>
            {
                if (!System.IO.Directory.Exists(path))
                    return;

                try
                {
                    // 移除所有文件和子目录的只读属性
                    var directory = new System.IO.DirectoryInfo(path);
                    foreach (var file in directory.GetFiles("*", System.IO.SearchOption.AllDirectories))
                    {
                        file.Attributes = System.IO.FileAttributes.Normal;
                    }
                    foreach (var dir in directory.GetDirectories("*", System.IO.SearchOption.AllDirectories))
                    {
                        dir.Attributes = System.IO.FileAttributes.Normal;
                    }

                    // 删除目录
                    System.IO.Directory.Delete(path, true);
                    AddLog("✓ 临时目录已删除");
                }
                catch (Exception ex)
                {
                    AddLog($"⚠ 清理临时目录失败: {ex.Message}");
                }
            });
        }

        private void SaveSettings()
        {
            try
            {
                // 保存前更新历史记录
                UpdateLocalFolderHistory();
                _settingsService.SaveSettings(Settings);
                UpdateWindowTitle();
                AddLog("✓ 设置已保存");
                MessageBox.Show("设置已保存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"✗ 保存设置失败：{ex.Message}");
                MessageBox.Show($"保存设置失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SaveSettingsSilently()
        {
            try
            {
                // 保存前更新历史记录
                UpdateLocalFolderHistory();
                _settingsService.SaveSettings(Settings);
                UpdateWindowTitle();
            }
            catch
            {
                // 静默失败，不显示错误
            }
        }

        private void UpdateWindowTitle()
        {
            var folderName = GetFolderName();
            var repoName = GetRepoName();
            
            // 如果都没有，显示默认标题
            if (string.IsNullOrEmpty(folderName) && string.IsNullOrEmpty(repoName))
            {
                WindowTitle = "Git Tools WPF";
                return;
            }
            
            // 如果只有文件夹名
            if (string.IsNullOrEmpty(repoName))
            {
                WindowTitle = folderName;
                return;
            }
            
            // 如果只有仓库名
            if (string.IsNullOrEmpty(folderName))
            {
                WindowTitle = repoName;
                return;
            }
            
            // 如果都有，显示：文件夹名 (user/repo)
            WindowTitle = $"{folderName} ({repoName})";
        }

        private string GetFolderName()
        {
            if (!string.IsNullOrEmpty(Settings.LocalFolder))
            {
                try
                {
                    return System.IO.Path.GetFileName(Settings.LocalFolder.TrimEnd('\\', '/'));
                }
                catch
                {
                    // 如果路径无效，忽略错误
                }
            }
            
            return string.Empty;
        }

        private string GetRepoName()
        {
            if (!string.IsNullOrEmpty(Settings.RepoUrl))
            {
                return ExtractRepoNameFromUrl(Settings.RepoUrl);
            }
            
            return string.Empty;
        }

        private string ExtractRepoNameFromUrl(string repoUrl)
        {
            try
            {
                // 移除 .git 后缀
                var url = repoUrl.TrimEnd('/');
                if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(0, url.Length - 4);
                }
                
                // 提取 user/repo 格式
                if (url.Contains("://"))
                {
                    // HTTPS 格式: https://github.com/user/repo
                    var uri = new Uri(url);
                    var segments = uri.AbsolutePath.Trim('/').Split('/');
                    if (segments.Length >= 2)
                    {
                        return $"{segments[segments.Length - 2]}/{segments[segments.Length - 1]}";
                    }
                }
                else if (url.Contains(":"))
                {
                    // SSH 格式: git@github.com:user/repo
                    var parts = url.Split(':');
                    if (parts.Length >= 2)
                    {
                        var path = parts[parts.Length - 1].Trim('/');
                        return path; // 已经是 user/repo 格式
                    }
                }
                
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void UpdateLocalFolderHistory()
        {
            if (string.IsNullOrWhiteSpace(Settings.LocalFolder))
                return;

            // 移除已存在的相同路径
            Settings.LocalFolderHistory.Remove(Settings.LocalFolder);

            // 添加到列表开头
            Settings.LocalFolderHistory.Insert(0, Settings.LocalFolder);

            // 只保留最近10个
            if (Settings.LocalFolderHistory.Count > 10)
            {
                Settings.LocalFolderHistory.RemoveRange(10, Settings.LocalFolderHistory.Count - 10);
            }
        }

        private void ChangeTheme(string? theme)
        {
            if (theme == null) return;
            
            Settings.Theme = theme;
            
            // 停止或启动系统主题监听
            if (theme == "System")
            {
                StartSystemThemeMonitoring();
            }
            else
            {
                StopSystemThemeMonitoring();
            }
            
            ApplyTheme();
            _settingsService.SaveSettings(Settings);
        }

        private void ApplyTheme()
        {
            string themeToApply = Settings.Theme;

            if (Settings.Theme == "System")
            {
                themeToApply = IsSystemDarkMode() ? "Dark" : "Light";
            }

            var uri = themeToApply switch
            {
                "Dark" => new Uri("Themes/DarkTheme.xaml", UriKind.Relative),
                _ => new Uri("Themes/LightTheme.xaml", UriKind.Relative)
            };

            var themeDictionaries = Application.Current.Resources.MergedDictionaries
                .Where(d => d.Source?.OriginalString?.Contains("Themes/") == true)
                .ToList();

            foreach (var dict in themeDictionaries)
            {
                Application.Current.Resources.MergedDictionaries.Remove(dict);
            }

            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
            
            // 通知主题已变化
            ThemeChanged?.Invoke();
        }

        private bool IsSystemDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                
                if (key?.GetValue("AppsUseLightTheme") is int intValue)
                {
                    return intValue == 0;
                }
            }
            catch { }
            
            return false;
        }

        private void StartSystemThemeMonitoring()
        {
            if (Settings.Theme != "System")
                return;

            // 使用定时器每秒检查一次系统主题
            _themeMonitorTimer = new System.Threading.Timer(_ =>
            {
                if (Settings.Theme == "System")
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var currentTheme = IsSystemDarkMode() ? "Dark" : "Light";
                        var appliedTheme = GetCurrentAppliedTheme();
                        
                        if (currentTheme != appliedTheme)
                        {
                            ApplyTheme();
                        }
                    });
                }
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void StopSystemThemeMonitoring()
        {
            _themeMonitorTimer?.Dispose();
            _themeMonitorTimer = null;
        }

        private string GetCurrentAppliedTheme()
        {
            var themeDictionary = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString?.Contains("Themes/") == true);
            
            if (themeDictionary?.Source?.OriginalString?.Contains("DarkTheme") == true)
                return "Dark";
            
            return "Light";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
