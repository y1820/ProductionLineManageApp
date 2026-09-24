using Opc.Ua;
using OpcUaHelper;
using ProductionLineManage.Core.Constants;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Services.DeviceManager.Drivers
{
    /// <summary>
    /// OPC UA 协议驱动：基于 OpcUaHelper 实现连接、读写、订阅与心跳。
    /// 支持匿名/用户名密码认证，订阅模式由 StationAcquisition 初始化。
    /// </summary>
    public class OpcUaConnection : IDeviceCommunication
    {
        #region ===================== 字段 =====================

        #region --------------------- 连接状态 ---------------------

        /// <summary> OPC UA 客户端实例 </summary>
        private OpcUaClient? _client;

        /// <summary> 当前工位连接配置 </summary>
        private device_ConnectInfo? _config;

        /// <summary> 是否已连接 </summary>
        private bool _isConnected;

        /// <summary> 保护连接与订阅操作的锁 </summary>
        private readonly object _lockObj = new object();

        #endregion

        #region --------------------- 订阅管理 ---------------------

        /// <summary> 订阅 Key → 节点 Id </summary>
        private readonly ConcurrentDictionary<string, string> _subscriptionKeys = new();

        /// <summary> 节点 Id → 值变化回调列表 </summary>
        private readonly ConcurrentDictionary<string, List<Action<object?>>> _subscriptionCallbacks = new();

        #endregion

        #region --------------------- 心跳辅助 ---------------------

        /// <summary> 服务器状态节点（预留，当前未使用） </summary>
        private const string HeartbeatNodeId = "ns=0;i=2258";

        /// <summary> Int16 心跳写入随机数 </summary>
        private readonly Random _random = new Random();

        #endregion

        #endregion

        #region ===================== IDeviceCommunication 属性 =====================

        /// <summary> 当前连接是否可用 </summary>
        public bool IsConnected => _isConnected;

        #endregion

        #region ===================== 连接与断开 =====================

        /// <summary> 连接 OPC UA 服务器（解析连接串、设置认证、ConnectServer） </summary>
        public async Task<bool> ConnectAsync(device_ConnectInfo config)
        {
            _config = config;

            try
            {
                var (url, authType, userName, password) = ParseConnectionString(config.ConnectionString); // 解析 URL 与认证参数

                _client = new OpcUaClient();

                // 按 auth 参数设置 UserIdentity
                switch (authType.ToLower())
                {
                    case "anonymous":
                        _client.UserIdentity = new UserIdentity(new AnonymousIdentityToken());
                        break;
                    case "username":
                        _client.UserIdentity = new UserIdentity(userName, password);
                        break;
                    default:
                        _client.UserIdentity = new UserIdentity(new AnonymousIdentityToken()); // 未知 auth 默认匿名
                        break;
                }

                await _client.ConnectServer(url);
                _isConnected = _client.Connected;

                if (_isConnected)
                {
                    return true;
                }
                else
                {
                    throw new Exception("OPC UA 连接失败");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"OPC UA 连接失败: {ex.Message}");
            }
        }

        /// <summary> 断开连接并清理订阅 </summary>
        public void Disconnect()
        {
            lock (_lockObj)
            {
                UnsubscribeAll(); // 先取消所有订阅

                if (_client != null)
                {
                    _client.Disconnect();
                    _client = null;
                }
                _isConnected = false;
            }
        }

        /// <summary> 释放资源（当前未主动 Disconnect） </summary>
        public void Dispose()
        {
            //Disconnect();
        }

        #endregion

        #region ===================== 读写 =====================

        /// <summary> 按数据类型读取 OPC 节点值；失败时返回类型默认值 </summary>
        public async Task<object?> ReadAsync(string dataAddress, string dataType, int dataLen)
        {
            if (_client == null || !_client.Connected)
            {
                throw new InvalidOperationException("OPC UA 客户端未连接");
            }

            try
            {
                object? result = dataType switch
                {
                    DeviceDataTypeConstants.OBoolean => await _client.ReadNodeAsync<bool>(dataAddress),
                    DeviceDataTypeConstants.OByte => await _client.ReadNodeAsync<byte>(dataAddress),
                    DeviceDataTypeConstants.OSByte => await _client.ReadNodeAsync<sbyte>(dataAddress),
                    DeviceDataTypeConstants.OInt16 => await _client.ReadNodeAsync<short>(dataAddress),
                    DeviceDataTypeConstants.OUInt16 => await _client.ReadNodeAsync<ushort>(dataAddress),
                    DeviceDataTypeConstants.OInt32 => await _client.ReadNodeAsync<int>(dataAddress),
                    DeviceDataTypeConstants.OUInt32 => await _client.ReadNodeAsync<uint>(dataAddress),
                    DeviceDataTypeConstants.OInt64 => await _client.ReadNodeAsync<long>(dataAddress),
                    DeviceDataTypeConstants.OUInt64 => await _client.ReadNodeAsync<ulong>(dataAddress),
                    DeviceDataTypeConstants.OFloat => await _client.ReadNodeAsync<float>(dataAddress),
                    DeviceDataTypeConstants.ODouble => await _client.ReadNodeAsync<double>(dataAddress),
                    DeviceDataTypeConstants.OString => await _client.ReadNodeAsync<string>(dataAddress),
                    DeviceDataTypeConstants.ODateTime => await _client.ReadNodeAsync<DateTime>(dataAddress),
                    DeviceDataTypeConstants.OGuid => await _client.ReadNodeAsync<Guid>(dataAddress),
                    _ => await _client.ReadNodeAsync<object>(dataAddress) // 未映射类型按 object 读取
                };

                return result;
            }
            catch //(Exception ex)
            {
                return GetDefaultValue(dataType); // 读取失败返回默认值，避免中断采集周期
            }
        }

        /// <summary> 按数据类型写入 OPC 节点值 </summary>
        public async Task<bool> WriteAsync(string dataAddress, string dataType, object value, int dataLen)
        {
            if (_client == null || !_client.Connected)
            {
                throw new InvalidOperationException("OPC UA 客户端未连接");
            }

            try
            {
                // 字符串类型按 DataLen 截断，防止超长
                if (dataType == DeviceDataTypeConstants.OString)
                {
                    string stringValue = value?.ToString() ?? string.Empty;
                    int maxLength = dataLen > 0 ? dataLen : 256;

                    if (stringValue.Length > maxLength)
                    {
                        stringValue = stringValue.Substring(0, maxLength);
                    }
                    return await _client.WriteNodeAsync(dataAddress, stringValue);
                }

                bool success = dataType switch
                {
                    DeviceDataTypeConstants.OBoolean => await _client.WriteNodeAsync(dataAddress, Convert.ToBoolean(value)),
                    DeviceDataTypeConstants.OByte => await _client.WriteNodeAsync(dataAddress, Convert.ToByte(value)),
                    DeviceDataTypeConstants.OSByte => await _client.WriteNodeAsync(dataAddress, Convert.ToSByte(value)),
                    DeviceDataTypeConstants.OInt16 => await _client.WriteNodeAsync(dataAddress, Convert.ToInt16(value)),
                    DeviceDataTypeConstants.OUInt16 => await _client.WriteNodeAsync(dataAddress, Convert.ToUInt16(value)),
                    DeviceDataTypeConstants.OInt32 => await _client.WriteNodeAsync(dataAddress, Convert.ToInt32(value)),
                    DeviceDataTypeConstants.OUInt32 => await _client.WriteNodeAsync(dataAddress, Convert.ToUInt32(value)),
                    DeviceDataTypeConstants.OInt64 => await _client.WriteNodeAsync(dataAddress, Convert.ToInt64(value)),
                    DeviceDataTypeConstants.OUInt64 => await _client.WriteNodeAsync(dataAddress, Convert.ToUInt64(value)),
                    DeviceDataTypeConstants.OFloat => await _client.WriteNodeAsync(dataAddress, Convert.ToSingle(value)),
                    DeviceDataTypeConstants.ODouble => await _client.WriteNodeAsync(dataAddress, Convert.ToDouble(value)),
                    DeviceDataTypeConstants.ODateTime => await _client.WriteNodeAsync(dataAddress, Convert.ToDateTime(value)),
                    DeviceDataTypeConstants.OGuid => await _client.WriteNodeAsync(dataAddress, Guid.Parse(value.ToString() ?? string.Empty)),
                    _ => false // 未支持类型写入失败
                };

                return success;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region ===================== 心跳 =====================

        /// <summary> 向配置的心跳地址写入 Bool/Int16 值以检测连接存活 </summary>
        public async Task<bool> HeartbeatAsync(string dataType, string address)
        {
            if (_client == null || !_client.Connected)
            {
                return false;
            }

            //try
            //{
            //    // 使用服务器状态节点作为心跳
            //    var status = await _client.ReadNodeAsync<string>(HeartbeatNodeId);
            //    return !string.IsNullOrEmpty(status);
            //}
            //catch
            //{
            //    return false;
            //}

            try
            {
                if (dataType == DeviceDataTypeConstants.OBoolean)
                {
                    return await WriteBoolHeartbeat(address); // 写 true，由 PLC 读后再写 false 确认
                }
                else if (dataType == DeviceDataTypeConstants.OInt16)
                {
                    return await WriteIntHeartbeat(address); // 写 0–9 随机数
                }
                else
                {
                    return true; // 其他类型默认视为心跳正常
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Bool 心跳：向设备地址写入 true </summary>
        private async Task<bool> WriteBoolHeartbeat(string address)
        {
            try
            {
                await WriteAsync(address, DeviceDataTypeConstants.OBoolean, true, 0);
            }
            catch (Exception ex)
            {
                throw new Exception("心跳写入异常：" + ex.Message);
            }
            return true;
        }

        /// <summary> Int16 心跳：向设备地址写入 0–9 随机数 </summary>
        private async Task<bool> WriteIntHeartbeat(string address)
        {
            try
            {
                await WriteAsync(address, DeviceDataTypeConstants.OInt16, _random.Next(0, 10), 0);
            }
            catch (Exception ex)
            {
                throw new Exception("心跳写入异常：" + ex.Message);
            }
            return true;
        }

        #endregion

        #region ===================== 订阅管理 =====================

        /// <summary> 订阅单个节点变化，返回订阅 Key（与 nodeId 相同） </summary>
        public string Subscribe(string nodeId, Action<object?> callback)
        {
            if (_client == null || !_client.Connected)
            {
                throw new InvalidOperationException("OPC UA 客户端未连接");
            }

            string key = nodeId; // 直接用节点地址作为 Key

            // 同一 nodeId 可注册多个回调
            _subscriptionCallbacks.AddOrUpdate(nodeId,
                new List<Action<object?>> { callback },
                (id, list) => { list.Add(callback); return list; });

            // 尚未订阅该节点时才向客户端 AddSubscription
            if (!_subscriptionKeys.ContainsKey(key))
            {
                _subscriptionKeys.TryAdd(key, nodeId);

                _client.AddSubscription(key, nodeId, (k, item, args) =>
                {
                    var notification = args.NotificationValue as MonitoredItemNotification;
                    if (notification != null)
                    {
                        var value = notification.Value.WrappedValue.Value;

                        // 触发该节点下全部回调
                        if (_subscriptionCallbacks.TryGetValue(nodeId, out var callbacks))
                        {
                            foreach (var cb in callbacks)
                            {
                                try
                                {
                                    cb(value);
                                }
                                catch { } // 单个回调异常不影响其他订阅者
                            }
                        }
                    }
                });
            }

            return key;
        }

        /// <summary> 取消指定节点的订阅 </summary>
        public void Unsubscribe(string nodeId)
        {
            string key = nodeId;

            if (_subscriptionKeys.TryRemove(key, out _))
            {
                _client?.RemoveSubscription(key);
                _subscriptionCallbacks.TryRemove(nodeId, out _);
            }
        }

        /// <summary> 取消全部订阅并清空回调字典 </summary>
        public void UnsubscribeAll()
        {
            foreach (var key in _subscriptionKeys.Keys.ToList())
            {
                _client?.RemoveSubscription(key);
            }
            _subscriptionKeys.Clear();
            _subscriptionCallbacks.Clear();
        }

        /// <summary> 按地址映射列表批量订阅，值变化时回调 valueHandler(DataName, value) </summary>
        public async Task SubscribeAllAsync(List<device_AddressMapping> mappings, Action<string, object?> valueHandler)
        {
            if (_client == null || !_client.Connected)
            {
                throw new InvalidOperationException("OPC UA 客户端未连接");
            }

            foreach (var mapping in mappings.Where(m => m.IsEnabled))
            {
                if (string.IsNullOrEmpty(mapping.DataAddress)) continue; // 跳过空地址

                Subscribe(mapping.DataAddress, (value) =>
                {
                    valueHandler(mapping.DataName, value); // 将节点值映射为 DataName 推送
                });
            }
            await Task.CompletedTask;
        }

        #endregion

        #region ===================== 连接字符串解析 =====================

        /// <summary>
        /// 解析连接字符串。
        /// 支持：纯 URL、"url=...;auth=anonymous"、"url=...;auth=username;user=...;password=..."
        /// </summary>
        private (string url, string authType, string userName, string password) ParseConnectionString(string connectionString)
        {
            string url = "opc.tcp://localhost:4840";
            string authType = "anonymous";
            string userName = "";
            string password = "";

            if (string.IsNullOrEmpty(connectionString))
                return (url, authType, userName, password);

            if (!connectionString.Contains("="))
            {
                return (connectionString, authType, userName, password); // 无键值对格式，整串当作 URL
            }

            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                {
                    switch (kv[0].ToLower())
                    {
                        case "url":
                            url = kv[1];
                            break;
                        case "auth":
                            authType = kv[1].ToLower();
                            break;
                        case "user":
                            userName = kv[1];
                            break;
                        case "password":
                            password = kv[1];
                            break;
                    }
                }
            }

            return (url, authType, userName, password);
        }

        #endregion

        #region ===================== 类型辅助 =====================

        /// <summary> 读取失败时按数据类型返回默认值 </summary>
        private object? GetDefaultValue(string dataType)
        {
            return dataType switch
            {
                "Boolean" => false,
                "Byte" => (byte)0,
                "SByte" => (sbyte)0,
                "Int16" => (short)0,
                "UInt16" => (ushort)0,
                "Int32" => 0,
                "UInt32" => 0u,
                "Int64" => 0L,
                "UInt64" => 0UL,
                "Float" => 0f,
                "Double" => 0d,
                "String" => string.Empty,
                "DateTime" => DateTime.MinValue,
                "Guid" => Guid.Empty,
                _ => null
            };
        }

        #endregion
    }
}
