using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.DeviceManager.Drivers;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 工位数据采集：地址映射加载、轮询扫描、OPC 订阅与值变化去重。
    /// 不直接持有驱动，通过 <see cref="_getDriver"/> 向 StationConnection 索取。
    /// </summary>
    internal sealed class StationAcquisition
    {
        #region ===================== 字段 =====================

        #region --------------------- 只读依赖 ---------------------

        /// <summary> 工位连接配置 </summary>
        private readonly device_ConnectInfo _config;

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        /// <summary> 数据缓存（地址映射表） </summary>
        private readonly IDataCacheService _cacheService;

        /// <summary> 看板状态更新 </summary>
        private readonly IDeviceStatusManager _deviceStatus;

        /// <summary> 业务消息队列（读到的数据入队给 Interaction） </summary>
        private readonly StationBusinessChannel _businessChannel;

        /// <summary> 获取当前驱动（来自 StationConnection） </summary>
        private readonly Func<IDeviceCommunication?> _getDriver;

        /// <summary> 读失败/超时回调（通常 MarkDisconnectedAsync） </summary>
        private readonly Func<string, CancellationToken, Task> _onReadFault;

        #endregion

        #region --------------------- 映射与采集状态 ---------------------

        /// <summary> 本工位全部启用的地址映射 </summary>
        private List<device_AddressMapping> _addressMappings = new();

        /// <summary> 按 DataName 快速查找 </summary>
        private Dictionary<string, device_AddressMapping> _mappingDict = new();

        /// <summary> 只读/读写方向的映射子集 </summary>
        private List<device_AddressMapping>? _readMappings;

        /// <summary> OPC UA 是否走订阅模式（非轮询 Read） </summary>
        private bool _useSubscription;

        /// <summary> 上一轮采集值，相同值不重复转发 </summary>
        private readonly Dictionary<string, object?> _lastReadValues = new();

        /// <summary> 保护 _lastReadValues </summary>
        private readonly object _lastReadValuesLock = new();

        /// <summary> 正在执行的扫描周期计数（Stop 时等待当前周期结束） </summary>
        private int _scanCycleInProgress;

        #endregion

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建工位采集模块 </summary>
        public StationAcquisition(
            device_ConnectInfo config,
            ILogger logger,
            IDataCacheService cacheService,
            IDeviceStatusManager deviceStatus,
            StationBusinessChannel businessChannel,
            Func<IDeviceCommunication?> getDriver,
            Func<string, CancellationToken, Task> onReadFault)
        {
            _config = config;
            _logger = logger;
            _cacheService = cacheService;
            _deviceStatus = deviceStatus;
            _businessChannel = businessChannel;
            _getDriver = getDriver;
            _onReadFault = onReadFault;
        }

        #endregion

        #region ===================== 对外属性 =====================

        /// <summary> 工位 Id </summary>
        public int StationId => _config.StationId;

        /// <summary> 是否存在指定名称的地址映射 </summary>
        public bool HasMapping(string dataName) => _mappingDict.ContainsKey(dataName);

        /// <summary> 是否配置了读取型号映射 </summary>
        public bool HasProductTypeReadMapping =>
            _mappingDict.ContainsKey(DataNameConstants.ReadProductType);

        /// <summary> 是否有扫描周期正在执行（Stop 时 Context 仍允许 IO） </summary>
        public bool IsScanCycleInProgress => Volatile.Read(ref _scanCycleInProgress) > 0;

        /// <summary> 尝试获取映射 </summary>
        public bool TryGetMapping(string dataName, out device_AddressMapping mapping) =>
            _mappingDict.TryGetValue(dataName, out mapping!);

        #endregion

        #region ===================== 地址映射加载 =====================

        /// <summary> 从 IDataCacheService 加载本工位启用的地址映射 </summary>
        public async Task LoadMappingsAsync()
        {
            try
            {
                var allMappings = _cacheService.GetData<List<device_AddressMapping>>();
                if (allMappings != null)
                {
                    _addressMappings = allMappings
                        .Where(m => m.StationId == StationId && m.IsEnabled)
                        .ToList();
                    _mappingDict = _addressMappings.ToDictionary(m => m.DataName, m => m);
                    _useSubscription = _config.ProtocolType == "OPCUA"
                        && _addressMappings.Any(m =>
                            m.DataDirection == "Read" || m.DataDirection == "ReadWrite");
                    _readMappings = _addressMappings
                        .Where(m => m.DataDirection == "Read" || m.DataDirection == "ReadWrite")
                        .ToList();
                    _logger.DeviceLog(StationId, _config.DeviceCode,
                        $"加载地址映射成功: 工位={StationId}, 共{_addressMappings.Count}条,需要读取的地址共{_readMappings?.Count}条");
                }
                else
                {
                    _logger.DeviceLog(StationId, _config.DeviceCode,
                        $"未找到地址映射配置: 工位={StationId}", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $" 加载地址映射失败: 工位={StationId} " + ex.Message, LogLevel.Error);
            }

            await Task.CompletedTask;
        }

        #endregion

        #region ===================== 轮询扫描 =====================

        /// <summary> 执行一个扫描周期：轮询 Read 或 OPC 订阅模式下空转 Delay </summary>
        public async Task RunScanCycleAsync(CancellationToken token)
        {
            var driver = _getDriver();
            if (driver == null || token.IsCancellationRequested)
                return;

            Interlocked.Increment(ref _scanCycleInProgress);
            try
            {
                if (_useSubscription && (driver is OpcUaConnection ||
                    driver is SharedDriverPool.ThreadSafeDriver safeDriver &&
                    safeDriver.Inner is OpcUaConnection))
                {
                    await Task.Delay(5, token); // 订阅模式由回调推送，此处仅占位
                }
                else
                {
                    if (_readMappings == null)
                        return;

                    foreach (var item in _readMappings)
                    {
                        try
                        {
                            var result = await driver.ReadAsync(
                                item.DataAddress, item.DataType, item.DataLen);
                            if (result == null)
                                continue;
                            if (!TryMarkValueChanged(item.DataName, result))
                                continue; // 值未变化，不转发（200 保持时去重；200→100→200 值变化仍会触发）

                            await _businessChannel.EnqueueAsync(new DeviceDataMessage
                            {
                                StationId = StationId,
                                DataType = item.DataName,
                                Data = result
                            }, token);
                        }
                        catch (Exception ex) when (ConnectionIoExceptionHelper.IsIoTimeout(ex))
                        {
                            _logger.DeviceLog(StationId, _config.DeviceCode,
                                $"读取超时（连接可能已断开）: 名称={item.DataName}, 地址={item.DataAddress}, {ex.Message}",
                                LogLevel.Warning);
                            await _onReadFault("读取超时", token);
                            break;
                        }
                        catch (Exception ex) when (ConnectionIoExceptionHelper.IsTransport(ex))
                        {
                            _logger.DeviceLog(StationId, _config.DeviceCode,
                                $"读取传输异常: 名称={item.DataName}, {ex.Message}", LogLevel.Warning);
                            await _onReadFault("读取传输异常", token);
                            break;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"轮询读取数据错误，数据名称：{item.DataName}，数据地址：{item.DataAddress}，数据类型：{item.DataType}，数据长度：{item.DataLen}",
                                ex);
                        }
                    }
                }

                _deviceStatus.UpdateStatus(StationId, status => { status.LastError = string.Empty; });
                _deviceStatus.UpdateStatus(StationId, status => { status.UpdateTime = DateTime.Now; });
            }
            catch (OperationCanceledException)
            {
                // 订阅模式 Delay 取消时正常结束
            }
            catch (Exception ex) when (ConnectionIoExceptionHelper.IsIoTimeout(ex))
            {
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"扫描周期读取超时（连接可能已断开）: {ex.Message}", LogLevel.Warning);
                await _onReadFault("读取超时", token);
            }
            catch (Exception ex) when (ConnectionIoExceptionHelper.IsTransport(ex))
            {
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"扫描周期传输异常: {ex.Message}", LogLevel.Warning);
                await _onReadFault("传输异常", token);
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                    return;

                var root = ex.InnerException ?? ex;
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"交互地址读取设备数据失败: 工位={StationId}, 设备={_config.DeviceCode}，异常信息：{ex.Message}，原始异常：{root.Message}",
                    LogLevel.Error);
                await _onReadFault($"交互地址读取失败: {ex.Message}", token);
            }
            finally
            {
                Interlocked.Decrement(ref _scanCycleInProgress);
            }
        }

        #endregion

        #region ===================== 建连后准备 =====================

        /// <summary>
        /// 物理连接就绪后：清空值缓存并按协议初始化 OPC 订阅。
        /// 由 StationConnection 建连成功回调或 Task 编排层调用。
        /// </summary>
        public async Task PrepareAfterConnectionAsync(
            IDeviceCommunication? driver,
            string protocolType,
            CancellationToken token)
        {
            ResetValueCache(); // 重连后避免旧值影响去重

            if (protocolType != DeviceProtocolTypeConstants.OPCUA || driver == null)
                return;

            if (driver is OpcUaConnection opcUa)
                await InitializeSubscriptionsAsync(opcUa);

            if (driver is SharedDriverPool.ThreadSafeDriver safeDriver)
                await InitializeSubscriptionsAsync(safeDriver);
        }

        #endregion

        #region ===================== OPC 订阅 =====================

        /// <summary> 建连成功后注册 OPC UA 订阅 </summary>
        public async Task InitializeSubscriptionsAsync(IDeviceCommunication driver)
        {
            if (_readMappings == null || _readMappings.Count <= 0)
                return;

            if (driver is OpcUaConnection opcUa)
                await opcUa.SubscribeAllAsync(_readMappings, OnDataValueChanged);
            else if (driver is SharedDriverPool.ThreadSafeDriver safeDriver &&
                     safeDriver.Inner is OpcUaConnection)
                await safeDriver.SubscribeAllAsync(_readMappings, OnDataValueChanged);

            _logger.DeviceLog(_config.StationId, _config.DeviceCode,
                $"OPC UA 订阅初始化完成: 工位={_config.StationId}, 订阅数={_readMappings.Count}");
        }

        /// <summary> OPC 值变化回调：去重后 TryEnqueue </summary>
        private void OnDataValueChanged(string dataName, object? value)
        {
            try
            {
                if (!TryMarkValueChanged(dataName, value))
                    return;

                _businessChannel.TryEnqueue(new DeviceDataMessage
                {
                    StationId = StationId,
                    DataType = dataName,
                    Data = value
                });
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"处理OPC回调数据异常，数据名称：{dataName}，值：{value}，异常：{ex.Message}");
            }
        }

        #endregion

        #region ===================== 值缓存 =====================

        /// <summary> 断线或重连后清空上一轮读值，避免错误去重 </summary>
        public void ResetValueCache()
        {
            lock (_lastReadValuesLock)
            {
                _lastReadValues.Clear();
            }
        }

        /// <summary> 记录新值并返回是否相对上次有变化 </summary>
        private bool TryMarkValueChanged(string dataName, object? newValue)
        {
            lock (_lastReadValuesLock)
            {
                if (_lastReadValues.TryGetValue(dataName, out var lastValue)
                    && ValuesEqual(lastValue, newValue))
                {
                    return false;
                }

                _lastReadValues[dataName] = NormalizeReadValue(newValue);
                return true;
            }
        }

        /// <summary> 将读值规范化为可比较形式 </summary>
        private static object? NormalizeReadValue(object? value)
        {
            if (value == null)
                return null;

            return value switch
            {
                byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                    => Convert.ToDecimal(value),
                bool b => b,
                string s => s,
                _ => value
            };
        }

        /// <summary> 比较两次读值是否相等 </summary>
        private static bool ValuesEqual(object? lastValue, object? newValue)
        {
            if (lastValue == null && newValue == null)
                return true;
            if (lastValue == null || newValue == null)
                return false;

            var normalizedNew = NormalizeReadValue(newValue);
            if (lastValue.Equals(normalizedNew))
                return true;

            return string.Equals(
                lastValue.ToString(),
                normalizedNew?.ToString(),
                StringComparison.Ordinal);
        }

        #endregion
    }
}
