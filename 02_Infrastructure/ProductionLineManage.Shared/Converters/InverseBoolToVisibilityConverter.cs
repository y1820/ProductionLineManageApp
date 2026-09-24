using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ProductionLineManage.Shared.Converters
{
    /// <summary> 反向 Bool→Visibility：true 时 Collapsed，false 时 Visible </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        #region ===================== IValueConverter =====================

        /// <summary> 数据源 → 界面：bool 取反映射到 Visibility </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible; // true 隐藏，false 显示
            }
            return Visibility.Visible; // 非 bool 默认显示
        }

        /// <summary> 界面 → 数据源：Visible 映射 false，其余映射 true </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible; // Visible→false，Collapsed/Hidden→true
            }
            return false;
        }

        #endregion
    }
}
