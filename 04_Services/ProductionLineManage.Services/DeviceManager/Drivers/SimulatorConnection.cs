using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Services.DeviceManager.Drivers
{
    /// <summary>
    /// 模拟设备驱动：不依赖真实硬件，用于测试与开发。
    /// 维护内存字典模拟 PLC 地址读写，并自动推进 RequestCode 指令流程。
    /// </summary>
    public class SimulatorConnection : IDeviceCommunication
    {
        #region ===================== 字段 =====================

        #region --------------------- 连接状态 ---------------------

        /// <summary> 是否已连接 </summary>
        private bool _isConnected;

        /// <summary> 当前工位连接配置 </summary>
        private device_ConnectInfo? _config;

        /// <summary> 保护模拟数据字典的锁 </summary>
        private readonly object _lockObj = new object();

        /// <summary> 后台模拟数据生成任务的取消源 </summary>
        private CancellationTokenSource clt = new CancellationTokenSource();

        #endregion

        #region --------------------- 模拟数据 ---------------------

        /// <summary> 模拟 PLC 地址 → 值 </summary>
        private readonly Dictionary<string, object> _simulatedData = new Dictionary<string, object>();

        /// <summary> 随机数生成（心跳失败概率测试等） </summary>
        private readonly Random _random = new Random();

        /// <summary> 流水码/物料码递增计数 </summary>
        private int _counter = 0;

        #endregion

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 启动后台任务，按 ResponseCode 自动推进 RequestCode </summary>
        public SimulatorConnection()
        {
            Task.Run(GenerateSimulatedData, clt.Token);
        }

        #endregion

        #region ===================== IDeviceCommunication 属性 =====================

        /// <summary> 当前连接是否可用 </summary>
        public bool IsConnected => _isConnected;

        #endregion

        #region ===================== 连接与断开 =====================

        /// <summary> 模拟连接设备（延迟 100ms 后初始化模拟数据） </summary>
        public async Task<bool> ConnectAsync(device_ConnectInfo config)
        {
            _config = config;

            await Task.Delay(100); // 模拟连接耗时

            lock (_lockObj)
            {
                _isConnected = true;
                InitializeSimulatedData(); // 重置 RunState、RequestCode 等初始值
            }

            return true;
        }

        /// <summary> 断开连接并清空模拟数据 </summary>
        public void Disconnect()
        {
            lock (_lockObj)
            {
                _isConnected = false;
                _simulatedData.Clear();
                clt?.Cancel(); // 停止后台生成任务
            }
        }

        /// <summary> 断开连接释放资源 </summary>
        public void Dispose()
        {
            Disconnect();
        }

        #endregion

        #region ===================== 读写 =====================

        /// <summary> 从模拟字典读取；地址不存在时按 dataType 返回默认值 </summary>
        public async Task<object?> ReadAsync(string dataAddress, string dataType, int dataLen)
        {
            await Task.Delay(5); // 模拟读取延迟

            lock (_lockObj)
            {
                if (_simulatedData.TryGetValue(dataAddress, out var value))
                {
                    return value;
                }
                return ParseDataType(dataType); // 未初始化地址返回类型默认
            }
        }

        /// <summary> 写入模拟字典；RequestCode/ResponseCode 会触发指令流程模拟 </summary>
        public async Task<bool> WriteAsync(string dataAddress, string dataType, object value, int dataLen)
        {
            await Task.Delay(5); // 模拟写入延迟

            lock (_lockObj)
            {
                _simulatedData[dataAddress] = value;

                // 记录最后下发的指令
                if (dataAddress == "RequestCode" || dataAddress.Contains("Command"))
                {
                    _simulatedData["LastCommand"] = value;
                    _simulatedData["LastCommandTime"] = DateTime.Now;
                }

                // 写入 ResponseCode 时模拟 PLC 侧状态推进
                if (dataAddress == "ResponseCode")
                {
                    SimulateResponse(value.ToString()!);
                }
            }

            return true;
        }

        /// <summary> 模拟心跳（当前固定返回连接状态；可启用 5% 失败概率测试重连） </summary>
        public async Task<bool> HeartbeatAsync(string dataType, string address)
        {
            await Task.Delay(10);

            lock (_lockObj)
            {
                // 模拟 5% 概率心跳失败（测试断线重连）
                //if (_random.Next(0, 100) < 5)
                //{
                //    return false;
                //}
                return _isConnected;
            }
        }

        /// <summary> 模拟驱动不支持 OPC 订阅 </summary>
        public Task SubscribeAllAsync(List<device_AddressMapping> mappings, Action<string, object?> valueHandler)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region ===================== 模拟数据初始化 =====================

        /// <summary> 连接成功后重置模拟字典为初始业务状态 </summary>
        private void InitializeSimulatedData()
        {
            _simulatedData.Clear();

            _simulatedData["RunState"] = (int)RunState.Running;
            _simulatedData["TodayCount"] = _random.Next(0, 500);
            _simulatedData["ReadProductType"] = "型号A";
            _simulatedData["RequestCode"] = 0;
            _simulatedData["ResponseCode"] = 0;
        }

        #endregion

        #region ===================== 后台指令流程模拟 =====================

        /// <summary> 后台循环：根据当前 ResponseCode 自动递增 RequestCode（测试用） </summary>
        private async Task GenerateSimulatedData()
        {
            while (clt.Token.IsCancellationRequested)
            {
                switch (Convert.ToInt32(_simulatedData["ResponseCode"]))
                {
                    case 0:
                        _simulatedData["RequestCode"] = 100;
                        break;
                    case 100:
                        _simulatedData["RequestCode"] = 200;
                        break;
                    case 200:
                        _simulatedData["RequestCode"] = 500;
                        break;
                    case 500:
                        _simulatedData["RequestCode"] = 800;
                        break;
                    case 800:
                        _simulatedData["RequestCode"] = 800;
                        return; // 流程结束退出循环
                }

                await Task.Delay(1000);
            }
        }

        /// <summary> 根据 ResponseCode 模拟 PLC 发送下一请求（握手→流水码→物料→保存） </summary>
        private void SimulateResponse(string responseValue)
        {
            if (int.TryParse(responseValue, out int responseCode))
            {
                if (responseCode == 100)
                {
                    _simulatedData["RequestCode"] = 200; // 握手成功，模拟流水码验证请求
                    _simulatedData["DataPayload"] = $"TEST_FLOW_{_counter++}";
                }
                else if (responseCode == 200)
                {
                    _simulatedData["RequestCode"] = 500; // 流水码成功，模拟物料验证
                    _simulatedData["DataPayload"] = $"MATERIAL_{_counter++}";
                    _simulatedData["CodeType"] = "HK";
                }
                else if (responseCode == 500)
                {
                    _simulatedData["RequestCode"] = 800; // 物料成功，模拟保存请求
                }
                else if (responseCode == 800)
                {
                    _simulatedData["RequestCode"] = 0; // 保存成功，重置
                }
            }
        }

        #endregion

        #region ===================== 类型辅助 =====================

        /// <summary> 地址未初始化时按 dataType 返回模拟默认值 </summary>
        private object? ParseDataType(string dataType)
        {
            switch (dataType)
            {
                case "bool":
                    return true;
                case "byte":
                    return 84;
                case "short":
                    return 1;
                case "int":
                    return 1;
                case "float":
                    return 0.0;
                case "double":
                    return 0.0;
                case "string":
                    return "12346";
                case "DateTime":
                    return DateTime.Now;
                default:
                    return null;
            }
        }

        #endregion
    }
}
