using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Helpers;
using Prism.Mvvm;

namespace DeviceModule.ViewModels.Dialog
{
    /// <summary>
    /// 设备连接对话框 — 各协议连接字段与可见性（Add/Edit 共用）
    /// </summary>
    public class DeviceConnectProtocolFields : BindableBase
    {
        #region ===================== 私有字段 =====================

        private string _protocolType = DeviceProtocolTypeConstants.S7; // 当前协议类型

        private string _s7CpuType = "1500";
        private string _s7Ip = "192.168.1.100";
        private int _s7Rack;
        private int _s7Slot = 1;

        private string _opcUaUrl = "opc.tcp://192.168.1.100:4840";

        private string _modbusIp = "192.168.1.100";
        private int _modbusPort = 502;
        private int _modbusSlaveId = 1;

        private string _tcpIpHost = "192.168.1.100";
        private int _tcpIpPort = 8080;

        #endregion

        #region ===================== 协议可见性 =====================

        /// <summary> S7 CPU 型号选项 </summary>
        public IReadOnlyList<string> S7CpuTypes => DeviceConnectionStringHelper.S7CpuTypes;

        public bool IsS7Protocol => _protocolType == DeviceProtocolTypeConstants.S7;
        public bool IsOpcUaProtocol => _protocolType == DeviceProtocolTypeConstants.OPCUA;
        public bool IsModbusProtocol => _protocolType == DeviceProtocolTypeConstants.Modbus;
        public bool IsTcpIpProtocol => _protocolType == DeviceProtocolTypeConstants.TCPIP;
        public bool IsSimulatorProtocol => _protocolType == DeviceProtocolTypeConstants.Simulator;

        #endregion

        #region ===================== 连接字段属性 =====================

        /// <summary> S7 CPU 型号 </summary>
        public string S7CpuType
        {
            get => _s7CpuType;
            set => SetProperty(ref _s7CpuType, value);
        }

        public string S7Ip
        {
            get => _s7Ip;
            set => SetProperty(ref _s7Ip, value);
        }

        public int S7Rack
        {
            get => _s7Rack;
            set => SetProperty(ref _s7Rack, value);
        }

        public int S7Slot
        {
            get => _s7Slot;
            set => SetProperty(ref _s7Slot, value);
        }

        public string OpcUaUrl
        {
            get => _opcUaUrl;
            set => SetProperty(ref _opcUaUrl, value);
        }

        public string ModbusIp
        {
            get => _modbusIp;
            set => SetProperty(ref _modbusIp, value);
        }

        public int ModbusPort
        {
            get => _modbusPort;
            set => SetProperty(ref _modbusPort, value);
        }

        public int ModbusSlaveId
        {
            get => _modbusSlaveId;
            set => SetProperty(ref _modbusSlaveId, value);
        }

        public string TcpIpHost
        {
            get => _tcpIpHost;
            set => SetProperty(ref _tcpIpHost, value);
        }

        public int TcpIpPort
        {
            get => _tcpIpPort;
            set => SetProperty(ref _tcpIpPort, value);
        }

        #endregion

        #region ===================== 公共方法 =====================

        /// <summary> 切换协议类型并刷新各协议区域可见性 </summary>
        public void SetProtocolType(string protocol)
        {
            if (string.IsNullOrWhiteSpace(protocol)) return;
            _protocolType = protocol;
            RaiseProtocolVisibilityChanged();
        }

        public void LoadFrom(string protocol, string connectionString)
        {
            SetProtocolType(protocol);

            var model = ToModel();
            DeviceConnectionStringHelper.Parse(protocol, connectionString, model);
            ApplyModel(model);

            if (string.IsNullOrWhiteSpace(connectionString))
                ApplyDefaults(protocol);
        }

        public void ApplyDefaults(string protocol)
        {
            var model = ToModel();
            DeviceConnectionStringHelper.ApplyDefaults(model, protocol);
            ApplyModel(model);
        }

        public string BuildConnectionString() =>
            DeviceConnectionStringHelper.Build(_protocolType, ToModel());

        public bool IsValid() =>
            DeviceConnectionStringHelper.IsValid(_protocolType, ToModel());

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 通知 UI 刷新各协议面板可见性 </summary>
        private void RaiseProtocolVisibilityChanged()
        {
            RaisePropertyChanged(nameof(IsS7Protocol));
            RaisePropertyChanged(nameof(IsOpcUaProtocol));
            RaisePropertyChanged(nameof(IsModbusProtocol));
            RaisePropertyChanged(nameof(IsTcpIpProtocol));
            RaisePropertyChanged(nameof(IsSimulatorProtocol));
        }

        private DeviceConnectProtocolModel ToModel() => new()
        {
            S7CpuType = S7CpuType,
            S7Ip = S7Ip,
            S7Rack = S7Rack,
            S7Slot = S7Slot,
            OpcUaUrl = OpcUaUrl,
            ModbusIp = ModbusIp,
            ModbusPort = ModbusPort,
            ModbusSlaveId = ModbusSlaveId,
            TcpIpHost = TcpIpHost,
            TcpIpPort = TcpIpPort
        };

        private void ApplyModel(DeviceConnectProtocolModel model)
        {
            S7CpuType = model.S7CpuType;
            S7Ip = model.S7Ip;
            S7Rack = model.S7Rack;
            S7Slot = model.S7Slot;
            OpcUaUrl = model.OpcUaUrl;
            ModbusIp = model.ModbusIp;
            ModbusPort = model.ModbusPort;
            ModbusSlaveId = model.ModbusSlaveId;
            TcpIpHost = model.TcpIpHost;
            TcpIpPort = model.TcpIpPort;
        }

        #endregion
    }
}
