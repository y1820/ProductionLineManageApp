using System.Globalization;
using System.IO;
using System.Text;

namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 产品加工数据导出辅助：批量大小、分页上限、表头、
    /// 文件名/分页名生成及 CSV 字段转义。
    /// </summary>
    public static class ProductDataExportHelper
    {
        #region ===================== 常量 =====================

        /// <summary> 数据库分批拉取行数 </summary>
        public const int DbBatchSize = 10000;

        /// <summary> 每个分页最多数据行数（不含表头；Excel 单 Sheet 上限 1048576 行含表头） </summary>
        public const int MaxDataRowsPerPage = 1_048_575;

        /// <summary> CSV/Excel 导出列标题 </summary>
        public static readonly string[] Headers =
        {
            "Id", "流水码", "工位", "型号", "产线", "数据名称", "数据值", "单位", "类型", "过站Id", "采集时间"
        };

        #endregion

        #region ===================== 文件名 =====================

        /// <summary> 生成默认导出文件名：{产线}({起止日期}).{扩展名} </summary>
        public static string BuildDefaultFileName(string lineName, DateTime startTime, DateTime endTime, string extension = ".csv")
        {
            var safeLineName = SanitizeFileName(string.IsNullOrWhiteSpace(lineName) ? "全部产线" : lineName);
            var startText = startTime.ToString("yyyy年MM月dd日");
            var endText = endTime.ToString("yyyy年MM月dd日");
            var ext = extension.StartsWith('.') ? extension : $".{extension}";
            return $"{safeLineName}({startText}-{endText}){ext}";
        }

        /// <summary> 根据扩展名判断是否为 CSV 导出 </summary>
        public static bool IsCsvExport(string filePath) =>
            Path.GetExtension(filePath).Equals(".csv", StringComparison.OrdinalIgnoreCase);

        /// <summary> 替换文件名中的非法字符 </summary>
        public static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_'); // 逐个替换
            }

            return name.Trim();
        }

        #endregion

        #region ===================== CSV 格式化 =====================

        /// <summary> CSV 字段转义（含逗号/引号/换行时加双引号） </summary>
        public static string EscapeCsvField(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\""; // RFC 4180 转义
            }

            return value;
        }

        /// <summary> 按数据类型提示格式化 CSV 单元格 </summary>
        public static string FormatCsvCell(string? text, string? dataTypeHint = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var trimmed = text.Trim();

            if (ExportValueParser.IsBoolType(dataTypeHint) && ExportValueParser.TryParseBool(trimmed, out var boolValue))
            {
                return boolValue ? "1" : "0"; // bool 导出为 0/1
            }

            if (ExportValueParser.IsDateTimeType(dataTypeHint) && ExportValueParser.TryParseDateTime(trimmed, out var dateFromText))
            {
                return dateFromText.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }

            if (ExportValueParser.TryParseNumber(trimmed, out var number))
            {
                return number.ToString(CultureInfo.InvariantCulture); // 数字不加引号
            }

            return EscapeCsvField(trimmed); // 其余按文本转义
        }

        /// <summary> 格式化 DateTime 为 CSV 字符串 </summary>
        public static string FormatCsvDateTime(DateTime? value) =>
            value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

        #endregion

        #region ===================== 分页 =====================

        /// <summary> 分页名称：第一页、第二页…… </summary>
        public static string BuildPageName(int pageIndex) => pageIndex switch
        {
            1 => "第一页",
            2 => "第二页",
            3 => "第三页",
            4 => "第四页",
            5 => "第五页",
            6 => "第六页",
            7 => "第七页",
            8 => "第八页",
            9 => "第九页",
            10 => "第十页",
            _ => $"第{pageIndex}页"
        };

        /// <summary> CSV 超行数时分文件：第二页起追加 _第二页 后缀 </summary>
        public static string BuildPagedFilePath(string filePath, int pageIndex)
        {
            if (pageIndex <= 1)
            {
                return filePath; // 第一页用原路径
            }

            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            return Path.Combine(directory, $"{fileNameWithoutExt}_{BuildPageName(pageIndex)}{extension}");
        }

        #endregion
    }
}
