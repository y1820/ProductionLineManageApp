using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Core.Services.DeviceManager.Connection
{
    /// <summary> 连接模式策略接口：集中式（共享 PLC）与分布式（独立驱动） </summary>
    public interface IConnectionModeStrategy
    {
        #region ===================== 连接获取与释放 =====================

        /// <summary> 获取或建立物理连接 </summary>
        Task<ConnectionAcquireResult> AcquireAsync(device_ConnectInfo config, CancellationToken token);

        /// <summary> 任务停止时释放驱动资源 </summary>
        void ReleaseOnStop(device_ConnectInfo config, IDeviceCommunication? driver);

        /// <summary> 本工位检测到断线时释放本地驱动（集中式通常为 no-op） </summary>
        void ReleaseOnLocalDisconnect(IDeviceCommunication? driver);

        #endregion
    }
}
