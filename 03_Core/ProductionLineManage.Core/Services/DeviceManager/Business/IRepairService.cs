using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Core.Services.DeviceManager.Business
{
    /// <summary> 返修服务接口 </summary>
    public interface IRepairService
    {
        #region ===================== 返修查询与确认 =====================

        /// <summary> 返修 200：查询允许返修顺序（无过站记录时 success=false） </summary>
        Task<(bool success, int allowSequence, int repairCount, string message)> QueryAllowRepairSequenceAsync(
            string flowCode, int productTypeId, int lineId);

        /// <summary> 返修 8000：按 Sequence 确认返修，在目标工位创建/重置待过站记录 </summary>
        Task<(bool success, int stationRecordId, int targetStationId, int repairCount, string message)> ConfirmRepairAtSequenceAsync(
            string flowCode,
            string trayCode,
            int productTypeId,
            int lineId,
            int requestSequence,
            int allowSequence,
            int currentRepairCount);

        /// <summary> 获取可返修的工位列表（当前工位之后的所有工位） </summary>
        Task<List<craft_StationInfo>> GetAvailableRepairStationsAsync(int productTypeId, int currentStationId);

        /// <summary> 确认返修（旧版按 StationId，保留供其他模块使用） </summary>
        Task<bool> ConfirmRepairAsync(string flowCode, string trayCode,
            int currentStationId, int targetStationId);

        #endregion

        #region ===================== 返修跳转 =====================

        /// <summary> 获取产品当前应加工的工位（考虑返修跳转） </summary>
        Task<int> GetCurrentProcessingStationAsync(string flowCode, string trayCode, int productTypeId);

        /// <summary> 检查产品是否处于返修跳转状态 </summary>
        Task<bool> IsInRepairJumpAsync(string flowCode, string trayCode);

        #endregion
    }
}
