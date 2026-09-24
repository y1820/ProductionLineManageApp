using ProductionLineManage.Core.Services.MotorCode;
using System.Data;
using System.Text.Json;
using Dapper;
using ProductionLineManage.Core.Configuration;
using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.ExternalWeb;
using ProductionLineManage.Infrastructure.Data.Repository;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.Production;

namespace ProductionLineManage.Services.ExternalWeb
{
    /// <summary>
    /// Win7 SOAP 外部工位业务实现（如 OP090）。
    /// 与 PLC 工位区别：不走 200/8000 过站明细；UploadDataWithInfo 保存时写入
    /// production_ProductStationStatus（供下线装箱等外部系统判定合格）及 report_ProcessHistory，
    /// 并将电机码绑定到 report_MaterialBind。
    /// 配置策略：appsettings 与工艺缓存有则用；无则 Id=0、物料名默认「电机码」，服务仍可正常运行。
    /// </summary>
    public sealed class ExternalWebStationService : IExternalWebStationService
    {
        #region ===================== 常量 =====================

        /// <summary> 写入 Application 日志时的 source 标识 </summary>
        private const string LogSource = "ExternalWebStation";

        /// <summary> 写入 Device 日志时的虚拟设备编码 </summary>
        private const string VirtualDeviceCode = "SOAP";

        #endregion

        #region ===================== 私有字段 =====================

        private readonly ExternalWebStationOptions _options;
        private readonly SQLHelper _sql;
        private readonly IDataCacheService _cache;
        private readonly ILogger _logger;
        private readonly IMotorCodeService _motorCodeService;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入配置、数据库、缓存、日志与电机码服务 </summary>
        public ExternalWebStationService(
            ExternalWebStationOptions options,
            SQLHelper sql,
            IDataCacheService cache,
            ILogger logger,
            IMotorCodeService motorCodeService)
        {
            _options = options;
            _sql = sql;
            _cache = cache;
            _logger = logger;
            _motorCodeService = motorCodeService;
        }

        #endregion

        #region ===================== SOAP 接口实现 =====================

        /// <summary> 连通性测试 </summary>
        public string HelloWorld()
        {
            Log(null, null, "HelloWorld");
            return "Hello World";
        }

        /// <summary> 获取客户名称（固定返回默认值） </summary>
        public string GetAllCustomerName() => "DefaultCustomer";

        /// <summary> 按客户名获取产品型号（返回配置的默认型号代号） </summary>
        public string GetProductTypeByCustomerName(string customerName) =>
            _options.DefaultProductTypeCode;

        /// <summary> 按产品型号生成电机码 </summary>
        public string GetMotorNumberByProductType(string productType)
        {
            var result = _motorCodeService.GenerateByProductTypeNameAsync(productType).GetAwaiter().GetResult();
            if (!result.Success)
            {
                Log(productType, null, $"GetMotorNumber 失败: {result.ErrorMessage}");
                return Fail(productType, null, result.ErrorMessage);
            }

            Log(productType, null, $"GetMotorNumber ProductType={productType} → {result.MotorCode}");
            return result.MotorCode;
        }

        /// <summary> 按产品型号与 FlagCode 生成电机码（FlagCode 暂不参与生成） </summary>
        public string GetMotorNumberByProductTypeAndFlagCode(string productType, string flagCode)
        {
            // FlagCode 暂不参与生成，与无 Flag 接口行为一致
            return GetMotorNumberByProductType(productType);
        }

        /// <summary> 上传原始 JSON 数据（兼容路径） </summary>
        public string UploadDataWithJSONCode(string jsonCode)
        {
            if (string.IsNullOrWhiteSpace(jsonCode))
                return Fail(null, null, "jsonCode is empty");

            try
            {
                SaveRawJsonUpload(jsonCode);
                Log(null, null, "UploadDataWithJSONCode 成功");
                return "OK";
            }
            catch (Exception ex)
            {
                return Fail(null, null, ex.Message, ex);
            }
        }

        /// <summary>
        /// 上传加工数据：解析 DataContent，写入工位状态、工艺历史与电机码绑定。
        /// </summary>
        public string UploadDataWithInfo(
            string stationName,
            string productType,
            string traySn,
            string productSn,
            string productResult,
            string dataContent,
            string insertTime,
            string operter)
        {
            try
            {
                // 有配置/缓存则解析 Id；解析失败时 Id 保持 0，仍允许落库
                var ctx = ResolveContext(stationName, productType);
                var parsed = ParseDataContent(dataContent);
                var flowCode = productSn ?? string.Empty;
                var trayCode = traySn ?? string.Empty;
                var status = ResolveProductStatus(productResult, parsed);

                _sql.ExecuteInTransactionAsync(async (conn, tran) =>
                {
                    var stationRecordId = 0;
                    if (CanWriteProductStationStatus(ctx, flowCode))
                    {
                        stationRecordId = await StationRecordOperations.UpsertWebSaveProductStatusAsync(
                            conn, tran, flowCode, trayCode,
                            ctx.StationId, ctx.ProductTypeId, ctx.LineId, status);
                    }

                    foreach (var item in parsed)
                    {
                        const string insertHistory = @"
INSERT INTO report_ProcessHistory
(StationRecordId, FlowCode, StationId, ProductTypeId, LineId,
 DataName, DataValue, DataType, DataUnit, IsRepair, RepairCount, IsQualified, CreateTime, UpdateTime)
VALUES
(@StationRecordId, @FlowCode, @StationId, @ProductTypeId, @LineId,
 @DataName, @DataValue, @DataType, @DataUnit, 0, 0, @IsQualified, GETDATE(), GETDATE())";

                        await conn.ExecuteAsync(insertHistory, new
                        {
                            StationRecordId = stationRecordId,
                            FlowCode = flowCode,
                            ctx.StationId,
                            ctx.ProductTypeId,
                            ctx.LineId,
                            item.DataName,
                            item.DataValue,
                            DataType = string.Empty,
                            DataUnit = string.Empty,
                            IsQualified = status == 1
                        }, tran);
                    }

                    // 从 DataContent 提取电机码并绑定为物料（名称默认「电机码」）
                    var motorCode = ExtractMotorCode(parsed);
                    if (!string.IsNullOrWhiteSpace(motorCode))
                        await BindMotorMaterialAsync(conn, tran, ctx, flowCode, motorCode);

                }).GetAwaiter().GetResult();

                Log(productType, stationName,
                    $"UploadDataWithInfo FlowCode={flowCode}, Station={stationName}, Status={status}, Items={parsed.Count}, Motor={(ExtractMotorCode(parsed) ?? "-")} → OK",
                    ctx.StationId);

                return "OK";
            }
            catch (Exception ex)
            {
                return Fail(productType, stationName, ex.Message, ex);
            }
        }

        /// <summary> 查询产品最近一次加工结果 </summary>
        public string GetLastProductResult(string productType, string stationName, string productSn)
        {
            try
            {
                var ctx = ResolveContext(stationName, productType);
                var result = QueryLastDataValue(productSn, ctx.StationId, "Final_Result")
                    ?? QueryLastDataValue(productSn, ctx.StationId, "_ProductResult")
                    ?? string.Empty;

                Log(productType, stationName, $"GetLastProductResult SN={productSn} → {result}", ctx.StationId);
                return result;
            }
            catch (Exception ex)
            {
                return Fail(productType, stationName, ex.Message, ex);
            }
        }

        /// <summary> 检查产品是否允许继续生产（上次结果须为 OK/1） </summary>
        public string CheckIsAllowProductionByProductSN(string productType, string stationName, string productSn)
        {
            try
            {
                var last = GetLastProductResult(productType, stationName, productSn);
                if (string.IsNullOrEmpty(last) || last == "1" || last.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    return "OK";

                return "NG";
            }
            catch (Exception ex)
            {
                return Fail(productType, stationName, ex.Message, ex);
            }
        }

        #endregion

        #region ===================== 电机码与落库 =====================

        /// <summary> UploadDataWithJSONCode 兼容路径：整段 JSON 写入一条 ProcessHistory </summary>
        private void SaveRawJsonUpload(string jsonCode)
        {
            _sql.ExecuteInTransactionAsync(async (conn, tran) =>
            {
                const string sql = @"
INSERT INTO report_ProcessHistory
(StationRecordId, FlowCode, StationId, ProductTypeId, LineId,
 DataName, DataValue, DataType, DataUnit, IsRepair, RepairCount, CreateTime)
VALUES
(0, N'', 0, 0, 0, N'RawJsonUpload', @DataValue, N'', N'', 0, 0, GETDATE())";

                await conn.ExecuteAsync(sql, new { DataValue = jsonCode }, tran);
            }).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 将电机码绑定为物料：先解绑同 FlowCode+工位+物料名的旧记录，再插入新绑定。
        /// 不过站 500 校验，与 DataSaveService 物料绑定逻辑类似但无过站前置条件。
        /// </summary>
        private async Task BindMotorMaterialAsync(
            IDbConnection conn,
            IDbTransaction tran,
            ResolvedContext ctx,
            string flowCode,
            string motorCode)
        {
            var materialName = ResolveMotorMaterialName(ctx);

            const string unbindSql = @"
UPDATE report_MaterialBind
SET BindStatus = @UnboundStatus, UnbindTime = GETDATE()
WHERE ProductTypeId = @ProductTypeId
  AND LineId = @LineId
  AND FlowCode = @FlowCode
  AND StationId = @StationId
  AND BindMaterialName = @MaterialName
  AND BindStatus = @BoundStatus";

            await conn.ExecuteAsync(unbindSql, new
            {
                ctx.ProductTypeId,
                ctx.LineId,
                FlowCode = flowCode,
                ctx.StationId,
                MaterialName = materialName,
                UnboundStatus = BindStatusConstants.UnboundHistory,
                BoundStatus = BindStatusConstants.Bound
            }, tran);

            const string bindSql = @"
INSERT INTO report_MaterialBind
(FlowCode, BindMaterialCode, BindMaterialName, BindStatus, BindTime, ProductTypeId, LineId, StationId)
VALUES
(@FlowCode, @MaterialCode, @MaterialName, @BoundStatus, GETDATE(), @ProductTypeId, @LineId, @StationId)";

            await conn.ExecuteAsync(bindSql, new
            {
                FlowCode = flowCode,
                MaterialCode = motorCode,
                MaterialName = materialName,
                BoundStatus = BindStatusConstants.Bound,
                ctx.ProductTypeId,
                ctx.LineId,
                ctx.StationId
            }, tran);
        }

        #endregion

        #region ===================== 解析与上下文 =====================

        /// <summary>
        /// 解析绑定物料显示名称：优先工位物料配置中的「电机码」，否则用 appsettings 默认值。
        /// </summary>
        private string ResolveMotorMaterialName(ResolvedContext ctx)
        {
            if (ctx.StationId > 0 && ctx.ProductTypeId > 0)
            {
                var stationMaterials = _cache.GetData<List<material_Station>>();
                var materials = _cache.GetData<List<material_Info>>();

                if (stationMaterials != null && materials != null)
                {
                    var linked = stationMaterials
                        .Where(s => s.StationId == ctx.StationId
                                    && s.TypeId == ctx.ProductTypeId
                                    && (ctx.LineId <= 0 || s.LineId == ctx.LineId))
                        .Select(s => materials.FirstOrDefault(m => m.Id == s.MaterialId))
                        .Where(m => m != null)
                        .Select(m => m!)
                        .FirstOrDefault(m => m.Name.Equals(_options.DefaultMotorMaterialName, StringComparison.OrdinalIgnoreCase)
                                          || m.Name.Contains(_options.DefaultMotorMaterialName, StringComparison.OrdinalIgnoreCase));

                    if (linked != null && !string.IsNullOrWhiteSpace(linked.Name))
                        return linked.Name;
                }
            }

            return _options.DefaultMotorMaterialName;
        }

        /// <summary> 解析 Win7 DataContent JSON：每项 { "Value": "..." } 转为一条测点 </summary>
        private static List<ParsedDataItem> ParseDataContent(string? dataContent)
        {
            var list = new List<ParsedDataItem>();
            if (string.IsNullOrWhiteSpace(dataContent))
                return list;

            using var doc = JsonDocument.Parse(dataContent);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.Object when prop.Value.TryGetProperty("Value", out var v) => v.ToString(),
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    _ => prop.Value.GetRawText()
                };

                list.Add(new ParsedDataItem(prop.Name, value ?? string.Empty));
            }

            return list;
        }

        /// <summary> 从已解析测点中提取电机码字段（字段名见 MotorNumberFieldName 配置） </summary>
        private string? ExtractMotorCode(IReadOnlyList<ParsedDataItem> items)
        {
            var field = _options.MotorNumberFieldName;
            return items.FirstOrDefault(i => i.DataName.Equals(field, StringComparison.OrdinalIgnoreCase))?.DataValue;
        }

        /// <summary> 判断是否可写入工位状态（Id 与流水码均须有效） </summary>
        private static bool CanWriteProductStationStatus(ResolvedContext ctx, string flowCode) =>
            !string.IsNullOrWhiteSpace(flowCode)
            && ctx.StationId > 0
            && ctx.ProductTypeId > 0
            && ctx.LineId > 0;

        /// <summary> 解析 Web 保存结果：1=合格，2=不合格（与老库 ProductResult 一致） </summary>
        private static int ResolveProductStatus(string? productResult, IReadOnlyList<ParsedDataItem> parsed)
        {
            if (TryMapProductStatus(productResult, out var fromParam))
                return fromParam;

            var finalResult = parsed.FirstOrDefault(i =>
                    i.DataName.Equals("Final_Result", StringComparison.OrdinalIgnoreCase)
                    || i.DataName.Equals("_ProductResult", StringComparison.OrdinalIgnoreCase))
                ?.DataValue;

            if (TryMapProductStatus(finalResult, out var fromJson))
                return fromJson;

            return 2; // 无法解析时默认不合格
        }

        /// <summary> 将 OK/NG/1/2 等文本映射为 Status（1=合格，2=不合格） </summary>
        private static bool TryMapProductStatus(string? raw, out int status)
        {
            status = 2;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var text = raw.Trim();
            if (int.TryParse(text, out var numeric))
            {
                status = numeric == 1 ? 1 : 2;
                return true;
            }

            if (text.Equals("OK", StringComparison.OrdinalIgnoreCase)
                || text.Equals("PASS", StringComparison.OrdinalIgnoreCase))
            {
                status = 1;
                return true;
            }

            if (text.Equals("NG", StringComparison.OrdinalIgnoreCase)
                || text.Equals("FAIL", StringComparison.OrdinalIgnoreCase))
            {
                status = 2;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 合并 appsettings 固定 Id 与缓存动态解析。
        /// 工位 Id 优先级：配置 StationId &gt; SOAP stationName &gt; StationNameHint（如 OP090）&gt; 0。
        /// </summary>
        private ResolvedContext ResolveContext(string? stationName, string? productTypeCode)
        {
            var ctx = new ResolvedContext
            {
                StationId = _options.StationId,
                LineId = _options.LineId,
                ProductTypeId = _options.ProductTypeId
            };

            // 1. SOAP 报文工位名（UploadDataWithInfo 等）
            if (ctx.StationId <= 0)
                ctx.StationId = TryResolveStationId(stationName);

            // 2. appsettings StationNameHint（GetMotorNumber 等无工位名接口 → 写入 OP090 目录而非 0）
            if (ctx.StationId <= 0 && !string.IsNullOrWhiteSpace(_options.StationNameHint))
                ctx.StationId = TryResolveStationId(_options.StationNameHint);

            if (ctx.ProductTypeId <= 0)
                ctx.ProductTypeId = TryResolveProductTypeId(productTypeCode);

            // 产线未配置时，尝试从已解析工位带出 LineId
            if (ctx.LineId <= 0 && ctx.StationId > 0)
            {
                var stations = _cache.GetData<List<craft_StationInfo>>();
                var station = stations?.FirstOrDefault(s => s.Id == ctx.StationId);
                if (station != null)
                    ctx.LineId = station.LineId;
            }

            return ctx;
        }

        /// <summary> 按工位代号或名称匹配 craft_StationInfo（如 OP090）；无匹配返回 0 </summary>
        private int TryResolveStationId(string? stationName)
        {
            if (string.IsNullOrWhiteSpace(stationName))
                return 0;

            var stations = _cache.GetData<List<craft_StationInfo>>();
            if (stations == null || stations.Count == 0)
                return 0;

            var key = stationName.Trim();
            var match = stations.FirstOrDefault(s =>
                s.Code.Equals(key, StringComparison.OrdinalIgnoreCase)
                || s.Name.Equals(key, StringComparison.OrdinalIgnoreCase));

            return match?.Id ?? 0;
        }

        /// <summary> 按型号名称匹配 craft_TypeInfo（如 DT01）；无匹配返回 0 </summary>
        private int TryResolveProductTypeId(string? productTypeCode)
        {
            if (string.IsNullOrWhiteSpace(productTypeCode))
                return 0;

            var types = _cache.GetData<List<craft_TypeInfo>>();
            if (types == null || types.Count == 0)
                return 0;

            var key = productTypeCode.Trim();
            var match = types.FirstOrDefault(t =>
                t.Name.Equals(key, StringComparison.OrdinalIgnoreCase));

            return match?.Id ?? 0;
        }

        /// <summary> 查询 report_ProcessHistory 中某测点最近一次 DataValue </summary>
        private string? QueryLastDataValue(string flowCode, int stationId, string dataName)
        {
            // StationId=0 时不按工位过滤，避免无配置时查不到数据
            var sql = stationId > 0
                ? @"
SELECT TOP 1 DataValue FROM report_ProcessHistory
WHERE FlowCode = @FlowCode AND StationId = @StationId AND DataName = @DataName
ORDER BY Id DESC"
                : @"
SELECT TOP 1 DataValue FROM report_ProcessHistory
WHERE FlowCode = @FlowCode AND DataName = @DataName
ORDER BY Id DESC";

            return _sql.QuerySingleAsync<string?>(sql, new
            {
                FlowCode = flowCode,
                StationId = stationId,
                DataName = dataName
            }).GetAwaiter().GetResult();
        }

        #endregion

        #region ===================== 日志 =====================

        /// <summary>
        /// 双写日志：Application（source=ExternalWebStation）+ Device（按 StationId 分目录）。
        /// 工位 Id 由 ResolveContext 解析；配置了 StationNameHint=OP090 时不再写入 Device/0。
        /// </summary>
        private void Log(string? productType, string? stationName, string message, int stationId = -1)
        {
            var suffix = string.IsNullOrEmpty(stationName) ? string.Empty : $" [{stationName}]";
            _logger.Info($"<ExternalWebStation>{suffix} {message}", LogSource);

            var logStationId = stationId >= 0 ? stationId : ResolveContext(stationName, productType).StationId;
            _logger.DeviceLog(logStationId, VirtualDeviceCode, message);
        }

        /// <summary> 记录失败并返回 Win7 可识别的 NG: 前缀字符串 </summary>
        private string Fail(string? productType, string? stationName, string message, Exception? ex = null)
        {
            var text = ex == null ? message : $"{message} | {ex.Message}";
            Log(productType, stationName, text);
            if (ex != null)
                _logger.Error(ex, text, LogSource);

            return "NG: " + text;
        }

        #endregion

        #region ===================== 内部类型 =====================

        /// <summary> DataContent JSON 解析后的单个测点 </summary>
        private sealed record ParsedDataItem(string DataName, string DataValue);

        /// <summary> 一次 SOAP 请求解析出的工位/型号/产线 Id（允许为 0） </summary>
        private sealed class ResolvedContext
        {
            public int StationId { get; set; }
            public int ProductTypeId { get; set; }
            public int LineId { get; set; }
        }

        #endregion
    }
}
