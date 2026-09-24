using System.Collections.Concurrent;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Infrastructure.Logging;
using Prism.Events;

namespace ProductionLineManage.Services.DeviceManager.Business
{
    /// <summary>
    /// 业务中介实现（应用单例）：注册工位交互类型与数据处理器，路由 PLC 消息到对应业务逻辑。
    /// </summary>
    public class DeviceBusinessMediator : IDeviceBusinessMediator
    {
        #region ===================== 私有字段 =====================

        private readonly ILogger _logger;
        private readonly IDataCacheService _cacheService;
        private readonly Dictionary<string, IDeviceDataHandler> _handlers = new();
        private readonly Dictionary<int, IInteractionType> _interactions = new();
        private readonly HashSet<int> _registeredStations = new();
        private readonly object _lockObj = new();

        /// <summary> 按工位 Id 索引的采集配置快照 </summary>
        private ConcurrentDictionary<int, IReadOnlyList<craft_DataCollectConfig>> _collectConfigsByStation =
            new();

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入日志、事件聚合器与缓存，并订阅采集配置更新 </summary>
        public DeviceBusinessMediator(
            ILogger logger,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService)
        {
            _logger = logger;
            _cacheService = cacheService;

            eventAggregator.GetEvent<DataCollectConfigUpdatedEvent>()
                .Subscribe(RebuildCollectConfigIndex, ThreadOption.BackgroundThread);

            if (_cacheService.HasData<List<craft_DataCollectConfig>>())
                RebuildCollectConfigIndex(_cacheService.GetData<List<craft_DataCollectConfig>>()!);
        }

        #endregion

        #region ===================== 处理器注册 =====================

        /// <summary> 注册数据处理器（如流水码、物料、保存等） </summary>
        public void RegisterHandler(string dataType, IDeviceDataHandler handler)
        {
            _handlers[dataType] = handler;
            _logger.Info($"业务中介：注册处理器 {dataType} -> {handler.GetType().Name}");
        }

        /// <summary> 获取已注册的全部数据处理器 </summary>
        public IReadOnlyDictionary<string, IDeviceDataHandler> GetHandlers()
        {
            return _handlers;
        }

        #endregion

        #region ===================== 工位注册 =====================

        /// <summary> 注册工位及其交互类型（CommandLineLogic 等） </summary>
        public Task RegisterStationAsync(int stationId, IInteractionType interaction)
        {
            lock (_lockObj)
            {
                _registeredStations.Add(stationId);
                _interactions[stationId] = interaction;
                _logger.Info($"业务中介：工位 {stationId} 已注册，交互类型={interaction.LogicType}");
            }
            return Task.CompletedTask;
        }

        /// <summary> 注销工位 </summary>
        public Task UnregisterStationAsync(int stationId)
        {
            lock (_lockObj)
            {
                _registeredStations.Remove(stationId);
                _interactions.Remove(stationId);
                _logger.Info($"业务中介：工位 {stationId} 已注销");
            }
            return Task.CompletedTask;
        }

        #endregion

        #region ===================== 消息路由 =====================

        /// <summary> 将 PLC 消息发布到对应工位的交互类型处理 </summary>
        public async Task PublishAsync(DeviceDataMessage message, IDeviceTaskContext context)
        {
            if (!_registeredStations.Contains(message.StationId))
            {
                _logger.DeviceLog(context.StationId, "业务中介",
                    $"工位 {message.StationId} 未注册，忽略消息 [{message.DataType}]", LogLevel.Warning);
                return;
            }

            if (!_interactions.TryGetValue(message.StationId, out var interaction))
            {
                _logger.DeviceLog(context.StationId, "业务中介",
                    $"工位 {message.StationId} 未绑定交互类型，忽略消息 [{message.DataType}]", LogLevel.Warning);
                return;
            }

            await interaction.HandleMessageAsync(message, context);
        }

        #endregion

        #region ===================== 采集配置 =====================

        /// <summary> 获取指定工位与型号的启用采集配置 </summary>
        public IReadOnlyList<craft_DataCollectConfig> GetCollectConfigs(int stationId, int productTypeId)
        {
            if (productTypeId <= 0)
                return Array.Empty<craft_DataCollectConfig>();

            if (!_collectConfigsByStation.TryGetValue(stationId, out var configs) || configs.Count == 0)
                return Array.Empty<craft_DataCollectConfig>();

            return configs
                .Where(c => c.IsEnabled && c.ProductTypeId == productTypeId)
                .ToList();
        }

        /// <summary> 重建按工位分组的采集配置索引 </summary>
        private void RebuildCollectConfigIndex(List<craft_DataCollectConfig> all)
        {
            var grouped = all
                .GroupBy(c => c.StationId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<craft_DataCollectConfig>)g.ToList());

            _collectConfigsByStation = new ConcurrentDictionary<int, IReadOnlyList<craft_DataCollectConfig>>(grouped);
            _logger.Info($"业务中介：加工数据采集配置索引已更新，工位数={grouped.Count}，总配置数={all.Count}");
        }

        #endregion
    }
}
