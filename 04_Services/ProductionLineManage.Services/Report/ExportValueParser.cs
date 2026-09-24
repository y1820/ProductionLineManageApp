using System.Globalization;

namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 导出值解析器：按数据类型提示推断 bool / 日期 / 数字，
    /// 供 Excel 与 CSV 导出共用。
    /// </summary>
    internal static class ExportValueParser
    {
        #region ===================== 数值解析 =====================

        /// <summary> 尝试将文本解析为数字（整数或浮点） </summary>
        public static bool TryParseNumber(string text, out double number)
        {
            number = 0;

            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                number = integer; // 优先整型
                return true;
            }

            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number))
            {
                return true; // Invariant 浮点
            }

            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out number))
            {
                return true; // 当前区域浮点
            }

            return false;
        }

        #endregion

        #region ===================== 日期解析 =====================

        /// <summary> 尝试将文本解析为 DateTime </summary>
        public static bool TryParseDateTime(string text, out DateTime value)
        {
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out value);
        }

        #endregion

        #region ===================== 布尔解析 =====================

        /// <summary> 尝试将文本解析为 bool（支持 true/false 与 0/1） </summary>
        public static bool TryParseBool(string text, out bool value)
        {
            if (bool.TryParse(text, out value))
            {
                return true;
            }

            switch (text)
            {
                case "0":
                    value = false;
                    return true;
                case "1":
                    value = true;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        #endregion

        #region ===================== 类型推断 =====================

        /// <summary> 根据 DataType 提示判断是否为布尔类型 </summary>
        public static bool IsBoolType(string? dataTypeHint)
        {
            if (string.IsNullOrWhiteSpace(dataTypeHint))
            {
                return false;
            }

            var hint = dataTypeHint.Trim();
            return hint.Contains("bool", StringComparison.OrdinalIgnoreCase)
                || hint.Contains("bit", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary> 根据 DataType 提示判断是否为日期时间类型 </summary>
        public static bool IsDateTimeType(string? dataTypeHint)
        {
            if (string.IsNullOrWhiteSpace(dataTypeHint))
            {
                return false;
            }

            var hint = dataTypeHint.Trim();
            return hint.Contains("date", StringComparison.OrdinalIgnoreCase)
                || hint.Contains("time", StringComparison.OrdinalIgnoreCase)
                || hint.Contains("datetime", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
