using NPOI.SS.UserModel;
using NPOI.XSSF.Streaming;
using ProductionLineManage.Core.Models.DataBase;
using System.IO;

namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 物料绑定数据导出 Excel（xlsx，SXSSF 流式写入，超行数自动分 Sheet）。
    /// </summary>
    public class MaterialBindExcelExporter
    {
        #region ===================== 常量 =====================

        /// <summary> SXSSF 内存窗口行数 </summary>
        private const int WorkbookRowWindow = 5000;
        private static readonly string[] Headers = MaterialBindExportHelper.Headers;

        #endregion

        #region ===================== 导出入口 =====================

        /// <summary>
        /// 分批拉取绑定记录并写入 xlsx。
        /// </summary>
        public async Task<ProductDataExportResult> ExportAsync(
            string filePath,
            Func<int, int, Task<IReadOnlyList<report_MaterialBind>>> fetchBatchAsync,
            IReadOnlyDictionary<int, craft_StationInfo> stationDict,
            IReadOnlyDictionary<int, craft_TypeInfo> typeDict,
            IReadOnlyDictionary<int, craft_LineInfo> lineDict,
            CancellationToken cancellationToken = default)
        {
            var workbook = new SXSSFWorkbook(WorkbookRowWindow);
            try
            {
                workbook.CompressTempFiles = false;

                var pageIndex = 1;
                ISheet sheet = CreateSheet(workbook, pageIndex);
                WriteHeader(sheet);

                var dateTimeStyle = CreateDateTimeStyle(workbook);
                var currentRowIndex = 1;
                var offset = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = await fetchBatchAsync(offset, ProductDataExportHelper.DbBatchSize).ConfigureAwait(false);
                    if (batch.Count == 0)
                        break;

                    foreach (var entity in batch)
                    {
                        if (currentRowIndex > ProductDataExportHelper.MaxDataRowsPerPage)
                        {
                            FlushSheet(sheet);
                            pageIndex++;
                            sheet = CreateSheet(workbook, pageIndex);
                            WriteHeader(sheet);
                            currentRowIndex = 1;
                        }

                        WriteDataRow(sheet, currentRowIndex, entity, stationDict, typeDict, lineDict, dateTimeStyle);
                        currentRowIndex++;
                    }

                    offset += batch.Count;
                    if (batch.Count < ProductDataExportHelper.DbBatchSize)
                        break;
                }

                FlushSheet(sheet);

                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                workbook.Write(stream);

                return new ProductDataExportResult
                {
                    OutputPaths = new[] { filePath },
                    PageCount = pageIndex
                };
            }
            finally
            {
                workbook.Dispose();
            }
        }

        #endregion

        #region ===================== Sheet 操作 =====================

        /// <summary> 创建分页 Sheet </summary>
        private static ISheet CreateSheet(IWorkbook workbook, int pageIndex) =>
            workbook.CreateSheet(ProductDataExportHelper.BuildPageName(pageIndex));

        /// <summary> 刷盘 SXSSF 行缓存 </summary>
        private static void FlushSheet(ISheet sheet)
        {
            if (sheet is SXSSFSheet sxssfSheet)
                sxssfSheet.FlushRows();
        }

        /// <summary> 写入表头行 </summary>
        private static void WriteHeader(ISheet sheet)
        {
            var headerRow = sheet.CreateRow(0);
            for (var i = 0; i < Headers.Length; i++)
                headerRow.CreateCell(i).SetCellValue(Headers[i]);
        }

        /// <summary> 创建日期时间单元格样式 </summary>
        private static ICellStyle CreateDateTimeStyle(IWorkbook workbook)
        {
            var style = workbook.CreateCellStyle();
            var format = workbook.CreateDataFormat();
            style.DataFormat = format.GetFormat("yyyy-mm-dd hh:mm:ss");
            return style;
        }

        #endregion

        #region ===================== 数据行写入 =====================

        /// <summary> 写入单条绑定记录 </summary>
        private static void WriteDataRow(
            ISheet sheet,
            int rowIndex,
            report_MaterialBind entity,
            IReadOnlyDictionary<int, craft_StationInfo> stationDict,
            IReadOnlyDictionary<int, craft_TypeInfo> typeDict,
            IReadOnlyDictionary<int, craft_LineInfo> lineDict,
            ICellStyle dateTimeStyle)
        {
            stationDict.TryGetValue(entity.StationId, out var station);
            typeDict.TryGetValue(entity.ProductTypeId, out var type);
            lineDict.TryGetValue(entity.LineId, out var line);

            var row = sheet.CreateRow(rowIndex);

            ExcelExportCellHelper.SetInt(row.CreateCell(0), entity.Id);
            ExcelExportCellHelper.SetString(row.CreateCell(1), entity.FlowCode);
            ExcelExportCellHelper.SetString(row.CreateCell(2), type?.Name ?? entity.ProductTypeId.ToString());
            ExcelExportCellHelper.SetString(row.CreateCell(3), line?.Name ?? entity.LineId.ToString());
            ExcelExportCellHelper.SetString(row.CreateCell(4), station?.DisplayText ?? entity.StationId.ToString());
            ExcelExportCellHelper.SetString(row.CreateCell(5), entity.BindMaterialName);
            ExcelExportCellHelper.SetString(row.CreateCell(6), entity.BindMaterialCode);
            ExcelExportCellHelper.SetString(row.CreateCell(7), MaterialBindQueryHelper.GetBindStatusText(entity.BindStatus));
            ExcelExportCellHelper.SetDateTimeOrBlank(row.CreateCell(8), entity.BindTime, dateTimeStyle);
            ExcelExportCellHelper.SetDateTimeOrBlank(
                row.CreateCell(9),
                entity.UnbindTime == default ? null : entity.UnbindTime, // default 表示未解绑
                dateTimeStyle);
        }

        #endregion
    }
}
