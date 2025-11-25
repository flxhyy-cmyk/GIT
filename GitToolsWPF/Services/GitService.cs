using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GitToolsWPF.Services
{
    public class GitService
    {
        public event Action<string>? OnOutput;
        private string _workingDirectory = "";
        private string _githubToken = "";
        private string _repoUrl = "";

        public void Initialize(string workingDir, string token, string repoUrl)
        {
            _workingDirectory = workingDir;
            _githubToken = token;
            _repoUrl = repoUrl;
        }

        public async Task<(bool success, string output)> ExecuteGitCommandAsync(string arguments)
        {
            return await ExecuteCommandAsync("git", arguments);
        }

        public async Task<(bool success, string output)> ExecuteCommandAsync(string fileName, string arguments)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = _workingDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var process = new Process { StartInfo = processInfo };
                    var output = new StringBuilder();
                    var hasOutput = false;

                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            hasOutput = true;
                            output.AppendLine(e.Data);
                            OnOutput?.Invoke(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            hasOutput = true;
                            output.AppendLine(e.Data);
                            OnOutput?.Invoke(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    if (!hasOutput && process.ExitCode == 0)
                    {
                        OnOutput?.Invoke("✓ 操作成功");
                    }

                    return (process.ExitCode == 0, output.ToString());
                }
                catch (Exception ex)
                {
                    var error = $"[错误] {ex.Message}";
                    OnOutput?.Invoke(error);
                    return (false, error);
                }
            });
        }

        public string GetRemoteUrl()
        {
            if (string.IsNullOrEmpty(_githubToken) || _repoUrl.Contains("@"))
            {
                return _repoUrl;
            }

            if (_repoUrl.StartsWith("https://"))
            {
                return _repoUrl.Replace("https://", $"https://{_githubToken}@");
            }

            return _repoUrl;
        }

        public async Task<bool> DeleteAllFilesExceptGitAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var directory = new DirectoryInfo(_workingDirectory);
                    
                    foreach (var file in directory.GetFiles())
                    {
                        file.Delete();
                    }
                    
                    foreach (var dir in directory.GetDirectories())
                    {
                        if (dir.Name != ".git")
                        {
                            dir.Delete(true);
                        }
                    }
                    
                    OnOutput?.Invoke("✓ 文件删除完成");
                    return true;
                }
                catch (Exception ex)
                {
                    OnOutput?.Invoke($"[错误] 删除文件失败：{ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> DeleteGitFolderAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var gitPath = Path.Combine(_workingDirectory, ".git");
                    if (!Directory.Exists(gitPath))
                    {
                        OnOutput?.Invoke("ℹ .git 文件夹不存在");
                        return true;
                    }

                    OnOutput?.Invoke("正在删除 .git 文件夹...");
                    
                    // 方法1：尝试使用 .NET API 删除
                    try
                    {
                        // 递归移除所有文件和文件夹的只读属性
                        RemoveReadOnlyAttribute(gitPath);
                        
                        // 尝试删除
                        Directory.Delete(gitPath, true);
                        OnOutput?.Invoke("✓ .git 文件夹已删除");
                        return true;
                    }
                    catch
                    {
                        OnOutput?.Invoke("⚠ 常规删除失败，尝试使用命令行强制删除...");
                    }
                    
                    // 方法2：使用 Windows 命令行强制删除
                    try
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c rmdir /s /q \"{gitPath}\"",
                            WorkingDirectory = _workingDirectory,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var process = new Process { StartInfo = processInfo };
                        process.Start();
                        process.WaitForExit();

                        // 检查是否删除成功
                        if (!Directory.Exists(gitPath))
                        {
                            OnOutput?.Invoke("✓ .git 文件夹已删除（使用命令行）");
                            return true;
                        }
                    }
                    catch
                    {
                        // 继续尝试下一个方法
                    }
                    
                    // 方法3：使用 PowerShell 强制删除
                    try
                    {
                        OnOutput?.Invoke("⚠ 命令行删除失败，尝试使用 PowerShell...");
                        
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-Command \"Remove-Item -Path '{gitPath}' -Recurse -Force\"",
                            WorkingDirectory = _workingDirectory,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var process = new Process { StartInfo = processInfo };
                        process.Start();
                        process.WaitForExit();

                        // 检查是否删除成功
                        if (!Directory.Exists(gitPath))
                        {
                            OnOutput?.Invoke("✓ .git 文件夹已删除（使用 PowerShell）");
                            return true;
                        }
                    }
                    catch
                    {
                        // 所有方法都失败
                    }
                    
                    OnOutput?.Invoke("✗ 所有删除方法都失败");
                    OnOutput?.Invoke("提示：请尝试以下操作：");
                    OnOutput?.Invoke("  1. 关闭所有 Git 相关程序（Git Bash、TortoiseGit 等）");
                    OnOutput?.Invoke("  2. 以管理员身份运行本程序");
                    OnOutput?.Invoke("  3. 手动删除 .git 文件夹");
                    return false;
                }
                catch (UnauthorizedAccessException ex)
                {
                    OnOutput?.Invoke($"[错误] 权限不足：{ex.Message}");
                    OnOutput?.Invoke("提示：请尝试以管理员身份运行程序");
                    return false;
                }
                catch (IOException ex)
                {
                    OnOutput?.Invoke($"[错误] 文件被占用：{ex.Message}");
                    OnOutput?.Invoke("提示：请关闭所有 Git 相关程序（如 Git Bash、TortoiseGit 等）");
                    return false;
                }
                catch (Exception ex)
                {
                    OnOutput?.Invoke($"[错误] 删除 .git 文件夹失败：{ex.Message}");
                    return false;
                }
            });
        }

        private void RemoveReadOnlyAttribute(string path)
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);
                
                // 移除目录本身的只读属性
                if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                }
                
                // 递归处理所有文件
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            file.Attributes &= ~FileAttributes.ReadOnly;
                        }
                    }
                    catch
                    {
                        // 忽略单个文件的错误，继续处理其他文件
                    }
                }
                
                // 递归处理所有子目录
                foreach (var dir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if ((dir.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            dir.Attributes &= ~FileAttributes.ReadOnly;
                        }
                    }
                    catch
                    {
                        // 忽略单个目录的错误，继续处理其他目录
                    }
                }
            }
            catch (Exception ex)
            {
                OnOutput?.Invoke($"⚠ 移除只读属性时出错：{ex.Message}");
            }
        }

        public async Task CopyDirectoryAsync(string sourceDir, string targetDir)
        {
            await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(targetDir);

                    // 复制所有文件
                    foreach (var file in Directory.GetFiles(sourceDir))
                    {
                        var fileName = Path.GetFileName(file);
                        var destFile = Path.Combine(targetDir, fileName);
                        File.Copy(file, destFile, true);
                    }

                    // 递归复制所有子目录
                    foreach (var dir in Directory.GetDirectories(sourceDir))
                    {
                        var dirName = Path.GetFileName(dir);
                        var destDir = Path.Combine(targetDir, dirName);
                        CopyDirectoryAsync(dir, destDir).Wait();
                    }

                    OnOutput?.Invoke("✓ 目录复制完成");
                }
                catch (Exception ex)
                {
                    OnOutput?.Invoke($"[错误] 复制目录失败：{ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// 检查是否处于游离 HEAD 状态
        /// </summary>
        public async Task<(bool isDetached, string currentHash, string commitMessage)> CheckDetachedHeadAsync()
        {
            try
            {
                // 检查当前分支
                var (_, branchOutput) = await ExecuteGitCommandAsync("branch --show-current");
                var currentBranch = branchOutput?.Trim() ?? "";
                
                // 如果没有当前分支，说明是游离 HEAD 状态
                var isDetached = string.IsNullOrEmpty(currentBranch);
                
                if (isDetached)
                {
                    // 获取当前提交的哈希
                    var (_, hashOutput) = await ExecuteGitCommandAsync("rev-parse --short HEAD");
                    var currentHash = hashOutput?.Trim() ?? "";
                    
                    // 获取当前提交的消息
                    var (_, messageOutput) = await ExecuteGitCommandAsync("log -1 --pretty=format:\"%s\"");
                    var commitMessage = messageOutput?.Trim().Trim('"') ?? "";
                    
                    return (true, currentHash, commitMessage);
                }
                
                return (false, "", "");
            }
            catch (Exception ex)
            {
                OnOutput?.Invoke($"[错误] 检查 HEAD 状态失败：{ex.Message}");
                return (false, "", "");
            }
        }

        /// <summary>
        /// 创建新分支并切换到该分支
        /// </summary>
        public async Task<bool> CreateAndCheckoutBranchAsync(string branchName)
        {
            try
            {
                OnOutput?.Invoke($"正在创建分支：{branchName}...");
                
                // 创建并切换到新分支
                var (success, output) = await ExecuteGitCommandAsync($"checkout -b {branchName}");
                
                if (success)
                {
                    OnOutput?.Invoke($"✓ 已创建并切换到分支：{branchName}");
                    return true;
                }
                else
                {
                    OnOutput?.Invoke($"✗ 创建分支失败：{output}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnOutput?.Invoke($"[错误] 创建分支失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成建议的分支名称
        /// </summary>
        public string GenerateSuggestedBranchName(string versionNumber = "")
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd");
            
            if (!string.IsNullOrEmpty(versionNumber))
            {
                // 基于版本号生成：branch-from-v3-20241124
                return $"branch-from-v{versionNumber}-{timestamp}";
            }
            else
            {
                // 默认生成：detached-20241124
                return $"detached-{timestamp}";
            }
        }

        /// <summary>
        /// 检查分支是否已存在
        /// </summary>
        public async Task<bool> BranchExistsAsync(string branchName)
        {
            try
            {
                var (_, output) = await ExecuteGitCommandAsync("branch --list");
                var branches = output?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (branches != null)
                {
                    foreach (var branch in branches)
                    {
                        var cleanBranch = branch.Trim().TrimStart('*').Trim();
                        if (cleanBranch == branchName)
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
    }
}
