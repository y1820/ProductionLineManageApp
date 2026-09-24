using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace DeviceModule.ViewModels.Dialog
{
    /// <summary>
    /// 编辑设备连接弹窗视图模型：修改已有工位 PLC 连接参数。
    /// </summary>
    public class EditDeviceConnectViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================

        /// <summary>设备连接信息数据库操作</summary>
        private readonly IRepository<device_ConnectInfo> _configRepo;

        // 原始数据 接收弹窗参数
        /// <summary>设备连接信息原始数据</summary>
        private device_ConnectInfo _originalData = new device_ConnectInfo();

        // 全量数据（用于重复验证）
        private List<device_ConnectInfo> _allConfigs = new List<device_ConnectInfo>();
        private List<craft_LineInfo> _allLines = new List<craft_LineInfo>();
        private List<craft_StationInfo> _allStations = new List<craft_StationInfo>();

        // 协议类型选项
        private List<string> _protocolTypes = DeviceProtocolTypeConstants.ProtocolTypes.ToList();
        /// <summary>逻辑类型选项</summary>
        private List<string> _logicTypes = InteractionTypeConstants.AllInteractionTypeTypes.ToList();

        // 连接模式
        private bool _isDistributedMode = true;
        private bool _isCentralizedMode = false;

        // 显示字段
        private int _id;
        private DateTime? _createTime;
        private string _stationName = string.Empty;

        // 表单字段
        private string _deviceCode = string.Empty;
        private int _protocolTypeIndex = 0;
        private string _protocolTypeItem = DeviceProtocolTypeConstants.ProtocolTypes[0];
        /// <summary>逻辑类型选择索引值</summary>
        private int _logicTypeIndex = 0;
        /// <summary>逻辑类型选择对象</summary>
        private string _logicTypeItem = InteractionTypeConstants.AllInteractionTypeTypes[0];
        private readonly DeviceConnectProtocolFields _connectionFields = new();
        private int _scanIntervalMs = 500;
        private int _heartbeatIntervalMs = 3000;
        private int _heartbeatTimeoutMs = 10000;
        private int _reconnectIntervalMs = 5000;
        private bool _isEnabled = true;
        private bool _isIssueModel;
        private string _remarks = string.Empty;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入设备连接仓储，初始化命令 </summary>
        public EditDeviceConnectViewModel(IRepository<device_ConnectInfo> configRepo)
        {
            _configRepo = configRepo;

            EditCommand = new DelegateCommand(OnEdit, CanEdit);
            CancelCommand = new DelegateCommand(OnCancel);

            PropertyChanged += (s, e) => EditCommand.RaiseCanExecuteChanged();
            _connectionFields.PropertyChanged += (_, _) => EditCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region ===================== 公共属性 =====================

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public DateTime? CreateTime
        {
            get => _createTime;
            set => SetProperty(ref _createTime, value);
        }

        public string StationName
        {
            get => _stationName;
            set => SetProperty(ref _stationName, value);
        }

        // 协议类型
        public List<string> ProtocolTypes
        {
            get => _protocolTypes;
            set => SetProperty(ref _protocolTypes, value);
        }

        public int ProtocolTypeIndex
        {
            get => _protocolTypeIndex;
            set => SetProperty(ref _protocolTypeIndex, value);
        }

        public string ProtocolTypeItem
        {
            get => _protocolTypeItem;
            set
            {
                if (value == null) return;
                _protocolTypeItem = value;
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

        // 连接模式
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


        // 表单字段
        public string DeviceCode
        {
            get => _deviceCode;
            set => SetProperty(ref _deviceCode, value);
        }

        public int ScanIntervalMs
        {
            get => _scanIntervalMs;
            set => SetProperty(ref _scanIntervalMs, value);
        }

        public int HeartbeatIntervalMs
        {
            get => _heartbeatIntervalMs;
            set => SetProperty(ref _heartbeatIntervalMs, value);
        }

        public int HeartbeatTimeoutMs
        {
            get => _heartbeatTimeoutMs;
            set => SetProperty(ref _heartbeatTimeoutMs, value);
        }

        public int ReconnectIntervalMs
        {
            get => _reconnectIntervalMs;
            set => SetProperty(ref _reconnectIntervalMs, value);
        }

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

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        #endregion

        #region ===================== 命令 =====================

        public DelegateCommand EditCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }
        #endregion

        #region ===================== IDialogAware =====================

        public string Title => "编辑设备连接";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // 获取原始配置
            _originalData = parameters.GetValue<device_ConnectInfo>("DeviceConfig") ?? new device_ConnectInfo();

            // 获取全量数据
            _allConfigs = parameters.GetValue<List<device_ConnectInfo>>("AllConfigs") ?? new List<device_ConnectInfo>();
            var lines = parameters.GetValue<List<craft_LineInfo>>("Lines");
            if (lines != null && lines.Any())
            {
                _allLines = lines;
            }
            _allStations = parameters.GetValue<List<craft_StationInfo>>("Stations") ?? new List<craft_StationInfo>();

            // 初始化显示数据
            Id = _originalData.Id;
            _createTime = _originalData.CreateTime;

            // 获取工位名称
            var station = _allStations.FirstOrDefault(s => s.Id == _originalData.StationId);
            StationName = station?.DisplayText ?? "未知";

            DeviceCode = _originalData.DeviceCode;
            ProtocolTypeItem = _originalData.ProtocolType;
            LogicTypeItem = _originalData.InteractionType;
            ConnectionFields.LoadFrom(_originalData.ProtocolType, _originalData.ConnectionString);
            ScanIntervalMs = _originalData.ScanIntervalMs;
            HeartbeatIntervalMs = _originalData.HeartbeatIntervalMs;
            HeartbeatTimeoutMs = _originalData.HeartbeatTimeoutMs;
            ReconnectIntervalMs = _originalData.ReconnectIntervalMs;
            IsEnabled = _originalData.IsEnabled;
            IsIssueModel = _originalData.IsIssueModel;
            Remarks = _originalData.Remarks ?? string.Empty;
            IsCentralizedMode = (_originalData.ConnectionMode == "Centralized") ? true : false;
            IsDistributedMode = !IsCentralizedMode;
        }

        #endregion

        #region ===================== 私有方法 =====================


        private bool CanEdit()
        {
            return !string.IsNullOrWhiteSpace(DeviceCode) &&
                   ConnectionFields.IsValid();
        }

        private bool IsDuplicate()
        {
            return _allConfigs.Any(c =>
                c.Id != Id &&
                c.StationId == _originalData.StationId);
        }

        private void OnEdit()
        {
            if (IsDuplicate())
            {
                HandyControl.Controls.MessageBox.Show($"工位 \"{StationName}\" 已存在其他设备连接配置", "提示");
                return;
            }

            try
            {
                var connectionMode = IsDistributedMode ? ConnectTypeConstants.Distributed : ConnectTypeConstants.Centralized;

                var entity = new device_ConnectInfo
                {
                    Id = Id,
                    StationId = _originalData.StationId,
                    DeviceCode = DeviceCode,
                    ProtocolType = ProtocolTypeItem,
                    InteractionType = LogicTypeItem,
                    ConnectionMode = connectionMode,
                    ConnectionString = ConnectionFields.BuildConnectionString(),
                    ScanIntervalMs = ScanIntervalMs,
                    HeartbeatIntervalMs = HeartbeatIntervalMs,
                    HeartbeatTimeoutMs = HeartbeatTimeoutMs,
                    ReconnectIntervalMs = ReconnectIntervalMs,
                    MaxReconnectAttempts = _originalData.MaxReconnectAttempts,
                    IsIssueModel = IsIssueModel,
                    IsEnabled = IsEnabled,
                    Remarks = Remarks,
                    CreateTime = CreateTime,
                    UpdateTime = DateTime.Now
                };

                var result = _configRepo.Update(entity);
                if (result > 0)
                {
                    HandyControl.Controls.MessageBox.Show("修改成功");
                    var parameters = new DialogParameters();
                    parameters.Add("DeviceConfig", entity);
                    RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show($"连接信息修改失败，影响数据小于等于0，当前连接信息Id为{entity.Id}", "提示");
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"修改失败：{ex.Message}", "错误");
            }
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #endregion
    }
}
