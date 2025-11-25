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

namespace GitToolsWPF.ViewModels
{
    public class CommitHistoryViewModel : INotifyPropertyChanged
    {
        private readonly GitService _gitService;
        private readonly string _workingDirectory;
        private readonly string _githubToken;
        private readonly string _repoUrl;
        
        private string _currentBranchName = "";
        private bool _isDetachedHead = false;
        private string _selectedBranchFilter = "全部分支";
        private string _statusMessage = "";
        private string _commitCountText = "";

        // 内存缓存（程序运行期间一直有效，直到手动清除）
        private static System.Collections.Generic.List<CommitInfo>? _cachedCommits = null;
        private static string _cachedWorkingDirectory = "";

        public ObservableCollection<CommitInfo> CommitHistory { get; } = new();
        public ObservableCollection<string> BranchFilterOptions { get; } = new() 
        { 
            "全部分支", 
            "本地分支", 
            "远程分支" 
        };

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
                _ = LoadCommitHistoryAsync();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string CommitCountText
        {
            get => _commitCountText;
            set { _commitCountText = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand SwitchToCommitCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<string>? OnLog;
        public event Action? OnCommitSwitched;
        
        /// <summary>
        /// 清除缓存（在 Git 状态改变后调用）
        /// </summary>
        public static void ClearCache()
        {
            _cachedCommits = null;
            _cachedWorkingDirectory = "";
        }

        public CommitHistoryViewModel(GitService gitService, string workingDir, string token, string repoUrl)
        {
            _gitService = gitService;
            _workingDirectory = workingDir;
            _githubToken = token;
            _repoUrl = repoUrl;

            RefreshCommand = new RelayCommand(async () => await LoadCommitHistoryAsync());
            SwitchToCommitCommand = new RelayCommand<CommitInfo>(async (commit) => await SwitchToCommitAsync(commit));

            // 初始加载
            _ = LoadCommitHistoryAsync();
        }

        private async Task LoadCommitHistoryAsync()
        {
            try
            {
                _gitService.Initialize(_workingDirectory, _githubToken, _repoUrl);
                
                // 获取当前分支信息
                var (_, branchOutput) = await _gitService.ExecuteGitCommandAsync("branch --show-current");
                var currentBranch = branchOutput?.Trim() ?? "";
                
                // 检查是否分离HEAD
                var isDetached = string.IsNullOrEmpty(currentBranch);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentBranchName = isDetached ? "分离HEAD" : currentBranch;
                    IsDetachedHead = isDetached;
                    StatusMessage = isDetached ? "当前不在任何分支上" : $"在分支 {currentBranch} 上";
                });
                
                // 获取当前HEAD的哈希
                var (_, currentHashOutput) = await _gitService.ExecuteGitCommandAsync("rev-parse HEAD");
                var currentHash = currentHashOutput?.Trim() ?? "";
                
                // 检查缓存是否有效（同一工作目录且缓存存在）
                var cacheValid = _cachedCommits != null &&
                                _cachedWorkingDirectory == _workingDirectory;
                
                if (cacheValid)
                {
                    OnLog?.Invoke("📦 使用缓存数据（程序运行期间有效）");
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CommitHistory.Clear();
                        
                        // 从缓存加载，但需要更新 IsCurrent 状态
                        foreach (var commit in _cachedCommits!)
                        {
                            commit.IsCurrent = commit.Hash.StartsWith(currentHash) || currentHash.StartsWith(commit.Hash);
                            CommitHistory.Add(commit);
                        }
                        
                        CommitCountText = $"共 {CommitHistory.Count} 个提交（来自缓存）";
                        OnLog?.Invoke($"✓ 已从缓存加载 {CommitHistory.Count} 个提交");
                    });
                    
                    return;
                }
                
                OnLog?.Invoke("正在获取提交历史...");
                
                // 获取远程更新
                await _gitService.ExecuteGitCommandAsync("fetch origin");
                
                // 根据筛选条件决定显示哪些分支
                string logCommand = SelectedBranchFilter switch
                {
                    "本地分支" => "log --branches --graph --pretty=format:\"%H|%h|%s|%an|%ar|%d|%P\" -50",
                    "远程分支" => "log --remotes --graph --pretty=format:\"%H|%h|%s|%an|%ar|%d|%P\" -50",
                    _ => "log --all --graph --pretty=format:\"%H|%h|%s|%an|%ar|%d|%P\" -50"
                };
                
                // 获取详细的提交历史（带图形和分支信息，包含父提交）
                var (success, output) = await _gitService.ExecuteGitCommandAsync(logCommand);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CommitHistory.Clear();
                    
                    if (success && !string.IsNullOrWhiteSpace(output))
                    {
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        var tempCommits = new System.Collections.Generic.List<CommitInfo>();
                        
                        foreach (var line in lines)
                        {
                            // 分离图形符号和提交信息
                            var graphEnd = line.LastIndexOf('*');
                            if (graphEnd == -1) continue;
                            
                            var graphSymbols = line.Substring(0, graphEnd + 1);
                            var commitData = line.Substring(graphEnd + 1).Trim();
                            
                            var parts = commitData.Split('|');
                            if (parts.Length >= 5)
                            {
                                var hash = parts[0];
                                var branches = parts.Length > 5 ? parts[5].Trim() : "";
                                var parentHashes = parts.Length > 6 ? parts[6].Trim() : "";
                                
                                // 清理分支信息（移除括号）
                                if (branches.StartsWith("(") && branches.EndsWith(")"))
                                {
                                    branches = branches.Substring(1, branches.Length - 2).Trim();
                                }
                                
                                // 解析分支信息
                                var isHead = branches.Contains("HEAD");
                                var hasLocalBranch = false;
                                var hasRemoteBranch = false;
                                
                                if (!string.IsNullOrEmpty(branches))
                                {
                                    var branchParts = branches.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var branch in branchParts)
                                    {
                                        var trimmedBranch = branch.Trim();
                                        if (trimmedBranch.StartsWith("origin/") || trimmedBranch.Contains("origin/"))
                                        {
                                            hasRemoteBranch = true;
                                        }
                                        else if (!trimmedBranch.StartsWith("HEAD") && !trimmedBranch.Contains("->"))
                                        {
                                            hasLocalBranch = true;
                                        }
                                    }
                                }
                                
                                // 判断是否是主线（简单判断：只有 * 或 * 后面没有分支符号）
                                var isMainLine = graphSymbols.Trim() == "*" || 
                                               (!graphSymbols.Contains("|") && !graphSymbols.Contains("/") && !graphSymbols.Contains("\\"));
                                
                                // 获取第一个父提交（用于版本编号计算）
                                var parentHash = "";
                                if (!string.IsNullOrEmpty(parentHashes))
                                {
                                    var parents = parentHashes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parents.Length > 0)
                                    {
                                        parentHash = parents[0];
                                    }
                                }
                                
                                var commit = new CommitInfo
                                {
                                    Hash = hash,
                                    ShortHash = parts[1],
                                    Message = parts[2],
                                    Author = parts[3],
                                    Date = parts[4],
                                    GraphSymbols = graphSymbols,
                                    Branches = branches,
                                    IsCurrent = hash.StartsWith(currentHash) || currentHash.StartsWith(hash),
                                    IsHead = isHead,
                                    IsLocalBranch = hasLocalBranch,
                                    IsRemoteBranch = hasRemoteBranch,
                                    IsMainLine = isMainLine,
                                    ParentHash = parentHash
                                };
                                tempCommits.Add(commit);
                            }
                        }
                        
                        // 计算版本编号
                        CalculateVersionNumbers(tempCommits);
                        
                        // 保存到缓存（程序运行期间一直有效）
                        _cachedCommits = new System.Collections.Generic.List<CommitInfo>(tempCommits);
                        _cachedWorkingDirectory = _workingDirectory;
                        
                        // 添加到显示列表
                        foreach (var commit in tempCommits)
                        {
                            CommitHistory.Add(commit);
                        }
                        
                        CommitCountText = $"共 {CommitHistory.Count} 个提交";
                        OnLog?.Invoke($"✓ 已加载 {CommitHistory.Count} 个提交（已缓存）");
                    }
                    else
                    {
                        CommitCountText = "无提交记录";
                        OnLog?.Invoke("✗ 获取提交历史失败");
                    }
                });
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"✗ 错误：{ex.Message}");
            }
        }

        private async Task SwitchToCommitAsync(CommitInfo? commit)
        {
            if (commit == null) return;
            
            // 检查是否已经是当前版本
            if (commit.IsCurrent)
            {
                MessageBox.Show(
                    "已经是当前版本！",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            
            // 智能判断：检查是否是分支的最新提交
            var isLatestCommit = await IsLatestCommitOnBranchAsync(commit);
            
            string confirmMessage;
            string successMessage;
            string checkoutCommand;
            
            if (isLatestCommit)
            {
                // 这是分支的最新提交，应该切换回分支而不是进入游离状态
                var branchName = await GetBranchNameForCommitAsync(commit);
                
                confirmMessage = $"确定要切换到分支 {branchName} 吗？\n\n" +
                    $"版本：{commit.ShortHash}\n" +
                    $"信息：{commit.Message}\n" +
                    $"作者：{commit.Author}\n" +
                    $"时间：{commit.Date}\n\n" +
                    "✓ 这是分支的最新提交\n" +
                    $"• 将切换到分支 {branchName}\n" +
                    "• 保持正常的分支状态\n" +
                    "• 可以正常提交和推送";
                
                successMessage = $"✓ 切换成功！\n\n" +
                    $"版本：{commit.ShortHash}\n" +
                    $"信息：{commit.Message}\n\n" +
                    $"当前在分支：{branchName}\n" +
                    "可以正常进行开发和提交";
                
                checkoutCommand = branchName;
            }
            else
            {
                // 这是历史提交，会进入游离状态
                confirmMessage = $"确定要切换到此版本吗？\n\n" +
                    $"版本：{commit.ShortHash}\n" +
                    $"信息：{commit.Message}\n" +
                    $"作者：{commit.Author}\n" +
                    $"时间：{commit.Date}\n\n" +
                    "⚠️ 注意：\n" +
                    "• 切换后将处于「分离HEAD」状态\n" +
                    "• 可以查看和运行历史代码\n" +
                    "• 不建议在此状态下提交更改";
                
                successMessage = $"✓ 切换成功！\n\n" +
                    $"版本：{commit.ShortHash}\n" +
                    $"信息：{commit.Message}\n\n" +
                    "当前处于「分离HEAD」状态\n" +
                    "返回最新版本：git checkout main";
                
                checkoutCommand = commit.Hash;
            }
            
            var result = MessageBox.Show(
                confirmMessage,
                "确认切换版本",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;
            
            try
            {
                OnLog?.Invoke($"正在切换到版本 {commit.ShortHash}...");
                
                // 检查工作区状态
                var (_, statusOutput) = await _gitService.ExecuteGitCommandAsync("status --short");
                
                if (!string.IsNullOrWhiteSpace(statusOutput))
                {
                    var stashResult = MessageBox.Show(
                        "⚠️ 工作区有未提交的更改\n\n" +
                        "是否暂存当前更改后切换？\n\n" +
                        "• 点击「是」- 暂存更改并切换\n" +
                        "• 点击「否」- 取消操作",
                        "未提交的更改",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (stashResult == MessageBoxResult.Yes)
                    {
                        await _gitService.ExecuteGitCommandAsync("stash push -m \"切换版本前自动暂存\"");
                        OnLog?.Invoke("✓ 更改已暂存");
                    }
                    else
                    {
                        OnLog?.Invoke("✗ 操作已取消");
                        return;
                    }
                }
                
                // 切换到指定提交或分支
                var (checkoutSuccess, checkoutOutput) = await _gitService.ExecuteGitCommandAsync($"checkout {checkoutCommand}");
                
                if (checkoutSuccess)
                {
                    OnLog?.Invoke($"✓ 已切换到版本 {commit.ShortHash}");
                    
                    // 清除缓存（因为 Git 状态已改变）
                    ClearCache();
                    
                    MessageBox.Show(
                        successMessage,
                        "切换成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    // 通知主窗口刷新
                    OnCommitSwitched?.Invoke();
                    
                    // 重新加载历史
                    await LoadCommitHistoryAsync();
                }
                else
                {
                    OnLog?.Invoke($"✗ 切换失败：{checkoutOutput}");
                    MessageBox.Show(
                        $"切换失败！\n\n{checkoutOutput}",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"✗ 错误：{ex.Message}");
                MessageBox.Show(
                    $"发生错误：\n{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 检查提交是否是某个分支的最新提交
        /// </summary>
        private async Task<bool> IsLatestCommitOnBranchAsync(CommitInfo commit)
        {
            try
            {
                // 获取所有分支及其最新提交
                var (success, output) = await _gitService.ExecuteGitCommandAsync("branch -a -v --no-abbrev");
                
                if (!success || string.IsNullOrWhiteSpace(output))
                    return false;
                
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    // 格式: * main abc1234 commit message
                    // 或:   remotes/origin/main abc1234 commit message
                    var parts = line.Trim().TrimStart('*').Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length >= 2)
                    {
                        var branchCommitHash = parts[1];
                        
                        // 检查提交哈希是否匹配
                        if (commit.Hash.StartsWith(branchCommitHash) || branchCommitHash.StartsWith(commit.Hash))
                        {
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 获取提交所在的分支名（优先返回本地分支）
        /// </summary>
        private async Task<string> GetBranchNameForCommitAsync(CommitInfo commit)
        {
            try
            {
                // 获取所有分支及其最新提交
                var (success, output) = await _gitService.ExecuteGitCommandAsync("branch -a -v --no-abbrev");
                
                if (!success || string.IsNullOrWhiteSpace(output))
                    return "main"; // 默认返回 main
                
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string? localBranch = null;
                string? remoteBranch = null;
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim().TrimStart('*').Trim();
                    var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length >= 2)
                    {
                        var branchName = parts[0];
                        var branchCommitHash = parts[1];
                        
                        // 检查提交哈希是否匹配
                        if (commit.Hash.StartsWith(branchCommitHash) || branchCommitHash.StartsWith(commit.Hash))
                        {
                            // 优先记录本地分支
                            if (!branchName.StartsWith("remotes/"))
                            {
                                localBranch = branchName;
                            }
                            else if (remoteBranch == null)
                            {
                                // 记录远程分支（去掉 remotes/origin/ 前缀）
                                remoteBranch = branchName.Replace("remotes/origin/", "");
                            }
                        }
                    }
                }
                
                // 优先返回本地分支，其次返回远程分支
                return localBranch ?? remoteBranch ?? "main";
            }
            catch
            {
                return "main"; // 出错时返回默认分支
            }
        }

        private void CalculateVersionNumbers(System.Collections.Generic.List<CommitInfo> commits)
        {
            if (commits.Count == 0) return;
            
            // 反转列表，从最早的提交开始编号
            var reversedCommits = new System.Collections.Generic.List<CommitInfo>(commits);
            reversedCommits.Reverse();
            
            // 创建哈希到提交的映射
            var commitMap = new System.Collections.Generic.Dictionary<string, CommitInfo>();
            foreach (var commit in reversedCommits)
            {
                commitMap[commit.Hash] = commit;
            }
            
            // 记录每个提交的子提交数量（用于检测分支点）
            var childrenCount = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var commit in reversedCommits)
            {
                if (!string.IsNullOrEmpty(commit.ParentHash))
                {
                    if (!childrenCount.ContainsKey(commit.ParentHash))
                    {
                        childrenCount[commit.ParentHash] = 0;
                    }
                    childrenCount[commit.ParentHash]++;
                }
            }
            
            // 主线编号计数器
            int mainLineCounter = 1;
            
            // 记录每个分支点的分支计数器 (父提交哈希 -> 分支字母索引)
            var branchCounters = new System.Collections.Generic.Dictionary<string, int>();
            
            // 记录已经分配编号的提交
            var numberedCommits = new System.Collections.Generic.HashSet<string>();
            
            // 第一遍：为主线提交分配数字编号
            foreach (var commit in reversedCommits)
            {
                if (commit.IsMainLine)
                {
                    commit.MainLineNumber = mainLineCounter;
                    commit.VersionNumber = mainLineCounter.ToString();
                    numberedCommits.Add(commit.Hash);
                    mainLineCounter++;
                }
            }
            
            // 第二遍：为分支提交分配编号
            foreach (var commit in reversedCommits)
            {
                if (!commit.IsMainLine && !numberedCommits.Contains(commit.Hash))
                {
                    // 这是一个分支提交
                    commit.IsBranchCommit = true;
                    
                    // 找到父提交
                    if (!string.IsNullOrEmpty(commit.ParentHash) && commitMap.ContainsKey(commit.ParentHash))
                    {
                        var parent = commitMap[commit.ParentHash];
                        
                        // 使用父提交的主线编号
                        if (parent.MainLineNumber > 0)
                        {
                            commit.MainLineNumber = parent.MainLineNumber;
                            
                            // 获取或初始化该分支点的分支计数器
                            if (!branchCounters.ContainsKey(commit.ParentHash))
                            {
                                branchCounters[commit.ParentHash] = 0;
                            }
                            
                            // 分配字母后缀 (A, B, C, ...)
                            var branchIndex = branchCounters[commit.ParentHash];
                            commit.BranchSuffix = GetBranchSuffix(branchIndex);
                            commit.VersionNumber = $"{commit.MainLineNumber}{commit.BranchSuffix}";
                            
                            branchCounters[commit.ParentHash]++;
                            numberedCommits.Add(commit.Hash);
                        }
                    }
                    
                    // 如果还没有编号（可能是孤立的分支），使用默认编号
                    if (string.IsNullOrEmpty(commit.VersionNumber))
                    {
                        commit.VersionNumber = "?";
                    }
                }
            }
        }
        
        private string GetBranchSuffix(int index)
        {
            // 将索引转换为字母 (0->A, 1->B, ..., 25->Z, 26->AA, 27->AB, ...)
            if (index < 26)
            {
                return ((char)('A' + index)).ToString();
            }
            else
            {
                // 超过 Z 后使用 AA, AB, AC...
                int first = index / 26 - 1;
                int second = index % 26;
                return $"{(char)('A' + first)}{(char)('A' + second)}";
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
