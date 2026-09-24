// Converters/ExpandCollapseConverter.cs
using System.Globalization;
using System.Windows.Data;

namespace ProductionLineManage.Shared.Converters
{
    /// <summary> 展开/折叠图标转换器：bool 映射为 ▼（展开）或 ▶（折叠） </summary>
    public class ExpandCollapseConverter : IValueConverter
    {
        #region ===================== IValueConverter =====================

        /// <summary> 数据源 → 界面：展开显示 ▼，折叠显示 ▶ </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool && (bool)value) ? " ▼ " : " ▶ "; // true=已展开
        }

        /// <summary> 界面 → 数据源：不支持反向转换 </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // 单向绑定，禁止回写
        }

        #endregion
    }
}
