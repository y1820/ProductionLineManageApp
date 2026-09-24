namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 物料绑定导出辅助：表头定义与默认文件名生成。
    /// </summary>
    public static class MaterialBindExportHelper
    {
        #region ===================== 表头 =====================

        /// <summary> CSV/Excel 导出列标题 </summary>
        public static readonly string[] Headers =
        {
            "Id", "流水码", "型号", "产线", "工位", "物料名称", "物料码", "绑定状态", "绑定时间", "解绑时间"
        };

        #endregion

        #region ===================== 文件名 =====================

        /// <summary> 生成默认导出文件名：绑定物料_{产线}({日期}).{扩展名} </summary>
        public static string BuildDefaultFileName(string lineName, string extension = ".csv")
        {
            var safeLineName = ProductDataExportHelper.SanitizeFileName(
                string.IsNullOrWhiteSpace(lineName) ? "全部产线" : lineName); // 非法字符替换
            var dateText = DateTime.Now.ToString("yyyy年MM月dd日");
            var ext = extension.StartsWith('.') ? extension : $".{extension}";
            return $"绑定物料_{safeLineName}({dateText}){ext}";
        }

        #endregion
    }
}
