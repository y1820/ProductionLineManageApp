using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 连接模式策略工厂：按 ConnectionMode 创建集中式或分布式策略。
    /// </summary>
    public static class ConnectionModeStrategyFactory
    {
        #region ===================== 工厂方法 =====================

        /// <summary> 根据工位配置的连接模式创建对应 IConnectionModeStrategy </summary>
        public static IConnectionModeStrategy Create(
            device_ConnectInfo config,
            ISharedDriverPool sharedDriverPool,
            ILogger logger)
        {
            return config.ConnectionMode == ConnectTypeConstants.Centralized
                ? new CentralizedConnectionStrategy(sharedDriverPool, logger) // 集成式：共享 PlcSession
                : new DistributedConnectionStrategy(logger); // 分布式：每工位独立驱动
        }

        #endregion
    }
}
