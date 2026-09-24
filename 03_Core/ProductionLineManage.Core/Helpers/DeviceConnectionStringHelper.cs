using ProductionLineManage.Core.Constants;

namespace ProductionLineManage.Core.Helpers
{
    /// <summary>
    /// 设备连接字符串解析与拼装（与各驱动 ParseConnectionString 格式保持一致）。
    /// </summary>
    public static class DeviceConnectionStringHelper
    {
        #region ===================== 常量 =====================

        /// <summary> S7 可选 CPU 型号列表 </summary>
        public static IReadOnlyList<string> S7CpuTypes { get; } = new[] { "300", "400", "1200", "1500" };

        #endregion

        #region ===================== 默认值与校验 =====================

        /// <summary> 按协议填充默认连接参数 </summary>
        public static void ApplyDefaults(DeviceConnectProtocolModel model, string protocol)
        {
            switch (protocol)
            {
                case DeviceProtocolTypeConstants.S7:
                    model.S7Ip = "192.168.1.100";
                    model.S7CpuType = "1500";
                    model.S7Rack = 0;
                    model.S7Slot = 1;
                    break;
                case DeviceProtocolTypeConstants.OPCUA:
                    model.OpcUaUrl = "opc.tcp://192.168.1.100:4840";
                    break;
                case DeviceProtocolTypeConstants.Modbus:
                    model.ModbusIp = "192.168.1.100";
                    model.ModbusPort = 502;
                    model.ModbusSlaveId = 1;
                    break;
                case DeviceProtocolTypeConstants.TCPIP:
                    model.TcpIpHost = "192.168.1.100";
                    model.TcpIpPort = 8080;
                    break;
            }
        }

        /// <summary> 校验当前协议下连接参数是否有效 </summary>
        public static bool IsValid(string protocol, DeviceConnectProtocolModel model)
        {
            return protocol switch
            {
                DeviceProtocolTypeConstants.S7 =>
                    !string.IsNullOrWhiteSpace(model.S7Ip) && !string.IsNullOrWhiteSpace(model.S7CpuType),
                DeviceProtocolTypeConstants.OPCUA =>
                    !string.IsNullOrWhiteSpace(model.OpcUaUrl),
                DeviceProtocolTypeConstants.Modbus =>
                    !string.IsNullOrWhiteSpace(model.ModbusIp) && model.ModbusPort > 0,
                DeviceProtocolTypeConstants.TCPIP =>
                    !string.IsNullOrWhiteSpace(model.TcpIpHost) && model.TcpIpPort > 0,
                DeviceProtocolTypeConstants.Simulator => true,
                _ => false
            };
        }

        #endregion

        #region ===================== 解析与拼装 =====================

        /// <summary> 从连接字符串解析到模型 </summary>
        public static void Parse(string protocol, string connectionString, DeviceConnectProtocolModel model)
        {
            if (model == null) return;

            switch (protocol)
            {
                case DeviceProtocolTypeConstants.S7:
                    ParseS7(connectionString, model);
                    break;
                case DeviceProtocolTypeConstants.OPCUA:
                    ParseOpcUa(connectionString, model);
                    break;
                case DeviceProtocolTypeConstants.Modbus:
                    ParseKeyValue(connectionString, model, defaults =>
                    {
                        model.ModbusIp = defaults.GetValueOrDefault("ip", "192.168.1.100");
                        model.ModbusPort = int.TryParse(defaults.GetValueOrDefault("port", "502"), out var p) ? p : 502;
                        model.ModbusSlaveId = int.TryParse(defaults.GetValueOrDefault("slave", "1"), out var s) ? s : 1;
                    });
                    break;
                case DeviceProtocolTypeConstants.TCPIP:
                    ParseTcpIp(connectionString, model);
                    break;
            }
        }

        /// <summary> 从模型拼装连接字符串 </summary>
        public static string Build(string protocol, DeviceConnectProtocolModel model)
        {
            return protocol switch
            {
                DeviceProtocolTypeConstants.S7 =>
                    $"ip={model.S7Ip};cpu={model.S7CpuType};rack={model.S7Rack};slot={model.S7Slot}",
                DeviceProtocolTypeConstants.OPCUA =>
                    string.IsNullOrWhiteSpace(model.OpcUaUrl) ? string.Empty : model.OpcUaUrl.Trim(),
                DeviceProtocolTypeConstants.Modbus =>
                    $"ip={model.ModbusIp};port={model.ModbusPort};slave={model.ModbusSlaveId}",
                DeviceProtocolTypeConstants.TCPIP =>
                    $"ip={model.TcpIpHost};port={model.TcpIpPort}",
                DeviceProtocolTypeConstants.Simulator => string.Empty,
                _ => string.Empty
            };
        }

        #endregion

        #region ===================== 协议解析内部方法 =====================

        /// <summary> 解析 S7 连接字符串：ip=;cpu=;rack=;slot= </summary>
        private static void ParseS7(string connectionString, DeviceConnectProtocolModel model)
        {
            model.S7Ip = "192.168.1.100";
            model.S7CpuType = "1500";
            model.S7Rack = 0;
            model.S7Slot = 1;

            if (string.IsNullOrWhiteSpace(connectionString)) return;

            foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;

                switch (kv[0].Trim().ToLowerInvariant())
                {
                    case "ip": model.S7Ip = kv[1].Trim(); break;
                    case "cpu": model.S7CpuType = kv[1].Trim(); break;
                    case "rack": if (int.TryParse(kv[1], out var rack)) model.S7Rack = rack; break;
                    case "slot": if (int.TryParse(kv[1], out var slot)) model.S7Slot = slot; break;
                }
            }
        }

        /// <summary> 解析 OPC UA 连接字符串（整段 URL 或 url= 键值） </summary>
        private static void ParseOpcUa(string connectionString, DeviceConnectProtocolModel model)
        {
            model.OpcUaUrl = "opc.tcp://192.168.1.100:4840";
            if (string.IsNullOrWhiteSpace(connectionString)) return;

            if (!connectionString.Contains('='))
            {
                model.OpcUaUrl = connectionString.Trim(); // 纯 URL 格式
                return;
            }

            foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim().Equals("url", StringComparison.OrdinalIgnoreCase))
                    model.OpcUaUrl = kv[1].Trim();
            }
        }

        /// <summary> 解析 TCP/IP 连接字符串（host:port 或 ip=;port=） </summary>
        private static void ParseTcpIp(string connectionString, DeviceConnectProtocolModel model)
        {
            model.TcpIpHost = "192.168.1.100";
            model.TcpIpPort = 8080;
            if (string.IsNullOrWhiteSpace(connectionString)) return;

            if (!connectionString.Contains('=') && connectionString.Contains(':'))
            {
                var parts = connectionString.Split(':', 2); // host:port 简写
                model.TcpIpHost = parts[0].Trim();
                if (int.TryParse(parts[1], out var port)) model.TcpIpPort = port;
                return;
            }

            ParseKeyValue(connectionString, model, dict =>
            {
                model.TcpIpHost = dict.GetValueOrDefault("ip", "192.168.1.100");
                model.TcpIpPort = int.TryParse(dict.GetValueOrDefault("port", "8080"), out var p) ? p : 8080;
            });
        }

        /// <summary> 通用 key=value; 格式解析 </summary>
        private static void ParseKeyValue(string connectionString, DeviceConnectProtocolModel model, Action<Dictionary<string, string>> apply)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length == 2)
                        dict[kv[0].Trim()] = kv[1].Trim();
                }
            }
            apply(dict);
        }

        #endregion
    }

    /// <summary> 各协议连接字段（ViewModel 绑定用） </summary>
    public class DeviceConnectProtocolModel
    {
        #region --------------------- S7 ---------------------

        /// <summary> S7 CPU 型号 </summary>
        public string S7CpuType { get; set; } = "1500";
        /// <summary> S7 PLC IP 地址 </summary>
        public string S7Ip { get; set; } = "192.168.1.100";
        /// <summary> S7 机架号 </summary>
        public int S7Rack { get; set; }
        /// <summary> S7 槽号 </summary>
        public int S7Slot { get; set; } = 1;

        #endregion

        #region --------------------- OPC UA ---------------------

        /// <summary> OPC UA 端点 URL </summary>
        public string OpcUaUrl { get; set; } = "opc.tcp://192.168.1.100:4840";

        #endregion

        #region --------------------- Modbus ---------------------

        /// <summary> Modbus 设备 IP </summary>
        public string ModbusIp { get; set; } = "192.168.1.100";
        /// <summary> Modbus 端口 </summary>
        public int ModbusPort { get; set; } = 502;
        /// <summary> Modbus 从站地址 </summary>
        public int ModbusSlaveId { get; set; } = 1;

        #endregion

        #region --------------------- TCP/IP ---------------------

        /// <summary> TCP 主机地址 </summary>
        public string TcpIpHost { get; set; } = "192.168.1.100";
        /// <summary> TCP 端口 </summary>
        public int TcpIpPort { get; set; } = 8080;

        #endregion
    }
}
