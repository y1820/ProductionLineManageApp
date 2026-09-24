using ProductionLineManage.Core.Models.DataBase;
using System.Globalization;
using System.IO;
using System.Text;

namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 物料绑定数据导出 CSV（流式写入；超行数上限时分文件）。
    /// </summary>
    public class MaterialBindCsvExporter
    {
        #region ===================== 导出入口 =====================

        /// <summary>
        /// 分批拉取绑定记录并写入 CSV。
        /// </summary>
        public async Task<ProductDataExportResult> ExportAsync(
            string filePath,
            Func<int, int, Task<IReadOnlyList<report_MaterialBind>>> fetchBatchAsync,
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
                        break;

                    var sb = new StringBuilder(512);
                    foreach (var entity in batch)
                    {
                        if (dataRowsOnPage >= ProductDataExportHelper.MaxDataRowsPerPage)
                        {
                            await CloseWriterAsync(writer);
                            writer = null;
                            pageIndex++;
                            dataRowsOnPage = 0;
                        }

                        writer ??= await OpenWriterAsync(filePath, pageIndex, outputPaths);
                        sb.Clear();
                        AppendCsvRow(sb, entity, stationDict, typeDict, lineDict);
                        await writer.WriteLineAsync(sb.ToString());
                        dataRowsOnPage++;
                    }

                    offset += batch.Count;
                    if (batch.Count < ProductDataExportHelper.DbBatchSize)
                        break;
                }
            }
            finally
            {
                await CloseWriterAsync(writer);
            }

            return new ProductDataExportResult
            {
                OutputPaths = outputPaths,
                PageCount = Math.Max(1, pageIndex)
            };
        }

        #endregion

        #region ===================== 文件 IO =====================

        /// <summary> 打开分页 CSV 并写入表头 </summary>
        private static async Task<StreamWriter> OpenWriterAsync(string filePath, int pageIndex, List<string> outputPaths)
        {
            var path = ProductDataExportHelper.BuildPagedFilePath(filePath, pageIndex);
            outputPaths.Add(path);

            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            await writer.WriteLineAsync(string.Join(",",
                MaterialBindExportHelper.Headers.Select(ProductDataExportHelper.EscapeCsvField)));
            return writer;
        }

        /// <summary> 刷盘并关闭 Writer </summary>
        private static async Task CloseWriterAsync(StreamWriter? writer)
        {
            if (writer == null)
                return;

            await writer.FlushAsync();
            writer.Dispose();
        }

        #endregion

        #region ===================== CSV 行组装 =====================

        /// <summary> 组装单条绑定记录 CSV 行 </summary>
        private static void AppendCsvRow(
            StringBuilder sb,
            report_MaterialBind entity,
            IReadOnlyDictionary<int, craft_StationInfo> stationDict,
            IReadOnlyDictionary<int, craft_TypeInfo> typeDict,
            IReadOnlyDictionary<int, craft_LineInfo> lineDict)
        {
            stationDict.TryGetValue(entity.StationId, out var station);
            typeDict.TryGetValue(entity.ProductTypeId, out var type);
            lineDict.TryGetValue(entity.LineId, out var line);

            AppendField(sb, entity.Id.ToString(CultureInfo.InvariantCulture));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(entity.FlowCode));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(type?.Name ?? entity.ProductTypeId.ToString()));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(line?.Name ?? entity.LineId.ToString()));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(station?.DisplayText ?? entity.StationId.ToString()));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(entity.BindMaterialName));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(entity.BindMaterialCode));
            AppendField(sb, ProductDataExportHelper.EscapeCsvField(MaterialBindQueryHelper.GetBindStatusText(entity.BindStatus)));
            AppendField(sb, ProductDataExportHelper.FormatCsvDateTime(entity.BindTime));
            AppendField(sb, entity.UnbindTime == default
                ? string.Empty
                : ProductDataExportHelper.FormatCsvDateTime(entity.UnbindTime), isLast: true);
        }

        /// <summary> 追加 CSV 字段 </summary>
        private static void AppendField(StringBuilder sb, string value, bool isLast = false)
        {
            sb.Append(value);
            if (!isLast)
                sb.Append(',');
        }

        #endregion
    }
}
