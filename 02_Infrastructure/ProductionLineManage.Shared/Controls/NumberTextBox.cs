using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TextBox = HandyControl.Controls.TextBox;

namespace ProductionLineManage.Shared.Controls
{
    /// <summary>自定义 TextBox 控件，仅允许整数输入并限制范围</summary>
    public class NumberTextBox : TextBox
    {
        #region ===================== 依赖属性 =====================

        /// <summary>最小值依赖属性</summary>
        public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            "Minimum",
            typeof(int),
            typeof(NumberTextBox),
            new PropertyMetadata(0));

        /// <summary>允许输入的最小值</summary>
        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        /// <summary>最大值依赖属性</summary>
        public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            "Maximum",
            typeof(int),
            typeof(NumberTextBox),
            new PropertyMetadata(99999));

        /// <summary>允许输入的最大值</summary>
        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        #endregion

        #region ===================== 构造 =====================

        /// <summary>初始化并注册输入拦截事件</summary>
        public NumberTextBox()
        {
            PreviewTextInput += OnPreviewTextInput;
            PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(this, OnPaste);
            TextChanged += OnTextChanged;
        }

        #endregion

        #region ===================== 输入拦截 =====================

        private bool _isUpdating = false;

        /// <summary>文本变化后过滤非数字字符并校验范围（拦截中文输入法）</summary>
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            string filtered = Regex.Replace(textBox.Text, "[^0-9]", "");

            if (filtered != textBox.Text)
            {
                _isUpdating = true;

                int caretIndex = textBox.CaretIndex;

                textBox.Text = filtered;

                int removedCount = textBox.Text.Length - filtered.Length;
                textBox.CaretIndex = Math.Max(0, caretIndex - removedCount)-1;

                _isUpdating = false;
            }

            if (int.TryParse(textBox.Text, out int value))
            {
                if (value < Minimum)
                {
                    _isUpdating = true;
                    textBox.Text = Minimum.ToString();
                    textBox.CaretIndex = textBox.Text.Length;
                    _isUpdating = false;
                }
                else if (value > Maximum)
                {
                    _isUpdating = true;
                    textBox.Text = Maximum.ToString();
                    textBox.CaretIndex = textBox.Text.Length;
                    _isUpdating = false;
                }
            }
        }

        /// <summary>拦截非数字字符输入</summary>
        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^[0-9]+$");
            e.Handled = !regex.IsMatch(e.Text);
        }

        /// <summary>拦截空格等功能键</summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back || e.Key == Key.Delete ||
                e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Tab || e.Key == Key.Enter)
            {
                return;
            }

            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        /// <summary>拦截非数字粘贴</summary>
        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsNumeric(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        /// <summary>判断文本是否为整数</summary>
        private bool IsNumeric(string text)
        {
            return int.TryParse(text, out _);
        }

        #endregion
    }
}
