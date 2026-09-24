using ProductionLineManage.Core.Models.DataBase;
using System.Globalization;
using System.IO;
using System.Text;

namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 产品加工数据导出 CSV（流式写入；超 Excel 行数上限时分文件）。
    /// </summary>
    public class ProductDataCsvExporter
    {
        #region ===================== 导出入口 =====================

        /// <summary>
        /// 分批拉取数据并写入 CSV，超 MaxDataRowsPerPage 时新建文件。
        /// </summary>
        public async Task<ProductDataExportResult> ExportAsync(
            string filePath,
            Func<int, int, Task<IReadOnlyList<report_ProcessHistory>>> fetchBatchAsync,
            IReadOnlyDictionary<int, craft_StationInfo> stationDict,
            IReadOnlyDictionary<int, craft_TypeInfo> typeDict,
            IReadOnlyDictionary<int, craft_LineInfo> lineDict,
            CancellationToken cancellationToken = default)
        {
            var outputPaths = new List<string>();
            StreamWriter? writer = null;
            var pageIndex = 1;
            var dataRowsOnPage = 0;
            var offset = 0;

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = await fetchBatchAsync(offset, ProductDataExportHelper.DbBatchSize).ConfigureAwait(false);
                    if (batch.Count == 0)
                    {
                        break;
                    }

                    var sb = new StringBuilder(512); // 复用 StringBuilder 减少分配
                    foreach (var entity in batch)
                    {
                        if (dataRowsOnPage >= ProductDataExportHelper.MaxDataRowsPerPage)
                        {
                            await CloseWriterAsync(writer); // 关闭当前文件
                            writer = null;
                            pageIndex++;
                            dataRowsOnPage = 0;
                        }

                        writer ??= await OpenWriterAsync(filePath, pageIndex, outputPaths); // 懒创建 Writer
                        sb.Clear();
                        AppendCsvRow(sb, entity, stationDict, typeDict, lineDict);
                        await writer.WriteLineAsync(sb.ToString());
                        dataRowsOnPage++;
                    }

                    offset += batch.Count;
                    if (batch.Count < ProductDataExportHelper.DbBatchSize)
                    {
                        break;
                    }
                }
            }
            finally
            {
                await CloseWriterAsync(writer); // 确保文件关闭
            }

            return new ProductDataExportResult
            {
                OutputPaths = outputPaths,
                PageCount = Math.Max(1, pageIndex)
            };
        }

        #endregion

        #region ===================== 文件 IO =====================

        /// <summary> 打开分页 CSV 文件并写入 BOM + 表头 </summary>
        private static async Task<StreamWriter> OpenWriterAsync(string filePath, int pageIndex, List<string> outputPaths)
        {
            var path = ProductDataExportHelper.BuildPagedFilePath(filePath, pageIndex);
            outputPaths.Add(path);

            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)); // UTF-8 BOM
            await writer.WriteLineAsync(string.Join(",", ProductDataExportHelper.Headers.Select(ProductDataExportHelper.EscapeCsvField)));
            return writer;
        }

        /// <summary> 刷盘并关闭 Writer </summary>
        private static async Task CloseWriterAsync(StreamWriter? writer)
        {
            if (writer == null)
            {
                return;
            }

            await writer.FlushAsync();
            writer.Dispose();
        }

        #endregion

        #region ===================== CSV 行组装 =====================

        /// <summary> 组装单条加工历史 CSV 行 </summary>
        private static void AppendCsvRow(
            StringBuilder sb,
            report_ProcessHistory entity,
            IReadOnlyDictionary<int, craft_StationInfo> stationDict,
            IReadOnlyDictionary<int, craft_TypeInfo> typeDict,
            IReadOnlyDictionary<int, craft_LineInfo> lineDict)
        {
            stationDict.TryGetValue(entity.StationId, out var station);
            typeDict.TryGetValue(entity.ProductTypeId, out var type);
            lineDict.TryGetValue(entity.LineId, out var line);

            AppendField(sb, entity.Id.ToString(CultureInfo.InvariantCulture));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(entity.FlowCode));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(station?.DisplayText ?? entity.StationId.ToString()));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(type?.Name ?? entity.ProductTypeId.ToString()));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(line?.Name ?? (entity.LineId > 0 ? entity.LineId.ToString() : "-")));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(entity.DataName));
            AppendField(sb, ProductDataExportHelper.FormatCsvCell(entity.DataValue, entity.DataType));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(entity.DataUnit));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(entity.DataType));
            AppendField(sb, entity.StationRecordId.ToString(CultureInfo.InvariantCulture));
            AppendField(sb, ProductDataExportHelper.FormatCsvDateTime(entity.CreateTime), isLast: true);
        }

        /// <summary> 追加 CSV 字段（逗号分隔） </summary>
        private static void AppendField(StringBuilder sb, string value, bool isLast = false)
        {
            sb.Append(value);
            if (!isLast)
            {
                sb.Append(',');
            }
        }

        #endregion
    }
}
