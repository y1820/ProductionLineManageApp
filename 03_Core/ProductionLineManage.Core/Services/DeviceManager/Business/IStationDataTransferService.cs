using ProductionLineManage.Core.Models.Device;

namespace ProductionLineManage.Core.Services.DeviceManager.Business
{
    /// <summary> 工位间数据传递服务：从 report_ProcessHistory 取源工位数据，200 验证后写入请求工位 PLC </summary>
    public interface IStationDataTransferService
    {
        #region ===================== 传值查询 =====================

        /// <summary>
        /// 按请求工位、型号、产线、流水码匹配配置，并从历史表读取待写入 PLC 的数据。
        /// 无配置或均无历史数据时返回空列表（不抛错）。
        /// </summary>
        Task<IReadOnlyList<StationTransferWriteItem>> GetTransferWritesAsync(
            int requestStationId,
            int productTypeId,
            int lineId,
            string flowCode);

        #endregion
    }
}
