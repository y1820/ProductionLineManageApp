using Dapper;
using ProductionLineManage.Infrastructure.Data.Repository;
using System.Data;

namespace ProductionLineManage.Services.Production
{
    /// <summary>
    /// 产品工位状态与过站明细的协同写入（200/8000/返修）。
    /// 供 FlowCodeService、RepairService、ExternalWebStationService 等调用。
    /// </summary>
    internal static class StationRecordOperations
    {
        #region ===================== 200 流水码验证 =====================

        /// <summary>
        /// 创建或更新待过站工位状态（Status=0）。
        /// 若已有返修标记则保留 IsRepair/RepairTargetStationId/RepairCount。
        /// </summary>
        public static async Task UpsertPendingProductStatusAsync(
            SQLHelper sql,
            string flowCode,
            string trayCode,
            int stationId,
            int productTypeId,
            int lineId)
        {
            const string sqlText = """
                UPDATE production_ProductStationStatus
                SET Status = 0, UpdateTime = GETDATE(), TrayCode = @TrayCode,
                    IsRepair = CASE WHEN IsRepair = 1 THEN 1 ELSE 0 END,
                    RepairTargetStationId = CASE WHEN IsRepair = 1 THEN RepairTargetStationId ELSE 0 END,
                    RepairCount = CASE WHEN IsRepair = 1 THEN RepairCount ELSE 0 END
                WHERE FlowCode = @FlowCode AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId AND LineId = @LineId;

                IF @@ROWCOUNT = 0
                INSERT INTO production_ProductStationStatus
                (FlowCode, TrayCode, ProductTypeId, LineId, StationId, Status,
                 IsRepair, RepairTargetStationId, RepairCount, CreateTime, UpdateTime)
                VALUES (@FlowCode, @TrayCode, @ProductTypeId, @LineId, @StationId, 0,
                        0, 0, 0, GETDATE(), GETDATE());
                """;

            await sql.ExecuteAsync(sqlText, new
            {
                FlowCode = flowCode,
                TrayCode = trayCode,
                StationId = stationId,
                ProductTypeId = productTypeId,
                LineId = lineId
            });
        }

        /// <summary>
        /// 创建或刷新未结束的过站明细（EndTime IS NULL），返回其 Id。
        /// </summary>
        public static async Task<int> CreateOrRefreshOpenPassRecordAsync(
            SQLHelper sql,
            string flowCode,
            string trayCode,
            int stationId,
            int productTypeId,
            int lineId)
        {
            const string refreshSql = @"
                UPDATE report_StationPassRecord
                SET StartTime = GETDATE(), UpdateTime = GETDATE(), TrayCode = @TrayCode
                WHERE FlowCode = @FlowCode AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId AND LineId = @LineId
                  AND EndTime IS NULL;

                IF @@ROWCOUNT = 0
                INSERT INTO report_StationPassRecord
                (FlowCode, TrayCode, ProductTypeId, LineId, StationId, StartTime, CreateTime, UpdateTime)
                VALUES (@FlowCode, @TrayCode, @ProductTypeId, @LineId, @StationId, GETDATE(), GETDATE(), GETDATE());";

            await sql.ExecuteAsync(refreshSql, new
            {
                FlowCode = flowCode,
                TrayCode = trayCode,
                StationId = stationId,
                ProductTypeId = productTypeId,
                LineId = lineId
            });

            const string selectSql = @"
                SELECT TOP 1 Id FROM report_StationPassRecord
                WHERE FlowCode = @FlowCode AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId AND LineId = @LineId
                  AND EndTime IS NULL
                ORDER BY Id DESC";

            return await sql.QuerySingleAsync<int?>(selectSql, new
            {
                FlowCode = flowCode,
                StationId = stationId,
                ProductTypeId = productTypeId,
                LineId = lineId
            }) ?? 0;
        }

        #endregion

        #region ===================== 查询 =====================

        /// <summary> 查询工位状态 Id（SQLHelper 版本） </summary>
        public static async Task<int> GetProductStationStatusIdAsync(
            SQLHelper sql,
            string flowCode,
            int stationId,
            int productTypeId,
            int lineId)
        {
            const string selectSql = @"
                SELECT TOP 1 Id FROM production_ProductStationStatus
                WHERE FlowCode = @FlowCode AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId AND LineId = @LineId
                ORDER BY Id DESC";

            return await sql.QuerySingleAsync<int?>(selectSql, new
            {
                FlowCode = flowCode,
                StationId = stationId,
                ProductTypeId = productTypeId,
                LineId = lineId
            }) ?? 0;
        }

        /// <summary> 查询工位状态 Id（事务内 Connection 版本） </summary>
        public static async Task<int> GetProductStationStatusIdAsync(
            IDbConnection conn,
            IDbTransaction? tran,
            string flowCode,
            int stationId,
            int productTypeId,
            int lineId)
        {
            const string selectSql = @"
                SELECT TOP 1 Id FROM production_ProductStationStatus
                WHERE FlowCode = @FlowCode AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId AND LineId = @LineId
                ORDER BY Id DESC";

            return await conn.QuerySingleOrDefaultAsync<int?>(selectSql, new
            {
                FlowCode = flowCode,
                StationId = stationId,
                ProductTypeId = productTypeId,
                LineId = lineId
            }, tran) ?? 0;
        }

        #endregion

        #region ===================== Web 工位保存 =====================

        /// <summary>
        /// Web 工位（如 OP090）保存：直接写入最终合格/不合格状态，不过站明细。
        /// </summary>
        public static async Task<int> UpsertWebSaveProductStatusAsync(
            IDbConnection conn,
            IDbTransaction tran,
            string flowCode,
            string trayCode,
            int stationId,
            int productTypeId,
            int lineId,
            int status)
        {
            const string sqlText = @"
                UPDATE production_ProductStationStatus
                SET Status = @Status, TrayCode = @TrayCode, UpdateTime = GETDATE(),
                    IsRepair = 0, RepairTargetStationId = 0, RepairCount = 0
                WHERE FlowCode = @FlowCode AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId AND LineId = @LineId;

                IF @@ROWCOUNT = 0
                INSERT INTO production_ProductStationStatus
                (FlowCode, TrayCode, ProductTypeId, LineId, StationId, Status,
                 IsRepair, RepairTargetStationId, RepairCount, CreateTime, UpdateTime)
                VALUES (@FlowCode, @TrayCode, @ProductTypeId, @LineId, @StationId, @Status,
                        0, 0, 0, GETDATE(), GETDATE());";

            await conn.ExecuteAsync(sqlText, new
            {
                FlowCode = flowCode,
                TrayCode = trayCode,
                StationId = stationId,
                ProductTypeId = productTypeId,
                LineId = lineId,
                Status = status
            }, tran);

            return await GetProductStationStatusIdAsync(
                conn, tran, flowCode, stationId, productTypeId, lineId);
        }

        #endregion

        #region ===================== 返修 =====================

        /// <summary>
        /// 创建或更新返修目标工位状态（Status=0，IsRepair=1）。
        /// </summary>
        public static async Task UpsertRepairTargetStatusAsync(
            SQLHelper sql,
            string flowCode,
            string trayCode,
            int targetStationId,
            int productTypeId,
            int lineId,
            int repairCount)
        {
            const string sqlText = @"
                UPDATE production_ProductStationStatus
                SET Status = 0, UpdateTime = GETDATE(), TrayCode = @TrayCode,
                    IsRepair = 1, RepairTargetStationId = @TargetStationId, RepairCount = @RepairCount
                WHERE FlowCode = @FlowCode AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId AND LineId = @LineId;

                IF @@ROWCOUNT = 0
                INSERT INTO production_ProductStationStatus
                (FlowCode, TrayCode, ProductTypeId, LineId, StationId, Status,
                 IsRepair, RepairTargetStationId, RepairCount, CreateTime, UpdateTime)
                VALUES (@FlowCode, @TrayCode, @ProductTypeId, @LineId, @StationId, 0,
                        1, @TargetStationId, @RepairCount, GETDATE(), GETDATE());";

            await sql.ExecuteAsync(sqlText, new
            {
                FlowCode = flowCode,
                TrayCode = trayCode,
                StationId = targetStationId,
                TargetStationId = targetStationId,
                ProductTypeId = productTypeId,
                LineId = lineId,
                RepairCount = repairCount
            });
        }

        #endregion
    }
}
