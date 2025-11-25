using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GitToolsWPF.Views
{
    public partial class VerificationDialog : Window
    {
        // Windows API for dark title bar
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly string _verificationCode;

        public bool IsVerified { get; private set; }

        public VerificationDialog()
        {
            InitializeComponent();

            // 生成4位随机验证码
            var random = new Random();
            _verificationCode = random.Next(1000, 9999).ToString();
            VerificationCodeDisplay.Text = _verificationCode;

            // 聚焦到输入框和设置标题栏颜色
            Loaded += (s, e) =>
            {
                VerificationCodeInput.Focus();
                ApplyDarkTitleBar();
            };
        }

        private void VerificationCodeInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // 隐藏错误消息
            ErrorMessage.Visibility = Visibility.Collapsed;

            // 检查输入是否匹配
            var input = VerificationCodeInput.Text.Trim();
            ConfirmButton.IsEnabled = input == _verificationCode;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var input = VerificationCodeInput.Text.Trim();
            
            if (input == _verificationCode)
            {
                IsVerified = true;
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorMessage.Visibility = Visibility.Visible;
                VerificationCodeInput.SelectAll();
                VerificationCodeInput.Focus();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsVerified = false;
            DialogResult = false;
            Close();
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
    }
}
