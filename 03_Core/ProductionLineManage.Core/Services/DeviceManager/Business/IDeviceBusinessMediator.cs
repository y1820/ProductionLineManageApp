using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;

namespace ProductionLineManage.Core.Services.DeviceManager.Business
{
    /// <summary>
    /// 业务中介接口。
    /// 职责：维护全局 Handler 字典、工位→交互类型映射，并将 PLC 消息路由至 IInteractionType。
    /// 不负责：创建 IInteractionType（由 DeviceConnectionTask + Factory 负责）、StationWorkState（由交互实例私有持有）。
    /// </summary>
    public interface IDeviceBusinessMediator
    {
        #region ===================== 工位注册 =====================

        /// <summary> 注册工位及其交互类型（工位连接就绪后调用一次） </summary>
        Task RegisterStationAsync(int stationId, IInteractionType interaction);

        /// <summary> 注销工位（StopAsync 时调用，同时移除交互类型引用） </summary>
        Task UnregisterStationAsync(int stationId);

        #endregion

        #region ===================== 处理器注册 =====================

        /// <summary> 注册全局业务处理器（应用启动时注册，所有工位共用） </summary>
        void RegisterHandler(string dataType, IDeviceDataHandler handler);

        /// <summary> 供 InteractionTypeFactory 获取已注册的业务处理器 </summary>
        IReadOnlyDictionary<string, IDeviceDataHandler> GetHandlers();

        #endregion

        #region ===================== 消息路由 =====================

        /// <summary> 转发 PLC 数据消息（DeviceConnectionTask 采集/订阅回调调用） </summary>
        Task PublishAsync(DeviceDataMessage message, IDeviceTaskContext context);

        #endregion

        #region ===================== 采集配置 =====================

        /// <summary> 获取指定工位、型号下启用的加工数据采集配置（productTypeId &lt;= 0 时返回空列表） </summary>
        IReadOnlyList<craft_DataCollectConfig> GetCollectConfigs(int stationId, int productTypeId);

        #endregion
    }
}
