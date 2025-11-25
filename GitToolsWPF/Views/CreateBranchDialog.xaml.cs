using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;

namespace GitToolsWPF.Views
{
    public partial class CreateBranchDialog : Window
    {
        // Windows API for dark title bar
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public string BranchName { get; private set; } = "";
        public bool IsConfirmed { get; private set; }

        public CreateBranchDialog(string currentVersion, string commitHash, string commitMessage, string suggestedBranchName)
        {
            InitializeComponent();

            // 设置当前位置信息
            CurrentPositionText.Text = $"{currentVersion} ({commitHash})";
            CommitMessageText.Text = $"提交信息：{commitMessage}";

            // 设置建议的分支名
            BranchNameInput.Text = suggestedBranchName;

            // 聚焦到输入框并选中文本
            Loaded += (s, e) =>
            {
                BranchNameInput.Focus();
                BranchNameInput.SelectAll();
                ApplyDarkTitleBar();
            };
        }

        private void BranchNameInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var branchName = BranchNameInput.Text.Trim();

            // 验证分支名
            if (string.IsNullOrWhiteSpace(branchName))
            {
                ValidationMessage.Text = "分支名称不能为空";
                ValidationMessage.Visibility = Visibility.Visible;
                CreateButton.IsEnabled = false;
                return;
            }

            // Git 分支名规则验证
            if (!IsValidBranchName(branchName))
            {
                ValidationMessage.Text = "分支名称包含非法字符（不能包含空格、~、^、:、?、*、[、\\等）";
                ValidationMessage.Visibility = Visibility.Visible;
                CreateButton.IsEnabled = false;
                return;
            }

            // 验证通过
            ValidationMessage.Visibility = Visibility.Collapsed;
            CreateButton.IsEnabled = true;
        }

        private bool IsValidBranchName(string branchName)
        {
            // Git 分支名规则：
            // - 不能包含空格
            // - 不能包含 ~, ^, :, ?, *, [, \
            // - 不能以 / 结尾
            // - 不能包含连续的 ..
            // - 不能以 . 开头

            if (branchName.StartsWith("."))
                return false;

            if (branchName.EndsWith("/"))
                return false;

            if (branchName.Contains(".."))
                return false;

            // 检查非法字符
            var invalidChars = new[] { ' ', '~', '^', ':', '?', '*', '[', '\\', '\t', '\n', '\r' };
            foreach (var c in invalidChars)
            {
                if (branchName.Contains(c))
                    return false;
            }

            return true;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            BranchName = BranchNameInput.Text.Trim();
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                var isDarkTheme = IsDarkTheme();

                if (isDarkTheme)
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        int value = 1;
                        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
                        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
                    }
                }
            }
            catch { }
        }

        private bool IsDarkTheme()
        {
            try
            {
                if (Application.Current.Resources.MergedDictionaries.Count > 0)
                {
                    var theme = Application.Current.Resources.MergedDictionaries[0];
                    if (theme.Contains("ContentBackground"))
                    {
                        var brush = theme["ContentBackground"] as System.Windows.Media.SolidColorBrush;
                        if (brush != null)
                        {
                            var color = brush.Color;
                            var brightness = (color.R + color.G + color.B) / 3;
                            return brightness < 128;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
