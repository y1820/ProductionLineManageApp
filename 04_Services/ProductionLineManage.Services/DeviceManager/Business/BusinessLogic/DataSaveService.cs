// ProductionLineManage.Services/DataSaveService.cs
using Dapper;
using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Data.Repository;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.DeviceManager.InteractionType;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace ProductionLineManage.Services.DeviceManager.Business.BusinessLogic
{
    /// <summary>
    /// 保存数据服务实现：校验工位状态与过站明细后，从 PLC 采集工艺数据并写入数据库。
    /// </summary>
    public class DataSaveService : IDataSaveService, IDeviceDataHandler
    {
        #region ===================== 私有字段 =====================

        private readonly ILogger _logger;
        private readonly SQLHelper _sqlHelper;
        private readonly IDeviceBusinessMediator _mediator;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入日志、数据库与业务中介 </summary>
        public DataSaveService(
            ILogger logger,
            SQLHelper sqlHelper,
            IDeviceBusinessMediator mediator)
        {
            _logger = logger;
            _sqlHelper = sqlHelper;
            _mediator = mediator;
        }

        #endregion

        #region ===================== IDeviceDataHandler =====================

        /// <summary> 处理器标识，供 CommandLineLogic 路由 </summary>
        public string DataType => DeviceHandlerKeys.DataSave;

        /// <summary>
        /// 保存数据入口。message.Data 应为 <see cref="SaveDataPayload"/>。
        /// 按 Mediator 提供的 craft_DataCollectConfig 从 PLC 采集后写入 report_ProcessHistory。
        /// </summary>
        public async Task<BusinessResponse> HandleAsync(DeviceDataMessage message, IDeviceTaskContext context)
        {
            var stationId = message.StationId;

            if (message.Data is not SaveDataPayload payload)
            {
                context.Log("<HandleAsync> 失败：缺少 SaveDataPayload", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = "保存数据缺少 SaveDataPayload 参数"
                };
            }

            if (string.IsNullOrWhiteSpace(payload.FlowCode))
            {
                context.Log("<HandleAsync> 失败：流水码为空", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = "流水码为空，无法保存"
                };
            }

            if (payload.ProductTypeId <= 0)
            {
                context.Log("<HandleAsync> 失败：产品型号 Id 无效（<=0）", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = "产品型号 Id 无效，无法保存加工数据"
                };
            }

            if (payload.StationRecordId <= 0)
            {
                context.Log("<HandleAsync> 失败：工位状态 Id 无效", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = "工位状态 Id 无效，请先完成流水码验证（200）"
                };
            }

            // 返修保存时校验目标工位，否则校验当前工位
            var expectedStationId = payload.IsRepairSave ? payload.RepairTargetStationId : stationId;
            context.Log($"<HandleAsync> 开始保存，流水码={payload.FlowCode}，型号Id={payload.ProductTypeId}，产线Id={payload.LineId}，工位状态Id={payload.StationRecordId}，期望工位Id={expectedStationId}，状态={payload.Status}");

            var statusCheck = await ValidateProductStationStatusAsync(
                payload.StationRecordId,
                payload.FlowCode,
                expectedStationId);
            if (!statusCheck.ok)
            {
                context.Log($"<HandleAsync> 失败：{statusCheck.message}", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = statusCheck.message
                };
            }

            var pendingCheck = await ValidatePendingPassRecordAsync(
                payload.FlowCode,
                expectedStationId,
                payload.ProductTypeId,
                payload.LineId);
            if (!pendingCheck.ok)
            {
                context.Log($"<HandleAsync> 失败：{pendingCheck.message}", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = pendingCheck.message
                };
            }

            var collectConfigs = _mediator.GetCollectConfigs(stationId, payload.ProductTypeId);
            List<report_ProcessHistory> processData;
            if (collectConfigs.Count == 0)
            {
                context.Log("<HandleAsync> 无采集配置，跳过工艺数据采集");
                processData = new List<report_ProcessHistory>();
            }
            else
            {
                context.Log($"<HandleAsync> 执行中，匹配采集配置 {collectConfigs.Count} 项，开始读取 PLC...");
                processData = await CollectProcessDataAsync(
                    context, stationId, payload.ProductTypeId, payload.LineId, collectConfigs);
                context.Log($"<HandleAsync> 执行中，采集完成 {processData.Count} 项，调用 SaveProductionData...");
            }

            var materialCodes = payload.Materials.Count > 0
                ? payload.Materials.Select(m => (m.MaterialCode, m.MaterialName)).ToList()
                : null;
            var isQualified = SaveStatusHelper.ResolveIsQualifiedForHistory(
                processData.Select(d => (d.DataName, d.DataValue)),
                payload.Status);
            var (result, mes) = await SaveProductionDataAsync(
                payload.FlowCode,
                stationId,
                payload.TrayCode,   
                payload.ProductTypeId,
                payload.LineId,
                processData,
                payload.Status,
                payload.StationRecordId,
                pendingCheck.passRecordId,
                materialCodes,
                payload.IsRepairSave,
                payload.RepairCount,
                payload.RepairTargetStationId,
                payload.IsRepairProcess,
                isQualified);

            var response = new BusinessResponse
            {
                StationId = stationId,
                Success = result,
                Message = mes,
                Data = payload.FlowCode
            };

            context.Log($"<HandleAsync> 完成，结果={result}，{mes}");
            return response;
        }

        #endregion

        #region ===================== 私有方法 =====================

        #region --------------------- PLC 采集 ---------------------

        /// <summary> 按采集配置逐项从 PLC 读取工艺数据 </summary>
        private async Task<List<report_ProcessHistory>> CollectProcessDataAsync(
            IDeviceTaskContext context,
            int stationId,
            int productTypeId,
            int lineId,
            IReadOnlyList<craft_DataCollectConfig> configs)
        {
            var result = new List<report_ProcessHistory>();

            foreach (var config in configs)
            {
                if (string.IsNullOrWhiteSpace(config.Address))
                {
                    context.Log($"<CollectProcessData> 跳过：{config.DataName} 地址为空", LogLevel.Warning);
                    continue;
                }

                var raw = await context.ReadAsync(config.Address, config.DataType, config.DataLength);
                if (raw == null)
                {
                    context.Log($"<CollectProcessData> 跳过读取失败：{config.DataName}（{config.Address}）", LogLevel.Warning);
                    continue;
                }

                result.Add(new report_ProcessHistory
                {
                    StationId = stationId,
                    DataName = config.DataName,
                    DataValue = raw.ToString() ?? string.Empty,
                    DataType = config.DataType,
                    DataUnit = config.DataUnit,
                    ProductTypeId = productTypeId,
                    LineId = lineId
                });
            }

            return result;
        }

        #endregion

        #region --------------------- 保存前校验 ---------------------

        /// <summary> 校验工位状态记录与流水码、工位是否一致 </summary>
        private async Task<(bool ok, string message)> ValidateProductStationStatusAsync(
            int statusId, string flowCode, int expectedStationId)
        {
            const string sql = @"
                SELECT FlowCode, StationId
                FROM production_ProductStationStatus
                WHERE Id = @Id";

            var record = await _sqlHelper.QuerySingleAsync<ProductStationStatusRow>(sql, new { Id = statusId });
            if (record == null)
                return (false, "工位状态不存在");

            if (!string.Equals(record.FlowCode, flowCode, StringComparison.OrdinalIgnoreCase))
                return (false, "工位状态流水码与当前产品不一致");

            if (record.StationId != expectedStationId)
                return (false, "工位状态工位与期望工位不一致");

            return (true, string.Empty);
        }

        /// <summary> 校验是否存在未结束的过站明细，并返回其 Id </summary>
        private async Task<(bool ok, int passRecordId, string message)> ValidatePendingPassRecordAsync(
            string flowCode, int expectedStationId, int productTypeId, int lineId)
        {
            const string sql = @"
                SELECT TOP 1 Id, FlowCode, StationId, EndTime
                FROM report_StationPassRecord
                WHERE FlowCode = @FlowCode
                  AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId
                  AND LineId = @LineId
                  AND EndTime IS NULL
                ORDER BY Id DESC";

            var record = await _sqlHelper.QuerySingleAsync<PendingPassRecordRow>(sql, new
            {
                FlowCode = flowCode,
                StationId = expectedStationId,
                ProductTypeId = productTypeId,
                LineId = lineId
            });
            if (record == null)
                return (false, 0, "未找到未结束的过站明细，不能保存");

            if (!string.Equals(record.FlowCode, flowCode, StringComparison.OrdinalIgnoreCase))
                return (false, 0, "过站明细流水码与当前产品不一致");

            if (record.StationId != expectedStationId)
                return (false, 0, "过站明细工位与期望工位不一致");

            return (true, record.Id, string.Empty);
        }

        #endregion

        #region --------------------- 内部 DTO ---------------------

        /// <summary> 工位状态查询行 </summary>
        private sealed class ProductStationStatusRow
        {
            public string FlowCode { get; set; } = string.Empty;
            public int StationId { get; set; }
        }

        /// <summary> 未结束过站明细查询行 </summary>
        private sealed class PendingPassRecordRow
        {
            public int Id { get; set; }
            public string FlowCode { get; set; } = string.Empty;
            public int StationId { get; set; }
            public DateTime? EndTime { get; set; }
        }

        #endregion

        #endregion

        #region ===================== 公共方法 =====================

        /// <summary>
        /// 保存生产数据（带事务）：更新工位状态/过站明细，写入工艺历史与物料绑定。
        /// </summary>
        public async Task<(bool result, string mes)> SaveProductionDataAsync(string flowCode, int stationId, string trayCode,
            int productTypeId, int lineId, List<report_ProcessHistory> datas, int status, int productStationStatusId,
            int passRecordId,
            List<(string materialCode, string materialName)>? materialCodes = null,
            bool isRepair = false,
            int repairCount = 0,
            int repairTargetStationId = 0,
            bool isRepairProcess = false,
            bool isQualified = true)
        {
            try
            {
                await _sqlHelper.ExecuteInTransactionAsync(async (connection, transaction) =>
                {
                    var historyIsRepair = isRepair || isRepairProcess;
                    var historyRepairCount = historyIsRepair ? repairCount : 0;
                    if (isRepair)
                    {
                        // 返修保存：目标工位保持 Status=0，仅更新返修标记
                        var targetStationId = repairTargetStationId > 0 ? repairTargetStationId : stationId;
                        const string repairStatusSql = @"
                    UPDATE production_ProductStationStatus
                    SET UpdateTime = GETDATE(),
                        IsRepair = 1,
                        RepairCount = @RepairCount,
                        RepairTargetStationId = @RepairTargetStationId
                    WHERE FlowCode = @FlowCode
                      AND StationId = @RepairTargetStationId
                      AND ProductTypeId = @ProductTypeId
                      AND LineId = @LineId
                      AND Status = 0";
                        var repairRows = await connection.ExecuteAsync(repairStatusSql,
                            new
                            {
                                FlowCode = flowCode,
                                RepairTargetStationId = targetStationId,
                                ProductTypeId = productTypeId,
                                LineId = lineId,
                                RepairCount = repairCount
                            }, transaction);
                        if (repairRows == 0)
                            throw new InvalidOperationException(
                                $"返修工位状态更新失败，目标工位={targetStationId} 可能已非待过站状态");
                        _logger.DeviceLog(stationId, "",
                            $"<SaveProductionData> 返修保存：目标工位={targetStationId} 保持 Status=0，RepairCount={repairCount}，工位状态 Id={productStationStatusId}，过站明细 Id={passRecordId}");
                    }
                    else
                    {
                        // 正常保存：结束过站明细并更新工位状态
                        const string closePassSql = @"
                    UPDATE report_StationPassRecord
                    SET EndTime = GETDATE(), UpdateTime = GETDATE()
                    WHERE Id = @Id AND EndTime IS NULL";
                        var passRows = await connection.ExecuteAsync(closePassSql,
                            new { Id = passRecordId }, transaction);
                        if (passRows == 0)
                            throw new InvalidOperationException(
                                $"过站明细结束失败，Id={passRecordId} 可能已结束或不存在");
                        const string updateStatusSql = @"
                    UPDATE production_ProductStationStatus
                    SET Status = @Status, UpdateTime = GETDATE(),
                        IsRepair = 0, RepairTargetStationId = 0
                    WHERE FlowCode = @FlowCode
                      AND StationId = @StationId
                      AND ProductTypeId = @ProductTypeId
                      AND LineId = @LineId";
                        await connection.ExecuteAsync(updateStatusSql,
                            new
                            {
                                Status = status,
                                FlowCode = flowCode,
                                StationId = stationId,
                                ProductTypeId = productTypeId,
                                LineId = lineId
                            }, transaction);
                        _logger.DeviceLog(stationId, "", $"<SaveProductionData> 过站明细已结束 Id={passRecordId}，工位状态 Id={productStationStatusId} Status={status}");
                    }

                    if (datas != null && datas.Any())
                    {
                        var insertSql = @"
                        INSERT INTO report_ProcessHistory 
                        (StationRecordId, FlowCode, TrayCode, StationId, ProductTypeId, LineId, DataName, DataValue, DataType, DataUnit, IsRepair, RepairCount, IsQualified, CreateTime, UpdateTime)
                        VALUES (@StationRecordId, @FlowCode, @TrayCode, @StationId, @ProductTypeId, @LineId, @DataName, @DataValue, @DataType, @DataUnit, @IsRepair, @RepairCount, @IsQualified, GETDATE(), GETDATE())";
                        foreach (var data in datas)
                        {
                            await connection.ExecuteAsync(insertSql, new
                            {
                                StationRecordId = productStationStatusId,
                                FlowCode = flowCode,
                                TrayCode = trayCode,
                                StationId = stationId,
                                ProductTypeId = data.ProductTypeId > 0 ? data.ProductTypeId : productTypeId,
                                LineId = data.LineId > 0 ? data.LineId : lineId,
                                data.DataName,
                                data.DataValue,
                                data.DataType,
                                DataUnit = data.DataUnit ?? string.Empty,
                                IsRepair = historyIsRepair ? 1 : 0,
                                RepairCount = historyRepairCount,
                                IsQualified = isQualified ? 1 : 0
                            }, transaction);
                        }
                        _logger.DeviceLog(stationId, "", $"<SaveProductionData> 工艺数据保存完成，数量: {datas.Count}");
                    }

                    if (materialCodes != null && materialCodes.Any())
                    {
                        // 先解绑本工位旧物料，再插入新绑定
                        var unbindSql = @"
                            UPDATE report_MaterialBind 
                            SET BindStatus = @UnboundStatus, UnbindTime = GETDATE()
                            WHERE ProductTypeId = @ProductTypeId
                              AND LineId = @LineId
                              AND FlowCode = @FlowCode
                              AND StationId = @StationId
                              AND BindStatus = @BoundStatus";

                        var unbindCount = await connection.ExecuteAsync(unbindSql, new
                        {
                            ProductTypeId = productTypeId,
                            LineId = lineId,
                            FlowCode = flowCode,
                            StationId = stationId,
                            UnboundStatus = BindStatusConstants.UnboundHistory,
                            BoundStatus = BindStatusConstants.Bound
                        }, transaction);
                        if (unbindCount > 0)
                            _logger.DeviceLog(stationId, "", $"<SaveProductionData> 已解绑本工位旧物料绑定 {unbindCount} 条");

                        foreach (var material in materialCodes)
                        {
                            var bindSql = @"
                            INSERT INTO report_MaterialBind 
                            (FlowCode, BindMaterialCode, BindMaterialName, BindStatus, BindTime, ProductTypeId, LineId, StationId)
                            VALUES (@FlowCode, @MaterialCode, @MaterialName, @BoundStatus, GETDATE(), @ProductTypeId, @LineId, @StationId)";

                            var bindCount = await connection.ExecuteAsync(bindSql, new
                            {
                                FlowCode = flowCode,
                                MaterialCode = material.materialCode,
                                MaterialName = material.materialName,
                                BoundStatus = BindStatusConstants.Bound,
                                ProductTypeId = productTypeId,
                                LineId = lineId,
                                StationId = stationId
                            }, transaction);
                            if (bindCount > 0)
                                _logger.DeviceLog(stationId, "", $"<SaveProductionData> 已绑定物料：{material.materialName} - {material.materialCode}");
                        }
                        _logger.DeviceLog(stationId, "", $"<SaveProductionData> 物料绑定完成，数量: {materialCodes.Count}");
                    }
                });

                return (true, "保存成功");
            }
            catch (Exception ex)
            {
                return (false, $"保存数据失败: {ex.Message}");
            }
        }

        #endregion
    }
}
