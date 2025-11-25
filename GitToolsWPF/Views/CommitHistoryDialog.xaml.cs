using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GitToolsWPF.ViewModels;

namespace GitToolsWPF.Views
{
    public partial class CommitHistoryDialog : Window
    {
        // Windows API for dark title bar
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public CommitHistoryDialog(CommitHistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // 应用主题
            ApplyTheme();
            
            // 设置自适应高度
            SetAdaptiveHeight();
            
            // 设置标题栏颜色
            Loaded += (s, e) => ApplyDarkTitleBar();
        }

        private void SetAdaptiveHeight()
        {
            try
            {
                // 获取屏幕工作区域尺寸（排除任务栏）
                var screenHeight = SystemParameters.WorkArea.Height;
                var screenWidth = SystemParameters.WorkArea.Width;
                
                // === 设置窗口高度 ===
                double windowHeight;
                
                if (screenHeight <= 768)
                {
                    // 小屏幕：75% 的屏幕高度
                    windowHeight = screenHeight * 0.75;
                }
                else if (screenHeight <= 1080)
                {
                    // 中等屏幕：80% 的屏幕高度
                    windowHeight = screenHeight * 0.80;
                }
                else
                {
                    // 大屏幕：85% 的屏幕高度
                    windowHeight = screenHeight * 0.85;
                }
                
                // 确保不小于最小高度
                windowHeight = Math.Max(windowHeight, MinHeight);
                
                // 确保不超过最大高度（屏幕的 90%）
                var maxHeight = screenHeight * 0.90;
                windowHeight = Math.Min(windowHeight, maxHeight);
                
                // === 设置窗口宽度 ===
                double windowWidth;
                
                if (screenWidth <= 1366)
                {
                    // 小屏幕：85% 的屏幕宽度
                    windowWidth = screenWidth * 0.85;
                }
                else if (screenWidth <= 1920)
                {
                    // 中等屏幕：70% 的屏幕宽度
                    windowWidth = screenWidth * 0.70;
                }
                else
                {
                    // 大屏幕：60% 的屏幕宽度
                    windowWidth = screenWidth * 0.60;
                }
                
                // 确保不小于最小宽度
                windowWidth = Math.Max(windowWidth, MinWidth);
                
                // 确保不超过最大宽度（屏幕的 90%）
                var maxWidth = screenWidth * 0.90;
                windowWidth = Math.Min(windowWidth, maxWidth);
                
                // 应用窗口尺寸
                this.Height = windowHeight;
                this.MaxHeight = maxHeight;
                this.Width = windowWidth;
                this.MaxWidth = maxWidth;
            }
            catch
            {
                // 如果获取屏幕信息失败，使用默认尺寸
                this.Height = 700;
                this.MaxHeight = 900;
                this.Width = 1100;
                this.MaxWidth = 1400;
            }
        }

        private void ApplyTheme()
        {
            // 从应用程序资源中获取当前主题
            if (Application.Current.Resources.MergedDictionaries.Count > 0)
            {
                var theme = Application.Current.Resources.MergedDictionaries[0];
                Resources.MergedDictionaries.Clear();
                Resources.MergedDictionaries.Add(theme);
            }
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                // 检查是否是深色主题
                var isDarkTheme = IsDarkTheme();
                
                if (isDarkTheme)
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        int value = 1; // 1 = dark mode, 0 = light mode
                        
                        // 尝试 Windows 11 的方式
                        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
                        
                        // 尝试 Windows 10 的方式（作为后备）
                        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
                    }
                }
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
