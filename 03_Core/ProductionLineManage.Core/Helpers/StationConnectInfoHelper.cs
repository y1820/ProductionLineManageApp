using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;

namespace ProductionLineManage.Core.Helpers
{
    /// <summary>
    /// 工位连接信息辅助：从缓存连接信息解析工位协议、交互类型及下拉数据。
    /// </summary>
    public static class StationConnectInfoHelper
    {
        #region ===================== 连接信息查询 =====================

        /// <summary> 获取工位对应的 device_ConnectInfo，未配置则返回 null </summary>
        public static device_ConnectInfo? GetConnectInfo(IDataCacheService cache, int stationId)
        {
            if (!cache.HasData<List<device_ConnectInfo>>())
                return null; // 连接信息尚未加载

            return cache.GetData<List<device_ConnectInfo>>()?.FirstOrDefault(c => c.StationId == stationId);
        }

        /// <summary> 获取工位通讯协议类型 </summary>
        public static string? GetProtocolType(IDataCacheService cache, int stationId) =>
            GetConnectInfo(cache, stationId)?.ProtocolType;

        /// <summary> 获取工位交互类型（指令型 / 信号型） </summary>
        public static string? GetInteractionType(IDataCacheService cache, int stationId) =>
            GetConnectInfo(cache, stationId)?.InteractionType;

        #endregion

        #region ===================== 下拉数据 =====================

        /// <summary> 按工位协议返回可选 PLC 数据类型 </summary>
        public static List<string> GetDataTypes(IDataCacheService cache, int stationId) =>
            DeviceDataTypeConstants.GetDataTypesForProtocol(GetProtocolType(cache, stationId)).ToList();

        /// <summary> 按工位交互类型返回地址映射可选数据名称 </summary>
        public static List<string> GetAddressMappingDataNames(IDataCacheService cache, int stationId) =>
            DataNameConstants.GetDataNamesForInteraction(GetInteractionType(cache, stationId)).ToList();

        #endregion
    }
}
