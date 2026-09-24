using NPOI.SS.UserModel;

namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 导出 Excel 时按内容推断单元格类型（数字 / 日期 / 文本），
    /// 避免 Excel 将数字存为文本或反之。
    /// </summary>
    internal static class ExcelExportCellHelper
    {
        #region ===================== 基础类型 =====================

        /// <summary> 写入整型单元格 </summary>
        public static void SetInt(ICell cell, int value)
        {
            cell.SetCellValue(value);
        }

        /// <summary> 写入文本单元格 </summary>
        public static void SetString(ICell cell, string? text)
        {
            cell.SetCellValue(text ?? string.Empty);
        }

        #endregion

        #region ===================== 智能推断 =====================

        /// <summary> 按 DataType 提示写入数字或文本 </summary>
        public static void SetNumericOrString(ICell cell, string? text, string? dataTypeHint = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                cell.SetBlank(); // 空值留空
                return;
            }

            var trimmed = text.Trim();

            if (ExportValueParser.IsBoolType(dataTypeHint) && ExportValueParser.TryParseBool(trimmed, out var boolValue))
            {
                cell.SetCellValue(boolValue ? 1 : 0); // bool 存 0/1
                return;
            }

            if (ExportValueParser.IsDateTimeType(dataTypeHint) && ExportValueParser.TryParseDateTime(trimmed, out var dateFromText))
            {
                cell.SetCellValue(dateFromText); // Excel 日期序列
                return;
            }

            if (ExportValueParser.TryParseNumber(trimmed, out var number))
            {
                cell.SetCellValue(number); // 数字类型
                return;
            }

            cell.SetCellValue(trimmed); // 兜底文本
        }

        /// <summary> 写入 DateTime 或留空 </summary>
        public static void SetDateTimeOrBlank(ICell cell, DateTime? value, ICellStyle? dateStyle = null)
        {
            if (!value.HasValue)
            {
                cell.SetBlank();
                return;
            }

            cell.SetCellValue(value.Value);
            if (dateStyle != null)
            {
                cell.CellStyle = dateStyle; // 应用 yyyy-mm-dd hh:mm:ss 格式
            }
        }

        #endregion
    }
}
