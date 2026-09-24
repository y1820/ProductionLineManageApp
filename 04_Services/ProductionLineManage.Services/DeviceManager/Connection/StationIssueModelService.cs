using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 工位型号下发：连接后首次下发、全局 IssueModelEvent 响应、是否已由 PLC 决定型号。
    /// </summary>
    internal sealed class StationIssueModelService
    {
        #region ===================== 字段 =====================

        /// <summary> 工位连接配置 </summary>
        private readonly device_ConnectInfo _config;

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        /// <summary> 型号缓存 </summary>
        private readonly IDataCacheService _cacheService;

        /// <summary> 地址映射查询 </summary>
        private readonly StationAcquisition _acquisition;

        /// <summary> 获取当前驱动 </summary>
        private readonly Func<IDeviceCommunication?> _getDriver;

        /// <summary> 是否已完成首次型号处理（下发或确认由 PLC 决定） </summary>
        private bool _issued;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建型号下发服务 </summary>
        public StationIssueModelService(
            device_ConnectInfo config,
            ILogger logger,
            IDataCacheService cacheService,
            StationAcquisition acquisition,
            Func<IDeviceCommunication?> getDriver)
        {
            _config = config;
            _logger = logger;
            _cacheService = cacheService;
            _acquisition = acquisition;
            _getDriver = getDriver;
        }

        #endregion

        #region ===================== 对外属性 =====================

        /// <summary> 是否已处理过首次型号逻辑 </summary>
        public bool IsIssued => _issued;

        #endregion

        #region ===================== 工作线程：首次型号 =====================

        /// <summary>
        /// 工作线程内调用：连接后只执行一次。
        /// 软件下发型号则写 PLC；否则标记由 PLC 决定。
        /// </summary>
        public async Task TryIssueOnceOnWorkerLoopAsync()
        {
            if (_issued)
                return;

            if (_config.IsIssueModel)
            {
                _logger.DeviceLog(_config.StationId, "型号下发", "由软件下发型号，准备下发型号");
                if (_cacheService.HasData<craft_TypeInfo>())
                {
                    _logger.DeviceLog(_config.StationId, "型号下发", "存在发布型号缓存数据");
                    var type = _cacheService.GetData<craft_TypeInfo>();
                    await IssueAsync(type); // 写入并置 _issued
                }
                else
                {
                    _logger.DeviceLog(_config.StationId, "型号下发", "缓存内不存在型号信息");
                    _issued = true; // 无缓存也视为已处理，避免每周期重试
                }
            }
            else
            {
                _logger.DeviceLog(_config.StationId, "型号下发", "由PLC决定型号");
                _issued = true;
            }
        }

        #endregion

        #region ===================== 事件：全局下发型号 =====================

        /// <summary> IssueModelEvent 订阅回调：向本工位 PLC 写入型号 </summary>
        public async Task IssueAsync(craft_TypeInfo typeInfo)
        {
            try
            {
                if (_acquisition.TryGetMapping(DataNameConstants.IssueProductType, out var address))
                {
                    _logger.DeviceLog(_config.StationId, "型号下发", "地址映射已配置型号下发");
                    var driver = _getDriver();
                    if (driver != null)
                    {
                        await driver.WriteAsync(
                            address.DataAddress,
                            address.DataType,
                            (short)typeInfo.IssueCode,
                            address.DataLen);
                        _logger.DeviceLog(_config.StationId, "型号下发",
                            $"已写入型号,编号为:{typeInfo.IssueCode}");
                    }
                    else
                    {
                        _logger.DeviceLog(_config.StationId, "型号下发", "驱动器为空", LogLevel.Warning);
                    }
                }
                else
                {
                    _logger.DeviceLog(_config.StationId, "型号下发", "地址映射未配置型号下发");
                }
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(_config.StationId, "型号下发",
                    $"型号下发失败: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _issued = true;
            }
        }

        #endregion
    }
}
