using NPOI.SS.UserModel;
using NPOI.XSSF.Streaming;
using ProductionLineManage.Core.Models.DataBase;
using System.IO;

namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 产品加工数据导出 Excel（xlsx，SXSSF 流式写入，超行数自动分 Sheet）。
    /// </summary>
    public class ProductDataExcelExporter
    {
        #region ===================== 常量 =====================

        /// <summary> SXSSF 内存窗口行数 </summary>
        private const int WorkbookRowWindow = 5000;
        private const int DbBatchSize = ProductDataExportHelper.DbBatchSize;
        private static readonly string[] Headers = ProductDataExportHelper.Headers;

        #endregion

        #region ===================== 导出入口 =====================

        /// <summary>
        /// 分批拉取数据并写入 xlsx，超 MaxDataRowsPerPage 时新建 Sheet。
        /// </summary>
        public async Task<ProductDataExportResult> ExportAsync(
            string filePath,
            Func<int, int, Task<IReadOnlyList<report_ProcessHistory>>> fetchBatchAsync,
            IReadOnlyDictionary<int, craft_StationInfo> stationDict,
            IReadOnlyDictionary<int, craft_TypeInfo> typeDict,
            IReadOnlyDictionary<int, craft_LineInfo> lineDict,
            CancellationToken cancellationToken = default)
        {
            var workbook = new SXSSFWorkbook(WorkbookRowWindow); // 流式工作簿，降低内存
            try
            {
                workbook.CompressTempFiles = false; // 临时文件不压缩，加快写入

                var pageIndex = 1;
                ISheet sheet = CreateSheet(workbook, pageIndex);
                WriteHeader(sheet);

                var dateTimeStyle = CreateDateTimeStyle(workbook);
                var currentRowIndex = 1; // 数据行从第 2 行起（0-based 第 1 行）
                var offset = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested(); // 支持取消

                    var batch = await fetchBatchAsync(offset, DbBatchSize).ConfigureAwait(false);
                    if (batch.Count == 0)
                    {
                        break; // 无更多数据
                    }

                    foreach (var entity in batch)
                    {
                        if (currentRowIndex > ProductDataExportHelper.MaxDataRowsPerPage)
                        {
                            FlushSheet(sheet); // 刷盘当前 Sheet
                            pageIndex++;
                            sheet = CreateSheet(workbook, pageIndex); // 新建 Sheet
                            WriteHeader(sheet);
                            currentRowIndex = 1;
                        }

                        WriteDataRow(sheet, currentRowIndex, entity, stationDict, typeDict, lineDict, dateTimeStyle);
                        currentRowIndex++;
                    }

                    offset += batch.Count;
                    if (batch.Count < DbBatchSize)
                    {
                        break; // 最后一批
                    }
                }

                FlushSheet(sheet);

                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                workbook.Write(stream); // 写入磁盘

                return new ProductDataExportResult
                {
                    OutputPaths = new[] { filePath },
                    PageCount = pageIndex
                };
            }
            finally
            {
                workbook.Dispose(); // 释放 SXSSF 临时文件
            }
        }

        /// <summary> 生成默认 xlsx 文件名 </summary>
        public static string BuildDefaultFileName(string lineName, DateTime startTime, DateTime endTime) =>
            ProductDataExportHelper.BuildDefaultFileName(lineName, startTime, endTime, ".xlsx");

        #endregion

        #region ===================== Sheet 操作 =====================

        /// <summary> 创建分页 Sheet </summary>
        private static ISheet CreateSheet(IWorkbook workbook, int pageIndex)
        {
            return workbook.CreateSheet(ProductDataExportHelper.BuildPageName(pageIndex));
        }

        /// <summary> 刷盘 SXSSF 行缓存 </summary>
        private static void FlushSheet(ISheet sheet)
        {
            if (sheet is SXSSFSheet sxssfSheet)
            {
                sxssfSheet.FlushRows();
            }
        }

        /// <summary> 写入表头行 </summary>
        private static void WriteHeader(ISheet sheet)
        {
            var headerRow = sheet.CreateRow(0);
            for (var i = 0; i < Headers.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(Headers[i]);
            }
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

        /// <summary> 写入单条加工历史记录 </summary>
        private static void WriteDataRow(
            ISheet sheet,
            int rowIndex,
            report_ProcessHistory entity,
            IReadOnlyDictionary<int, craft_StationInfo> stationDict,
            IReadOnlyDictionary<int, craft_TypeInfo> typeDict,
            IReadOnlyDictionary<int, craft_LineInfo> lineDict,
            ICellStyle dateTimeStyle)
        {
            stationDict.TryGetValue(entity.StationId, out var station); // 工位显示名
            typeDict.TryGetValue(entity.ProductTypeId, out var type); // 型号名
            lineDict.TryGetValue(entity.LineId, out var line); // 产线名

            var row = sheet.CreateRow(rowIndex);

            ExcelExportCellHelper.SetInt(row.CreateCell(0), entity.Id);
            ExcelExportCellHelper.SetString(row.CreateCell(1), entity.FlowCode);
            ExcelExportCellHelper.SetString(row.CreateCell(2), station?.DisplayText ?? entity.StationId.ToString());
            ExcelExportCellHelper.SetString(row.CreateCell(3), type?.Name ?? entity.ProductTypeId.ToString());
            ExcelExportCellHelper.SetString(row.CreateCell(4), line?.Name ?? (entity.LineId > 0 ? entity.LineId.ToString() : "-"));
            ExcelExportCellHelper.SetString(row.CreateCell(5), entity.DataName);
            ExcelExportCellHelper.SetNumericOrString(row.CreateCell(6), entity.DataValue, entity.DataType); // 按类型推断
            ExcelExportCellHelper.SetString(row.CreateCell(7), entity.DataUnit);
            ExcelExportCellHelper.SetString(row.CreateCell(8), entity.DataType);
            ExcelExportCellHelper.SetInt(row.CreateCell(9), entity.StationRecordId);
            ExcelExportCellHelper.SetDateTimeOrBlank(row.CreateCell(10), entity.CreateTime, dateTimeStyle);
        }

        #endregion
    }
}
