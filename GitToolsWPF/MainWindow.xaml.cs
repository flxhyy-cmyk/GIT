using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using GitToolsWPF.ViewModels;
using Microsoft.Win32;

namespace GitToolsWPF
{
    public partial class MainWindow : Window
    {
        // Windows API for dark title bar
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private MainViewModel ViewModel => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
            
            // 设置标题栏颜色
            Loaded += (s, e) => ApplyTitleBarTheme();
            
            // 初始化 Token
            TokenBox.Password = ViewModel.Settings.GitHubToken;
            TokenBox.PasswordChanged += (s, e) => ViewModel.Settings.GitHubToken = TokenBox.Password;
            
            // 监听日志消息变化，自动滚动到底部
            ViewModel.LogMessages.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    // 使用 Dispatcher 确保 UI 更新后再滚动
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (LogListBox.Items.Count > 0)
                        {
                            // 滚动到最后一项
                            LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            };

            // 监听 SelectedPage 属性变化，自动切换页面
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.SelectedPage))
                {
                    SwitchToPage(ViewModel.SelectedPage);
                }
            };
            
            // 监听悬浮通知请求
            ViewModel.ShowNotificationRequested += (message, color) =>
            {
                Dispatcher.Invoke(() => ShowNotification(message, color));
            };
            
            // 监听主题变化事件
            ViewModel.ThemeChanged += () =>
            {
                Dispatcher.Invoke(() => ApplyTitleBarTheme());
            };
        }

        private void SwitchToPage(string pageTag)
        {
            // 隐藏所有页面
            HistoryPage.Visibility = Visibility.Collapsed;
            PushPage.Visibility = Visibility.Collapsed;
            UpdatePage.Visibility = Visibility.Collapsed;
            ReleasePage.Visibility = Visibility.Collapsed;
            VersionPage.Visibility = Visibility.Collapsed;
            ClonePage.Visibility = Visibility.Collapsed;
            InitHistoryPage.Visibility = Visibility.Collapsed;
            CleanPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Collapsed;

            // 显示选中的页面
            var targetPage = pageTag switch
            {
                "History" => HistoryPage,
                "Push" => PushPage,
                "Update" => UpdatePage,
                "Release" => ReleasePage,
                "Version" => VersionPage,
                "Clone" => ClonePage,
                "InitHistory" => InitHistoryPage,
                "Clean" => CleanPage,
                "Settings" => SettingsPage,
                _ => HistoryPage
            };

            targetPage.Visibility = Visibility.Visible;
            
            // 滚动到顶部
            ContentScrollViewer.ScrollToTop();
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string tag)
                return;

            // 更新 ViewModel 的 SelectedPage，会触发自动切换
            ViewModel.SelectedPage = tag;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择本地文件夹（用于工作和克隆）"
            };

            if (dialog.ShowDialog() == true)
            {
                // 设置路径
                ViewModel.Settings.LocalFolder = dialog.FolderName;
                
                // 自动检测仓库地址
                DetectAndUpdateRepoUrl();
                
                // 静默保存设置到文件（不弹出提示框）
                ViewModel.SaveSettingsSilently();
                
                // 切换到项目克隆页面（会自动触发页面切换）
                ViewModel.SelectedPage = "Clone";
            }
        }

        private void LocalFolderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is string selectedPath)
            {
                // 更新设置
                ViewModel.Settings.LocalFolder = selectedPath;
                
                // 自动检测仓库地址
                DetectAndUpdateRepoUrl();
            }
        }

        private void DetectRepoUrl_Click(object sender, RoutedEventArgs e)
        {
            DetectAndUpdateRepoUrl();
        }

        private void RepoUrlLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenRepoUrl();
            e.Handled = true; // 防止事件冒泡
        }

        private void RepoUrlLink_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Documents.Run run)
            {
                run.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0056b3"));
            }
        }

        private void RepoUrlLink_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Documents.Run run)
            {
                run.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#007BFF"));
            }
        }

        private void OpenRepoUrl()
        {
            var repoUrl = ViewModel.Settings.RepoUrl;
            
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                ShowNotification("⚠️ 请先配置仓库地址", "#FFC107");
                return;
            }
            
            try
            {
                // 转换为浏览器可访问的URL
                var browserUrl = ConvertToBrowserUrl(repoUrl);
                
                if (string.IsNullOrWhiteSpace(browserUrl))
                {
                    ShowNotification("⚠️ 仓库地址格式无效", "#DC3545");
                    return;
                }
                
                // 在默认浏览器中打开
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = browserUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                
                ShowNotification("✓ 已在浏览器中打开", "#28A745");
            }
            catch (Exception ex)
            {
                ShowNotification($"✗ 打开失败：{ex.Message}", "#DC3545");
            }
        }

        private string ConvertToBrowserUrl(string repoUrl)
        {
            try
            {
                var url = repoUrl.Trim();
                
                // 移除 .git 后缀
                if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(0, url.Length - 4);
                }
                
                // 如果已经是 HTTPS URL，直接返回
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }
                
                // 转换 SSH 格式到 HTTPS
                // git@github.com:user/repo -> https://github.com/user/repo
                if (url.StartsWith("git@"))
                {
                    var parts = url.Split('@', ':');
                    if (parts.Length >= 3)
                    {
                        var host = parts[1]; // github.com
                        var path = parts[2]; // user/repo
                        return $"https://{host}/{path}";
                    }
                }
                
                // 如果无法识别格式，返回空
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async void DetectAndUpdateRepoUrl()
        {
            var localFolder = ViewModel.Settings.LocalFolder;
            
            if (string.IsNullOrWhiteSpace(localFolder))
            {
                ShowNotification("⚠️ 请先选择本地文件夹", "#FFC107");
                return;
            }

            if (!System.IO.Directory.Exists(localFolder))
            {
                ShowNotification("⚠️ 文件夹不存在", "#DC3545");
                return;
            }

            var gitConfigPath = System.IO.Path.Combine(localFolder, ".git", "config");
            
            if (!System.IO.File.Exists(gitConfigPath))
            {
                ShowNotification("ℹ️ 该文件夹不是 Git 仓库", "#6C757D");
                return;
            }

            try
            {
                // 读取 .git/config 文件
                var configContent = await System.IO.File.ReadAllTextAsync(gitConfigPath);
                
                // 查找 [remote "origin"] 下的 url
                var lines = configContent.Split('\n');
                bool inRemoteOrigin = false;
                string? detectedUrl = null;
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine == "[remote \"origin\"]")
                    {
                        inRemoteOrigin = true;
                        continue;
                    }
                    
                    if (inRemoteOrigin)
                    {
                        if (trimmedLine.StartsWith("["))
                        {
                            // 进入新的section，停止查找
                            break;
                        }
                        
                        if (trimmedLine.StartsWith("url = "))
                        {
                            detectedUrl = trimmedLine.Substring(6).Trim();
                            break;
                        }
                    }
                }
                
                if (!string.IsNullOrWhiteSpace(detectedUrl))
                {
                    var currentUrl = ViewModel.Settings.RepoUrl;
                    
                    // 更新仓库地址
                    ViewModel.Settings.RepoUrl = detectedUrl;
                    
                    // 从URL中提取仓库名称
                    var repoName = ExtractRepoName(detectedUrl);
                    
                    // 显示检测到的远程仓库
                    ShowNotification($"检测到远方仓库：{repoName}", "#28A745");
                }
                else
                {
                    ShowNotification("⚠️ 未找到远程仓库地址", "#FFC107");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"✗ 检测失败：{ex.Message}", "#DC3545");
            }
        }

        private string ExtractRepoName(string repoUrl)
        {
            try
            {
                // 移除 .git 后缀
                var url = repoUrl.TrimEnd('/');
                if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(0, url.Length - 4);
                }
                
                // 提取最后两段：用户名/仓库名
                // 支持格式：
                // https://github.com/user/repo.git
                // git@github.com:user/repo.git
                // https://github.com/user/repo
                
                if (url.Contains("://"))
                {
                    // HTTPS 格式
                    var uri = new Uri(url);
                    var segments = uri.AbsolutePath.Trim('/').Split('/');
                    if (segments.Length >= 2)
                    {
                        return $"{segments[segments.Length - 2]}/{segments[segments.Length - 1]}";
                    }
                }
                else if (url.Contains(":"))
                {
                    // SSH 格式 (git@github.com:user/repo)
                    var parts = url.Split(':');
                    if (parts.Length >= 2)
                    {
                        var path = parts[parts.Length - 1].Trim('/');
                        var segments = path.Split('/');
                        if (segments.Length >= 2)
                        {
                            return $"{segments[segments.Length - 2]}/{segments[segments.Length - 1]}";
                        }
                    }
                }
                
                // 如果无法解析，返回原URL
                return repoUrl;
            }
            catch
            {
                // 解析失败，返回原URL
                return repoUrl;
            }
        }

        private async void ShowNotification(string message, string backgroundColor)
        {
            // 设置消息和颜色
            NotificationText.Text = message;
            NotificationBorder.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(backgroundColor));
            
            // 根据消息类型设置图标
            if (message.StartsWith("✓"))
                NotificationIcon.Text = "✓";
            else if (message.StartsWith("⚠️"))
                NotificationIcon.Text = "⚠️";
            else if (message.StartsWith("ℹ️"))
                NotificationIcon.Text = "ℹ️";
            else if (message.StartsWith("✗"))
                NotificationIcon.Text = "✗";
            else
                NotificationIcon.Text = "•";
            
            // 显示通知
            NotificationBorder.Visibility = Visibility.Visible;
            
            // 3秒后自动隐藏
            await System.Threading.Tasks.Task.Delay(3000);
            NotificationBorder.Visibility = Visibility.Collapsed;
        }

        private void LogListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                // 获取选中的日志行
                var selectedItems = LogListBox.SelectedItems;
                if (selectedItems.Count > 0)
                {
                    var selectedText = string.Join(Environment.NewLine, 
                        selectedItems.Cast<string>());
                    
                    try
                    {
                        Clipboard.SetText(selectedText);
                        // 可选：显示提示
                        // MessageBox.Show($"已复制 {selectedItems.Count} 行到剪贴板", "提示", 
                        //     MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"复制失败：{ex.Message}", "错误", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                e.Handled = true;
            }
            // Ctrl+C 也支持复制
            else if (e.Key == System.Windows.Input.Key.C && 
                     (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                var selectedItems = LogListBox.SelectedItems;
                if (selectedItems.Count > 0)
                {
                    var selectedText = string.Join(Environment.NewLine, 
                        selectedItems.Cast<string>());
                    
                    try
                    {
                        Clipboard.SetText(selectedText);
                    }
                    catch { }
                }
            }
            // Ctrl+A 全选
            else if (e.Key == System.Windows.Input.Key.A && 
                     (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                LogListBox.SelectAll();
                e.Handled = true;
            }
        }

        public void ApplyTitleBarTheme()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                // 检查是否是深色主题
                var isDarkTheme = IsDarkTheme();
                int value = isDarkTheme ? 1 : 0; // 1 = dark mode, 0 = light mode

                // 尝试 Windows 11 的方式
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));

                // 尝试 Windows 10 的方式（作为后备）
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
            }
            catch
            {
                // 如果失败，忽略错误（可能是旧版本 Windows）
            }
        }

        private bool IsDarkTheme()
        {
            try
            {
                // 检查当前主题的背景色
                if (Application.Current.Resources.MergedDictionaries.Count > 0)
                {
                    var theme = Application.Current.Resources.MergedDictionaries[0];
                    if (theme.Contains("ContentBackground"))
                    {
                        var brush = theme["ContentBackground"] as System.Windows.Media.SolidColorBrush;
                        if (brush != null)
                        {
                            // 如果背景色的亮度低于 128，认为是深色主题
                            var color = brush.Color;
                            var brightness = (color.R + color.G + color.B) / 3;
                            return brightness < 128;
                        }
                    }
                }
            }
            catch
            {
                // 如果检测失败，默认返回 false
            }
            return false;
        }
    }
}
