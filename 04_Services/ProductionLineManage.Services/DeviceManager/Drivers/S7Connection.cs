// ============================================================
// S7Connection - 西门子 S7 协议驱动
// 使用 S7NetPlus 库实现 S7 通信
// ============================================================

using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;
using S7.Net;
using S7.Net.Types;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using DataType = S7.Net.DataType;

namespace ProductionLineManage.Services.DeviceManager.Drivers
{
    /// <summary>
    /// 西门子 S7 协议驱动
    /// 支持 CPU: S7-200, S7-300, S7-400, S7-1200, S7-1500, S7-200Smart, Logo
    /// 支持地址: DB块、M区、I区、Q区
    /// </summary>
    public class S7Connection : IDeviceCommunication
    {
        private static int _globalOpenAttemptCounter;
        private static int _globalConnectAsyncCounter;

        #region ===================== 私有字段 =====================

        private readonly ILogger? _logger;
        private Plc? _plc;
        private device_ConnectInfo? _config;
        private bool _isConnected;
        private readonly object _lockObj = new object();
        private readonly SemaphoreSlim _connectGate = new(1, 1);
        private readonly Random _random = new Random(); // 随机值，Int 心跳使用

        /// <summary> 单次 Read/Write 超时；不断开连接，仅抛错由上层记日志 </summary>
        private const int IoTimeoutMs = 3000;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 初始化 S7 驱动 </summary>
        public S7Connection(ILogger? logger = null)
        {
            _logger = logger;
        }

        #endregion

        #region ===================== IDeviceCommunication 实现 =====================

        /// <summary> 是否已连接 PLC </summary>
        public bool IsConnected
        {
            get
            {
                lock (_lockObj)
                {
                    if (_plc == null)
                    {
                        _isConnected = false;
                        return false;
                    }

                    _isConnected = _plc.IsConnected;
                    return _isConnected;
                }
            }
        }

        #region --------------------- 连接与断开 ---------------------

        /// <summary> 连接 PLC（S7NetPlus Open 为同步阻塞，在连接门禁内直接调用） </summary>
        public async Task<bool> ConnectAsync(device_ConnectInfo config)
        {
            _config = config;
            var connectNo = Interlocked.Increment(ref _globalConnectAsyncCounter);
            var stationId = config.StationId;
            var deviceCode = config.DeviceCode ?? string.Empty;

            await _connectGate.WaitAsync();
            try
            {
                lock (_lockObj)
                {
                    if (_plc != null && _plc.IsConnected)
                    {
                        _isConnected = true;
                        _logger?.Info(
                            $"[S7 Connect #{connectNo}] 已连接，跳过 Open 工位={stationId}",
                            nameof(S7Connection));
                        return true;
                    }
                }

                var parsed = ParseConnectionString(config.ConnectionString);
                var cpu = ConvertToCpuType(parsed.cpuType);
                var rack = (short)parsed.rack;
                var slot = (short)parsed.slot;

                lock (_lockObj)
                {
                    if (_plc == null)
                        _plc = new Plc(cpu, parsed.ip, rack, slot);
                }

                var attemptNo = Interlocked.Increment(ref _globalOpenAttemptCounter);
                var sw = Stopwatch.StartNew();
                _logger?.Info(
                    $"[S7 Open #{attemptNo}] 同步连接开始 工位={stationId} 设备={deviceCode} thread={Environment.CurrentManagedThreadId}",
                    nameof(S7Connection));

                try
                {
                    lock (_lockObj)
                    {
                        _plc!.Open();
                        _isConnected = _plc.IsConnected;
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    InvalidateConnection();
                    _logger?.Error(
                        $"[S7 Open #{attemptNo}] 失败 工位={stationId} 耗时={sw.ElapsedMilliseconds}ms err={ex.Message}",
                        nameof(S7Connection));
                    return false;
                }

                sw.Stop();
                if (_isConnected)
                {
                    _logger?.Info(
                        $"[S7 Open #{attemptNo}] 成功 工位={stationId} 耗时={sw.ElapsedMilliseconds}ms",
                        nameof(S7Connection));
                    return true;
                }

                InvalidateConnection();
                _logger?.Error(
                    $"[S7 Open #{attemptNo}] 失败 工位={stationId} PLC 未连接",
                    nameof(S7Connection));
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Error(
                    $"[S7 Connect #{connectNo}] 异常 工位={stationId} err={ex.Message}",
                    nameof(S7Connection));
                return false;
            }
            finally
            {
                _connectGate.Release();
                _logger?.Info(
                    $"[S7 Connect #{connectNo}] 释放连接门禁 工位={stationId}",
                    nameof(S7Connection));
            }
        }

        /// <summary> 断开 PLC 连接并释放实例 </summary>
        public void Disconnect()
        {
            lock (_lockObj)
            {
                if (_plc != null && _plc.IsConnected)
                {
                    _plc.Close();
                }
                _plc = null;
                _isConnected = false;
            }
        }

        #endregion

        #region --------------------- 读取操作 ---------------------

        /// <summary> 读取 PLC 数据（S7String 与其他类型分路处理） </summary>
        public async Task<object?> ReadAsync(string dataAddress, string varType, int dataLen = 0)
        {
            if (!IsConnected)
                throw new InvalidOperationException("PLC 未连接");

            try
            {
                return varType switch
                {
                    DeviceDataTypeConstants.SS7String => await ReadS7String(dataAddress, varType, dataLen),
                    _ => await ReadOther(dataAddress, varType, dataLen)
                };
            }
            catch (Exception ex) when (IsTransportException(ex))
            {
                InvalidateConnection();
                throw;
            }
        }

        /// <summary>读取其他类型</summary>
        private async Task<object> ReadOther(string dataAddress, string varType, int dataLen)
        {
            var (_dataType, _db, _startByte, _bit) = ParseAddress(dataAddress);
            return await AwaitWithTimeoutAsync(
                _plc!.ReadAsync(_dataType, _db, _startByte, GetVarType(varType), dataLen),
                IoTimeoutMs,
                "读取",
                disconnectOnTimeout: false) ?? ReturnDefaultValue(varType);
        }
        private async Task<string> ReadS7String(string dataAddress, string varType, int dataLen)
        {
            var (db, startAddress) = ParseStrAddress(dataAddress);
            return (string)(await AwaitWithTimeoutAsync(
                _plc!.ReadAsync(DataType.DataBlock, db, startAddress, VarType.S7String, dataLen),
                IoTimeoutMs,
                "读取字符串",
                disconnectOnTimeout: false) ?? "");
        }


        #endregion

        #region --------------------- 写入操作 ---------------------

        /// <summary> 写入 PLC 数据（S7String 与其他类型分路处理） </summary>
        public async Task<bool> WriteAsync(string dataAddress, string dataType, object value, int dataLen = 0)
        {
            if (!IsConnected)
                throw new InvalidOperationException("PLC 未连接");

            try
            {
                return dataType switch
                {
                    "S7String" => await WriteS7StringAsync(dataAddress, dataType, value, dataLen),
                    _ => await WriteOtherAsync(dataAddress, dataType, value, dataLen)
                };
            }
            catch (Exception ex) when (IsTransportException(ex))
            {
                InvalidateConnection();
                throw;
            }
        }

        private async Task<bool> WriteS7StringAsync(string dataAddress, string dataType, object value, int dataLen = 0)
        {
            try
            {
                var (db, startAddress) = ParseStrAddress(dataAddress);
                //根据S7西门子中String变量类型需要添加长度
                byte[] stringBytes = S7String.ToByteArray(value.ToString(), (value.ToString() ?? "").Length);
                //await AwaitWithTimeoutAsync(
                //    _plc!.WriteAsync(DataType.DataBlock, db, startAddress, stringBytes),
                //    IoTimeoutMs,
                //    "写入字符串",
                //    disconnectOnTimeout: false);
                for (int i = 0; i < 5; i++)
                {
                    await _plc!.WriteAsync(DataType.DataBlock, db, startAddress, stringBytes);
                    await Task.Delay(20);
                    var result = await _plc!.ReadAsync(DataType.DataBlock, db, startAddress, VarType.S7String, dataLen);
                    if (result?.ToString() == value.ToString())
                    {
                        return true;
                    }
                }
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"S7字符串写入异常(地址：{dataAddress}、值：{value})：" + ex.Message);
            }
            return true;
        }
        private async Task<bool> WriteOtherAsync(string dataAddress, string dataType, object value, int dataLen = 0)
        {
            try
            {
                var resultValue = ConvertDataType(dataType, value);
                var (_dataType, _db, _startByte, _bit) = ParseAddress(dataAddress);
                //写入
                //await AwaitWithTimeoutAsync(
                //_plc!.WriteAsync(dataAddress, resultValue),
                //IoTimeoutMs,
                //"写入",
                //disconnectOnTimeout: false);
                for (int i = 0;i < 5 ;i++)
                {
                    await _plc!.WriteAsync(dataAddress, resultValue);
                    await Task.Delay(20);
                    var readBack = await _plc!.ReadAsync(_dataType, _db, _startByte, GetVarType(dataType), dataLen);
                    if (ValuesMatch(readBack, resultValue, dataType)) 
                        return true;
                }
               
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"S7写入异常(地址：{dataAddress}、值：{value})：" + ex.Message);
            }
            return true;
        }


        #endregion

        #region --------------------- 心跳处理 ---------------------

        /// <summary> 心跳检测（支持 Bool 与 Int 类型） </summary>
        public async Task<bool> HeartbeatAsync(string dataType, string address)
        {
            if (!IsConnected)
                return false;

            try
            {
                if (dataType == "Bool")
                    return await WriteBoolHeartbeat(address);
                if (dataType == "Int")
                    return await WriteIntHeartbeat(address);
                return true;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex) when (IsTransportException(ex))
            {
                InvalidateConnection();
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>往设备里写 true ，设备读取到后写false 来确认心跳</summary>
        private async Task<bool> WriteBoolHeartbeat(string address)
        {
            try
            {
                //写入设备地址true
                await WriteOtherAsync(address, "Bool", true);
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException)
                    throw;
                throw new Exception("心跳写入异常：" + ex.Message);
            }
            return true;
        }
        /// <summary>写入int类型的心跳</summary>
        /// <param name="address"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<bool> WriteIntHeartbeat(string address)
        {
            try
            {
                //写入设备地址0-10随机数
                await WriteOtherAsync(address, "Int", _random.Next(0, 10));
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException)
                    throw;
                throw new Exception("心跳写入异常：" + ex.Message);
            }
            return true;
        }

        #endregion

        /// <summary> 释放连接与连接门禁 </summary>
        public void Dispose()
        {
            Disconnect();
            _connectGate.Dispose();
        }

        /// <summary> S7 不支持订阅，抛出 NotImplementedException </summary>
        public Task SubscribeAllAsync(List<device_AddressMapping> mappings, Action<string, object?> valueHandler)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region ===================== 私有方法：连接与超时 =====================

        /// <summary>
        /// 带超时的 await。IO 超时会 Invalidate 并等待底层 Task 结束，避免 Task 未观察异常。
        /// </summary>
        private async Task AwaitWithTimeoutAsync(Task task, int timeoutMs, string operationName, bool disconnectOnTimeout)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
            }
            catch (TimeoutException)
            {
                _ = ObserveTaskFaultAsync(task);
                if (disconnectOnTimeout)
                    Disconnect();
                else
                    InvalidateConnection();
                throw new TimeoutException($"S7 {operationName} 超时（{timeoutMs}ms）");
            }
        }

        private async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task, int timeoutMs, string operationName, bool disconnectOnTimeout)
        {
            try
            {
                return await task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
            }
            catch (TimeoutException)
            {
                _ = ObserveTaskFaultAsync(task);
                //if (disconnectOnTimeout)
                //    Disconnect();
                //else
                //    InvalidateConnection();
                throw new TimeoutException($"S7 {operationName} 超时（{timeoutMs}ms）");
            }
        }

        /// <summary>TCP 已不可用但保留 Plc 实例供重连 Open</summary>
        private void InvalidateConnection()
        {
            lock (_lockObj)
            {
                _isConnected = false;
                try
                {
                    if (_plc != null && _plc.IsConnected)
                        _plc.Close();
                }
                catch
                {
                    // 远端已断开时 Close 可能失败，忽略
                }
            }
        }

        private static bool IsTransportException(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SocketException or IOException)
                    return true;
            }
            return false;
        }

        /// <summary>WaitAsync 超时后底层 S7.Net Task 仍会继续；await 至结束并吞掉异常，避免 GC 终结器抛出</summary>
        private static async Task ObserveTaskFaultAsync(Task task)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                    _ = task.Exception;
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // 仅观察异常，不向上抛
            }
        }

        /// <summary>
        /// 解析连接字符串
        /// 格式: "ip=192.168.1.100;cpu=1500;rack=0;slot=1"
        /// </summary>
        private (string ip, string cpuType, int rack, int slot) ParseConnectionString(string connectionString)
        {
            string ip = "192.168.1.100";
            string cpuType = "1500";
            int rack = 0;
            int slot = 1;

            if (string.IsNullOrEmpty(connectionString))
                return (ip, cpuType, rack, slot);

            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                {
                    switch (kv[0].ToLower())
                    {
                        case "ip": ip = kv[1]; break;
                        case "cpu": cpuType = kv[1]; break;
                        case "rack": int.TryParse(kv[1], out rack); break;
                        case "slot": int.TryParse(kv[1], out slot); break;
                    }
                }
            }

            return (ip, cpuType, rack, slot);
        }



        #endregion

        #region ===================== 私有方法：地址解析 =====================

        /// <summary>
        /// 解析 S7 地址
        /// 支持格式:
        ///   DB100.DBW0      - 数据块字
        ///   DB100.DBB0      - 数据块字节
        ///   DB100.DBD0      - 数据块双字
        ///   DB100.DBX0.0    - 数据块位
        ///   M0.0            - 标志区
        ///   I0.0            - 输入区
        ///   Q0.0            - 输出区
        ///   DB100.STRING0   - 字符串（需要长度信息）
        /// </summary>
        private (DataType dataType, int db, int startByte, int bit) ParseAddress(string address)
        {
            address = address.ToUpper().Trim();

            // 处理 DB 块地址
            if (address.StartsWith("DB"))
            {
                return ParseDBAddress(address);
            }

            // 处理 M、I、Q 等地址
            return ParseSimpleAddress(address);
        }

        /// <summary>
        /// 解析 DB 块地址: DB100.DBW0, DB100.DBB0, DB100.DBD0, DB100.DBX0.0
        /// </summary>
        private (DataType dataType, int db, int startByte, int bit) ParseDBAddress(string address)
        {
            // 提取 DB 号
            int dbEnd = address.IndexOf('.');
            if (dbEnd == -1)
                throw new FormatException($"无效的 DB 地址格式: {address}");

            string dbPart = address.Substring(2, dbEnd - 2);
            if (!int.TryParse(dbPart, out int dbNumber))
                throw new FormatException($"无效的 DB 号: {address}");

            string remaining = address.Substring(dbEnd + 1);

            // 解析数据类型和偏移
            if (remaining.StartsWith("DBX"))
            {
                // 位地址: DBX0.0
                string bitPart = remaining.Substring(3);
                var dotIndex = bitPart.IndexOf('.');
                if (dotIndex == -1)
                    throw new FormatException($"无效的位地址格式: {address}");

                if (int.TryParse(bitPart.Substring(0, dotIndex), out int byteOffset) &&
                    int.TryParse(bitPart.Substring(dotIndex + 1), out int bitOffset))
                {
                    return (DataType.DataBlock, dbNumber, byteOffset, bitOffset);
                }
            }
            else if (remaining.StartsWith("DBW"))
            {
                // 字地址: DBW0
                string offsetPart = remaining.Substring(3);
                if (int.TryParse(offsetPart, out int offset))
                {
                    return (DataType.DataBlock, dbNumber, offset, 0);
                }
            }
            else if (remaining.StartsWith("DBD"))
            {
                // 双字地址: DBD0
                string offsetPart = remaining.Substring(3);
                if (int.TryParse(offsetPart, out int offset))
                {
                    return (DataType.DataBlock, dbNumber, offset, 0);
                }
            }
            else if (remaining.StartsWith("DBB"))
            {
                // 字节地址: DBB0
                string offsetPart = remaining.Substring(3);
                if (int.TryParse(offsetPart, out int offset))
                {
                    return (DataType.DataBlock, dbNumber, offset, 0);
                }
            }

            throw new FormatException($"不支持的 DB 地址格式: {address}");
        }

        /// <summary>
        /// 解析简单地址: M0.0, I0.0, Q0.0
        /// </summary>
        private (DataType dataType, int db, int startByte, int bit) ParseSimpleAddress(string address)
        {
            DataType dataType;
            int startByte = 0;
            int bit = 0;

            // 确定数据类型
            if (address.StartsWith("M"))
            {
                dataType = DataType.Memory;
                address = address.Substring(1);
            }
            else if (address.StartsWith("I"))
            {
                dataType = DataType.Input;
                address = address.Substring(1);
            }
            else if (address.StartsWith("Q"))
            {
                dataType = DataType.Output;
                address = address.Substring(1);
            }
            else if (address.StartsWith("T"))
            {
                dataType = DataType.Timer;
                address = address.Substring(1);
            }
            else
            {
                throw new FormatException($"不支持的地址类型: {address}");
            }

            // 解析字节和位
            var dotIndex = address.IndexOf('.');
            if (dotIndex == -1)
            {
                // 没有位，可能是字/字节地址
                if (int.TryParse(address, out int byteOffset))
                {
                    return (dataType, 0, byteOffset, 0);
                }
            }
            else
            {
                // 位地址格式: 0.0
                if (int.TryParse(address.Substring(0, dotIndex), out int byteOffset) &&
                    int.TryParse(address.Substring(dotIndex + 1), out int bitOffset))
                {
                    return (dataType, 0, byteOffset, bitOffset);
                }
            }

            throw new FormatException($"无效的地址格式: {address}");
        }
        /// <summary> 将配置数据类型映射为 S7.Net VarType </summary>
        private VarType GetVarType(string varName)
        {
            switch (varName)
            {
                case DeviceDataTypeConstants.SBool:
                    return VarType.Bit;
                case DeviceDataTypeConstants.SByte:
                    return VarType.Byte;
                case DeviceDataTypeConstants.SWord:
                    return VarType.Word;
                case DeviceDataTypeConstants.SInt:
                    return VarType.Int;
                case DeviceDataTypeConstants.SDInt:
                    return VarType.DInt;
                case DeviceDataTypeConstants.SS7String:
                    return VarType.S7String;
                case DeviceDataTypeConstants.SReal:
                    return VarType.Real;
                case DeviceDataTypeConstants.SDateTime:
                    return VarType.DateTime;
                case DeviceDataTypeConstants.STimer:
                    return VarType.Timer;
                default:
                    throw new Exception($"变量类型错误,未找到对应的数据类型：{varName}");
                    
            }
        }

        /// <summary>返回默认值</summary>
        /// <param name="varType"></param>
        /// <returns></returns>
        private object ReturnDefaultValue(string varType)
        {
            switch (varType)
            {
                case "Bool":
                    return false;
                case "Byte":
                    return 0;
                case "Word":
                    return 0;
                case "DWord":
                    return 0;
                case "Int":
                    return 0;
                case "DInt":
                    return 0;
                case "S7String":
                    return "default";
                case "Real":
                    return 0.0;
                case "String":
                    return "default";
                case "DateTime":
                    return "1997-01-01";
                case "LReal":
                    return 0.0;
                case "S7WString":
                    return "default";
                default: return 0;
            }

        }

        /// <summary>
        /// 解析DB块里字符串的地址 示例数据：DB=11,StartAddress=5
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private (int dbNumber, int startAddress) ParseStrAddress(string address)
        {
            address = address.Trim();
            if (address.Length == 0)
                throw new Exception(" S7解析字符串错误，地址映射中的DataAddress长度为0");
            string[] strings = address.Split(',');
            if (strings.Length == 2)
            {
                int db = int.Parse(strings[0].Replace("DB=", ""));
                int startAddress = int.Parse(strings[1].Replace("StartAddress=", ""));
                if (db <= 0 || startAddress < 0)
                    throw new Exception($"S7解析字符串错误，地址映射中的DataAddress分组后解析错误:{strings[0]}、{strings[1]}");
                return (db, startAddress);

            }
            else
            {
                throw new Exception($"S7解析字符串错误，地址映射中的DataAddress分组后不为2:{strings.Length}");
            }
        }


        #endregion

        #region ===================== 私有方法：类型转换 =====================

        /// <summary> 转换 CPU 类型字符串为 S7NetPlus 枚举 </summary>
        private CpuType ConvertToCpuType(string cpuType)
        {
            return cpuType.ToLower() switch
            {
                "1500" => CpuType.S71500,
                "1200" => CpuType.S71200,
                "200" => CpuType.S7200,
                "300" => CpuType.S7300,
                "400" => CpuType.S7400,
                "200smart" => CpuType.S7200Smart,
                "logo" => CpuType.Logo0BA8,
                _ => CpuType.S71500
            };
        }

        /// <summary> 根据数据类型转换为设备可识别的类型 </summary>
        /// <param name="dataType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private object ConvertDataType(string dataType, object value)
        {
            return dataType switch
            {
                "Byte" => (byte)value,
                "Word" => Convert.ToUInt16(value),
                "Int" => Convert.ToInt16(value),
                "Real" => (float)value,
                _ => value,
            };

        }
        private static bool ValuesMatch(object? readBack, object expected, string dataType)
        {
            if (readBack is null) return false;
            try
            {
                return dataType switch
                {
                    "Bool" => Convert.ToBoolean(readBack) == Convert.ToBoolean(expected),
                    "Byte" => Convert.ToByte(readBack) == Convert.ToByte(expected),
                    "Word" => Convert.ToUInt16(readBack) == Convert.ToUInt16(expected),
                    "Int" => Convert.ToInt16(readBack) == Convert.ToInt16(expected),
                    "DInt" => Convert.ToInt32(readBack) == Convert.ToInt32(expected),
                    "Real" => Math.Abs(Convert.ToSingle(readBack) - Convert.ToSingle(expected)) < 1e-4f,
                    _ => readBack.ToString() == expected.ToString()
                };
            }
            catch
            {
                return false;
            }
        }
            #endregion
     }
}
