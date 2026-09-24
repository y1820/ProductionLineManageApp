using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace DeviceModule.ViewModels.Dialog
{
    /// <summary>
    /// 新增设备连接弹窗视图模型：配置工位 PLC 连接参数并写入 device_ConnectInfo。
    /// </summary>
    public class AddDeviceConnectViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================
        /// <summary>设备连接信息数据库操作</summary>
        private readonly IRepository<device_ConnectInfo> _configRepo;

        // 全量数据
        /// <summary>所有设备连接信息</summary>
        private List<device_ConnectInfo> _allConfigs = new List<device_ConnectInfo>();
        /// <summary>所有产线信息</summary>
        private List<craft_LineInfo> _allLines = new List<craft_LineInfo>();
        /// <summary>所有工位信息</summary>
        private List<craft_StationInfo> _allStations = new List<craft_StationInfo>();

        /// <summary>协议类型选项</summary>
        private List<string> _protocolTypes = DeviceProtocolTypeConstants.ProtocolTypes.ToList();
        /// <summary>逻辑类型选项</summary>
        private List<string> _logicTypes = InteractionTypeConstants.AllInteractionTypeTypes.ToList();

        // 连接模式
        /// <summary>分布式</summary>
        private bool _isDistributedMode = true;
        /// <summary>中央PLC</summary>
        private bool _isCentralizedMode = false;

        // 产线筛选
        /// <summary>产线选择索引值</summary>
        private int _lineSelectedIndex = -1;
        /// <summary>产线选择对象</summary>
        private craft_LineInfo _lineSelectedItem = new craft_LineInfo();
        /// <summary>产线下的工位集合</summary>
        private List<craft_StationInfo> _stations = new List<craft_StationInfo>();
        /// <summary>工位选择索引值</summary>
        private int _stationSelectedIndex = -1;
        /// <summary>工位选择对象</summary>
        private craft_StationInfo _stationSelectedItem = new craft_StationInfo();
        /// <summary>工位下拉框是否启用</summary>
        private bool _isStationEnabled = false;

        // 表单字段
        /// <summary>设备编码</summary>
        private string _deviceCode = "PLC_1";
        /// <summary>协议类型选择索引值</summary>
        private int _protocolTypeIndex = 0;
        /// <summary>协议类型选择对象</summary>
        private string _protocolTypeItem = DeviceProtocolTypeConstants.ProtocolTypes[0];
        /// <summary>逻辑类型选择索引值</summary>
        private int _logicTypeIndex = 0;
        /// <summary>逻辑类型选择对象</summary>
        private string _logicTypeItem = InteractionTypeConstants.AllInteractionTypeTypes[0];
        /// <summary>各协议连接字段</summary>
        private readonly DeviceConnectProtocolFields _connectionFields = new();
        /// <summary>扫描周期</summary>
        private int _scanIntervalMs = 500;
        /// <summary>心跳间隔时间</summary>
        private int _heartbeatIntervalMs = 3000;
        /// <summary>心跳超时时间</summary>
        private int _heartbeatTimeoutMs = 10000;
        /// <summary>重连时间</summary>
        private int _reconnectIntervalMs = 5000;
        /// <summary>是否启用</summary>
        private bool _isEnabled = true;
        /// <summary>是否由 SCADA 下发型号</summary>
        private bool _isIssueModel;
        /// <summary>备注</summary>
        private string _remarks = string.Empty;


        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入设备连接仓储，初始化命令 </summary>
        public AddDeviceConnectViewModel(IRepository<device_ConnectInfo> configRepo)
        {
            _configRepo = configRepo;

            AddCommand = new DelegateCommand(OnAdd, CanAdd);
            CancelCommand = new DelegateCommand(OnCancel);

            PropertyChanged += (s, e) => AddCommand.RaiseCanExecuteChanged();
            _connectionFields.PropertyChanged += (_, _) => AddCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region ===================== 公共属性 =====================

        /// <summary>协议类型选项</summary>
        public List<string> ProtocolTypes
        {
            get => _protocolTypes;
            set => SetProperty(ref _protocolTypes, value);
        }
        /// <summary>协议类型选择索引值</summary>
        public int ProtocolTypeIndex
        {
            get => _protocolTypeIndex;
            set => SetProperty(ref _protocolTypeIndex, value);
        }
        /// <summary>协议类型选择对象</summary>
        public string ProtocolTypeItem
        {
            get => _protocolTypeItem;
            set
            {
                if (value == null) return;
                _protocolTypeItem = value;
                //手动更新选择索引值
                ProtocolTypeIndex = ProtocolTypes.IndexOf(value);
                ConnectionFields.SetProtocolType(value);
                RaisePropertyChanged();
            }
        }

        /// <summary>各协议连接字段（绑定 ProtocolConnectionPanel）</summary>
        public DeviceConnectProtocolFields ConnectionFields => _connectionFields;

        /// <summary>逻辑类型选项</summary>
        public List<string> LogicTypes
        {
            get => _logicTypes;
            set => SetProperty(ref _logicTypes, value);
        }
        /// <summary>逻辑类型选择索引值</summary>
        public int LogicTypeIndex
        {
            get => _logicTypeIndex;
            set => SetProperty(ref _logicTypeIndex, value);
        }
        /// <summary>逻辑类型选择对象</summary>
        public string LogicTypeItem
        {
            get => _logicTypeItem;
            set
            {
                if (value == null) return;
                _logicTypeItem = value;
                //手动更新选择索引值
                LogicTypeIndex = LogicTypes.IndexOf(value);
                RaisePropertyChanged();
            }
        }

        /// <summary>是否为分布式连接</summary>
        public bool IsDistributedMode
        {
            get => _isDistributedMode;
            set
            {
                SetProperty(ref _isDistributedMode, value);
                if (value)
                {
                    IsCentralizedMode = false;
                    RaisePropertyChanged(nameof(IsCentralizedMode));
                }
            }
        }
        /// <summary>是否为中央PLC连接</summary>
        public bool IsCentralizedMode
        {
            get => _isCentralizedMode;
            set
            {
                SetProperty(ref _isCentralizedMode, value);
                if (value)
                {
                    IsDistributedMode = false;
                    RaisePropertyChanged(nameof(IsDistributedMode));
                }
            }
        }


        // 产线
        /// <summary>所有产线信息</summary>
        public List<craft_LineInfo> Lines
        {
            get => _allLines;
            set => SetProperty(ref _allLines, value);
        }
        /// <summary>产线选择索引值</summary>
        public int LineSelectedIndex
        {
            get => _lineSelectedIndex;
            set => SetProperty(ref _lineSelectedIndex, value);
        }
        /// <summary>产线选择对象</summary>                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               
        public craft_LineInfo LineSelectedItem
        {
            get => _lineSelectedItem;
            set
            {
                if (value == null) return;
                _lineSelectedItem = value;
                //手动更新索引值
                LineSelectedIndex = Lines.FindIndex(l => l.Id == value.Id);
                //加载对应的工位集合
                FilterStationsByLine();
                RaisePropertyChanged();
            }
        }

        // 工位
        /// <summary>产线下的工位集合</summary>
        public List<craft_StationInfo> Stations
        {
            get => _stations;
            set => SetProperty(ref _stations, value);
        }
        /// <summary>工位选择索引值</summary>
        public int StationSelectedIndex
        {
            get => _stationSelectedIndex;
            set => SetProperty(ref _stationSelectedIndex, value);
        }
        /// <summary>工位选择对象</summary>
        public craft_StationInfo StationSelectedItem
        {
            get => _stationSelectedItem;
            set
            {
                if (value == null) return;
                _stationSelectedItem = value;
                //手动更新索引值
                StationSelectedIndex = Stations.FindIndex(s => s.Id == value.Id);
                //加载地址映射中所能选择的内容

                RaisePropertyChanged();
            }
        }
        /// <summary>工位下拉框是否启用</summary>
        public bool IsStationEnabled
        {
            get => _isStationEnabled;
            set => SetProperty(ref _isStationEnabled, value);
        }

        // 表单字段
        /// <summary>设备编码</summary>
        public string DeviceCode
        {
            get => _deviceCode;
            set => SetProperty(ref _deviceCode, value);
        }
        /// <summary>扫描周期</summary>
        public int ScanIntervalMs
        {
            get => _scanIntervalMs;
            set => SetProperty(ref _scanIntervalMs, value);
        }
        /// <summary>心跳间隔时间</summary>
        public int HeartbeatIntervalMs
        {
            get => _heartbeatIntervalMs;
            set => SetProperty(ref _heartbeatIntervalMs, value);
        }
        /// <summary>心跳超时时间</summary>
        public int HeartbeatTimeoutMs
        {
            get => _heartbeatTimeoutMs;
            set => SetProperty(ref _heartbeatTimeoutMs, value);
        }
        /// <summary>重连时间</summary>
        public int ReconnectIntervalMs
        {
            get => _reconnectIntervalMs;
            set => SetProperty(ref _reconnectIntervalMs, value);
        }
        /// <summary>是否启用</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>是否由 SCADA 下发型号到 PLC</summary>
        public bool IsIssueModel
        {
            get => _isIssueModel;
            set => SetProperty(ref _isIssueModel, value);
        }

        /// <summary>备注</summary>
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        #endregion

        #region ===================== 命令 =====================

        public DelegateCommand AddCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }

        #endregion

        #region ===================== IDialogAware =====================

        public string Title => "新增设备连接";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            //加载所有连接信息
            _allConfigs = parameters.GetValue<List<device_ConnectInfo>>("AllConfigs") ?? new List<device_ConnectInfo>();
            //加载所有产线
            var lines = parameters.GetValue<List<craft_LineInfo>>("Lines");
            if (lines != null && lines.Any())
            {
                Lines = lines;
            }
            //暂存所有工位
            _allStations = parameters.GetValue<List<craft_StationInfo>>("Stations") ?? new List<craft_StationInfo>();

            // 默认值
            IsDistributedMode = true;
            ScanIntervalMs = 500;
            HeartbeatIntervalMs = 3000;
            HeartbeatTimeoutMs = 10000;
            ReconnectIntervalMs = 5000;
            IsEnabled = true;

            ConnectionFields.LoadFrom(ProtocolTypeItem, string.Empty);
        }

        #endregion

        #region ===================== 私有方法 =====================
        /// <summary>加载产线下的工位</summary>
        private void FilterStationsByLine()
        {
            //确认选择的产线对象值有效
            if (LineSelectedItem?.Id > 0)
            {
                // 获取已配置的工位ID集合
                var configuredStationIds = _allConfigs.Select(c => c.StationId).ToHashSet();
                //筛选产线下的工位和没有连接信息的工位
                Stations = _allStations
                    .Where(s => s.LineId == LineSelectedItem.Id &&
                    !configuredStationIds.Contains(s.Id))
                    .OrderBy(s => s.Code).ToList();
                IsStationEnabled = Stations.Any();
            }
            else
            {
                Stations = new List<craft_StationInfo>();
                IsStationEnabled = false;
            }

            StationSelectedIndex = -1;
            StationSelectedItem = new craft_StationInfo();
        }
        /// <summary>是否允许新增按钮启用</summary>
        private bool CanAdd()
        {
            return LineSelectedIndex != -1 &&
                   StationSelectedIndex != -1 &&
                   ConnectionFields.IsValid();
        }
        /// <summary>是否存在重复的工位连接信息</summary>
        private bool IsDuplicate()
        {
            return _allConfigs.Any(c => c.StationId == StationSelectedItem.Id);
        }

        /// <summary>新增方法</summary>
        private void OnAdd()
        {
            if (IsDuplicate())
            {
                HandyControl.Controls.MessageBox.Show($"工位 \"{StationSelectedItem.DisplayText}\" 已存在设备连接配置", "提示");
                return;
            }

            int configId = 0;
            try
            {
                var connectionMode = IsDistributedMode ? ConnectTypeConstants.Distributed : ConnectTypeConstants.Centralized;

                var entity = new device_ConnectInfo
                {
                    StationId = StationSelectedItem.Id,
                    DeviceCode = DeviceCode,
                    ProtocolType = ProtocolTypeItem,
                    InteractionType = LogicTypeItem,
                    ConnectionMode = connectionMode,
                    ConnectionString = ConnectionFields.BuildConnectionString(),
                    ScanIntervalMs = ScanIntervalMs,
                    HeartbeatIntervalMs = HeartbeatIntervalMs,
                    HeartbeatTimeoutMs = HeartbeatTimeoutMs,
                    ReconnectIntervalMs = ReconnectIntervalMs,
                    MaxReconnectAttempts = -1,
                    IsIssueModel = IsIssueModel,
                    IsEnabled = IsEnabled,
                    Remarks = Remarks,
                    CreateTime = DateTime.Now,
                    UpdateTime = DateTime.Now
                };

                var resultId = _configRepo.Insert(entity);
                if (resultId > 0)
                {
                    configId = entity.Id = resultId;
                    HandyControl.Controls.MessageBox.Show("新增成功");

                    var parameters = new DialogParameters();
                    parameters.Add("DeviceConfig", entity);
                    RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show("新增失败", "提示");
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"新增失败：{ex.Message}", "错误");
                if (configId != 0)
                    _configRepo.Delete(configId);
            }
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }
        #endregion
    }


}
