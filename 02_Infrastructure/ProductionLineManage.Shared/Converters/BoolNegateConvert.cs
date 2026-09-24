using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ProductionLineManage.Shared.Converters
{
    /// <summary> Bool 取反转换器：true→false、false→true，仅支持单向绑定 </summary>
    public class BoolNegateConvert : IValueConverter
    {
        #region ===================== IValueConverter =====================

        /// <summary> 数据源 → 界面：对 bool 取反 </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? false : true; // 仅当值为 true 时返回 false

        /// <summary> 界面 → 数据源：不支持反向转换 </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException(); // 单向绑定，禁止回写

        #endregion
    }
}
