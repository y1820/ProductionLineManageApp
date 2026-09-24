using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeviceModule.ViewModels
{
    /// <summary>
    /// 设备看板视图模型
    /// 职责：显示所有工位的设备状态（基于工艺工位配置）
    /// </summary>
    public class DeviceDashboardViewModel : BindableBase, IDisposable
    {
        #region ===================== 私有字段 =====================

        private readonly IEventAggregator _eventAggregator;
        private readonly IDeviceManagementService _deviceManagement;
        private readonly IDataCacheService _cacheService;
        private readonly ILoadingService _loadingService;

        // 数据源
        private List<craft_LineInfo> _lines = new List<craft_LineInfo>();
        private List<craft_StationInfo> _allStations = new List<craft_StationInfo>();
        private Dictionary<int, craft_LineInfo> _lineDict = new Dictionary<int, craft_LineInfo>();
        private Dictionary<int, device_ConnectInfo> _deviceConfigs = new Dictionary<int, device_ConnectInfo>();

        /// <summary> 设备状态（从连接管理器获取）key = 工位Id  </summary>
        private Dictionary<int, DeviceStatus> _deviceStatuses = new Dictionary<int, DeviceStatus>();

        /// <summary>筛选条件 </summary>
        private int _lineSelectedIndex = -1;
        private craft_LineInfo _lineSelectedItem = new craft_LineInfo();
        private List<string> _statusFilters = new List<string> { "全部", "在线", "离线", "报警", "未配置" };
        private int _statusFilterIndex = 0;
        private string _statusFilterItem = "全部";

        /// <summary>显示分组（按产线分组）</summary>
        private ObservableCollection<LineDeviceGroup> _lineGroups = new ObservableCollection<LineDeviceGroup>();

        // 自动刷新定时器
        private System.Timers.Timer? _refreshTimer;
        private string _refreshStatus = "就绪";
        private bool _isRefreshing;

        //手动连接操作
        private bool _startAllConnecting = false;
        private bool _stopAllConnecting = false;


        #endregion

        #region ===================== 公共属性 =====================

        /// <summary>按产线分组的设备卡片集合</summary>
        public ObservableCollection<LineDeviceGroup> LineGroups
        {
            get => _lineGroups;
            set => SetProperty(ref _lineGroups, value);
        }

        /// <summary>产线列表</summary>
        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set
            {
                SetProperty(ref _lines, value);
            }
        }

        /// <summary>产线下拉框选中索引</summary>
        public int LineSelectedIndex
        {
            get => _lineSelectedIndex;
            set => SetProperty(ref _lineSelectedIndex, value);
        }

        /// <summary>产线下拉框选中项</summary>
        public craft_LineInfo LineSelectedItem
        {
            get => _lineSelectedItem;
            set
            {
                if (value == null) return;
                _lineSelectedItem = value;
                LineSelectedIndex = Lines.FindIndex(l => l.Id == value.Id);
                ApplyFilter();
                RaisePropertyChanged();
            }
        }

        /// <summary>状态筛选选项</summary>
        public List<string> StatusFilters
        {
            get => _statusFilters;
            set => SetProperty(ref _statusFilters, value);
        }

        /// <summary>状态筛选索引</summary>
        public int StatusFilterIndex
        {
            get => _statusFilterIndex;
            set => SetProperty(ref _statusFilterIndex, value);
        }

        /// <summary>状态筛选选中项</summary>
        public string StatusFilterItem
        {
            get => _statusFilterItem;
            set
            {
                if (value == null) return;
                _statusFilterItem = value;
                StatusFilterIndex = StatusFilters.IndexOf(value);
                ApplyFilter();
                RaisePropertyChanged();
            }
        }

        /// <summary>刷新状态文字</summary>
        public string RefreshStatus
        {
            get => _refreshStatus;
            set => SetProperty(ref _refreshStatus, value);
        }

        //是否能开始所有连接，有连接配置 and 未连接的设备大于0时
        public bool CanStartAllConnect => LineGroups.Any(l => l.Configured > 0 && l.Unline > 0);
        //存在任意连接成功的数量>0时
        public bool CanStopAllConnect => LineGroups.Any(l => l.Configured > 0 && l.Online > 0);

        #endregion

        #region ===================== 命令 =====================

        /// <summary>手动刷新命令</summary>
        public DelegateCommand RefreshCommand { get; set; }
        // 全局命令
        public DelegateCommand ConnectAllCommand { get; set; }
        public DelegateCommand DisconnectAllCommand { get; set; }

        // 单个工位命令（卡片内使用）
        public DelegateCommand<DeviceCardViewModel> ConnectStationCommand { get; set; }
        public DelegateCommand<DeviceCardViewModel> DisconnectStationCommand { get; set; }

        #endregion

        #region ===================== 构造 =====================

        public DeviceDashboardViewModel(
            IEventAggregator eventAggregator,
            IDeviceManagementService deviceManagement,
            IDataCacheService cacheService,
            ILoadingService loadingService)
        {
            _eventAggregator = eventAggregator;
            _deviceManagement = deviceManagement;
            _cacheService = cacheService;
            _loadingService = loadingService;

            RefreshCommand = new DelegateCommand(OnRefresh);

            // 初始化命令
            ConnectAllCommand = new DelegateCommand(OnConnectAll, () => !_startAllConnecting);//
            DisconnectAllCommand = new DelegateCommand(OnDisconnectAll, () => !_stopAllConnecting);//

            ConnectStationCommand = new DelegateCommand<DeviceCardViewModel>(OnConnectStation);
            DisconnectStationCommand = new DelegateCommand<DeviceCardViewModel>(OnDisconnectStation);

            //初始化 获取缓存更新界面
            InitializeData();
            //订阅
            SubscribeEvents();
            _deviceManagement.DeviceStatusChanged += OnDeviceStatusChanged;
            // 兜底轮询（状态推送为主）
            StartAutoRefresh();
        }

        #endregion

        #region ===================== 初始化 =====================

        /// <summary>
        /// 初始化数据
        /// 从缓存加载产线、工位信息，从连接管理器获取设备状态
        /// </summary>
        private void InitializeData()
        {
            // 加载产线缓存
            if (_cacheService.HasData<List<craft_LineInfo>>())
            {
                Lines = _cacheService.GetData<List<craft_LineInfo>>();
                _lineDict = Lines.ToDictionary(l => l.Id);
            }
            // 加载工位缓存（核心：所有工位都显示，无论是否有设备配置）
            if (_cacheService.HasData<List<craft_StationInfo>>())
            {
                _allStations = _cacheService.GetData<List<craft_StationInfo>>();
            }
            //加载工位连接配置
            if (_cacheService.HasData<List<device_ConnectInfo>>())
            {
                var configs = _cacheService.GetData<List<device_ConnectInfo>>();
                _deviceConfigs = configs.ToDictionary(c => c.StationId);
            }
            // 获取所有设备状态（可能为空，没有配置的工位不会有状态）
            _deviceStatuses = _deviceManagement.GetAllDeviceStatus().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            // 构建分组显示
            BuildLineGroups();
        }

        /// <summary>
        /// 按产线分组构建卡片集合
        /// 核心逻辑：基于工位表，而不是基于设备配置表
        /// </summary>
        private void BuildLineGroups()
        {
            var groups = new List<LineDeviceGroup>();//新建产线组

            foreach (var line in Lines)
            {
                // 获取该产线下的所有工位（从工位表）
                var stationsInLine = _allStations.Where(s => s.LineId == line.Id).ToList();

                if (stationsInLine.Count == 0) continue;

                // 为每个工位创建设备卡片
                var devices = new List<DeviceCardViewModel>();
                foreach (var station in stationsInLine)
                {
                    // 尝试获取设备状态（可能为 null，表示未配置）
                    _deviceStatuses.TryGetValue(station.Id, out var status);

                    var card = new DeviceCardViewModel(station, status, _deviceConfigs);
                    devices.Add(card);
                }
                // 应用筛选
                devices = ApplyFilterToDevices(devices);

                if (devices.Count > 0)
                {
                    groups.Add(new LineDeviceGroup
                    {
                        LineId = line.Id,
                        LineName = line.Name,
                        Devices = new ObservableCollection<DeviceCardViewModel>(devices),
                        IsExpanded = true
                    });
                }
            }

            // 更新分组摘要
            foreach (var group in groups)
            {
                group.UpdateSummary();
            }

            // 更新界面
            LineGroups.Clear();
            foreach (var group in groups)
            {
                LineGroups.Add(group);
            }

            //通知所有连接/停止按钮可见性的属性
            RaisePropertyChanged(nameof(CanStartAllConnect));
            RaisePropertyChanged(nameof(CanStopAllConnect));
        }

        /// <summary>
        /// 应用筛选条件，在线、离线、报警、未配置
        /// </summary>
        private List<DeviceCardViewModel> ApplyFilterToDevices(List<DeviceCardViewModel> devices)
        {
            if (StatusFilterItem == "全部") return devices;

            return StatusFilterItem switch
            {
                "在线" => devices.Where(d => d.ConnectionState == ConnectionState.Connected).ToList(),
                "离线" => devices.Where(d => d.ConnectionState == ConnectionState.Disconnected ||
                                              d.ConnectionState == ConnectionState.Error).ToList(),
                "报警" => devices.Where(d => d.ConnectionState == ConnectionState.Error).ToList(),
                "未配置" => devices.Where(d => !d.HasDeviceConfig).ToList(),
                _ => devices
            };
        }

        #endregion

        #region ===================== 事件订阅 =====================

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeEvents()
        {

            // 工位信息变化事件（工位新增/修改/删除时刷新）
            _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>()
                .Subscribe(OnStationsUpdated, ThreadOption.UIThread);

            // 产线信息变化事件
            _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>()
                .Subscribe(OnLinesUpdated, ThreadOption.UIThread);

            //设备连接配置变化（新增/修改/删除时刷新）
            _eventAggregator.GetEvent<DeviceConnectUpdatedEvent>()
                .Subscribe(OnDeviceConfigsUpdated, ThreadOption.UIThread);
        }

        private void OnDeviceStatusChanged(object? sender, int stationId)
        {
            if (Application.Current?.Dispatcher == null)
                return;

            Application.Current.Dispatcher.InvokeAsync(() => RefreshStationCard(stationId));
        }

        /// <summary>
        /// 工位信息变化回调
        /// </summary>
        private void OnStationsUpdated(List<craft_StationInfo> stations)
        {
            _allStations = stations;
            BuildLineGroups();
        }

        /// <summary>
        /// 产线信息变化回调
        /// </summary>
        private void OnLinesUpdated(List<craft_LineInfo> lines)
        {
            Lines = lines;
            _lineDict = lines.ToDictionary(l => l.Id);
            BuildLineGroups();
        }

        /// <summary> 工位连接配置变化回调 </summary>
        /// <param name="configs"></param>
        private void OnDeviceConfigsUpdated(List<device_ConnectInfo> configs)
        {
            _deviceConfigs = configs.ToDictionary(c => c.StationId);
            BuildLineGroups();
        }
        #endregion

        #region ===================== 筛选逻辑 =====================

        /// <summary>
        /// 应用所有筛选条件
        /// </summary>
        private void ApplyFilter()
        {
            BuildLineGroups();
        }

        #endregion

        #region ===================== 命令 =====================实现

        /// <summary>
        /// 手动刷新
        /// </summary>
        private async void OnRefresh()
        {
            await _loadingService.ExecuteAsync(RefreshDataAsync, "正在加载设备看板...");

        }

        /// <summary>
        /// 刷新数据
        /// </summary>
        private async Task RefreshDataAsync()
        {
            if (_isRefreshing) return;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _isRefreshing = true;
                    RefreshStatus = "正在刷新...";

                    _deviceStatuses = _deviceManagement.GetAllDeviceStatus()
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    foreach (var deviceStatus in _deviceStatuses)
                        RefreshStationCard(deviceStatus.Key, deviceStatus.Value);

                    RefreshStatus = $"刷新完成 {DateTime.Now:HH:mm:ss}";
                }
                catch (Exception ex)
                {
                    RefreshStatus = $"刷新失败: {ex.Message}";
                }
                finally
                {
                    _isRefreshing = false;
                }
            });
        }

        private void RefreshStationCard(int stationId, DeviceStatus? status = null)
        {
            status ??= _deviceManagement.GetDeviceStatus(stationId);
            if (status == null)
                return;

            _deviceStatuses[stationId] = status;

            var lineId = (_allStations.FirstOrDefault(s => s.Id == stationId) ?? new craft_StationInfo()).LineId;
            if (lineId == 0)
                return;

            var line = LineGroups.FirstOrDefault(l => l.LineId == lineId);
            if (line == null)
                return;

            var station = line.Devices.FirstOrDefault(s => s.StationId == stationId);
            if (station == null)
                return;

            station.UpdateStatus(status);
            line.UpdateSummary();
            RaisePropertyChanged(nameof(CanStartAllConnect));
            RaisePropertyChanged(nameof(CanStopAllConnect));
        }



        private async void OnConnectAll()
        {
            _startAllConnecting = true;
            ConnectAllCommand?.RaiseCanExecuteChanged();
            await _loadingService.ExecuteAsync(async () =>
            {
                await _deviceManagement.StartAllDevicesAsync();
            }, "正在连接所有设备...");
            _startAllConnecting = false;
            ConnectAllCommand?.RaiseCanExecuteChanged();
            //通知所有连接/停止按钮可见性的属性
            RaisePropertyChanged(nameof(CanStartAllConnect));
            RaisePropertyChanged(nameof(CanStopAllConnect));

        }

        private async void OnDisconnectAll()
        {
            var result = HandyControl.Controls.MessageBox.Show(
                "确定要断开所有设备连接吗？",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;
            _stopAllConnecting = false;
            DisconnectAllCommand?.RaiseCanExecuteChanged();

            await _loadingService.ExecuteAsync(async () =>
            {
                await _deviceManagement.StopAllDevicesAsync();
            }, "正在断开所有设备...");
            _stopAllConnecting = false;
            DisconnectAllCommand?.RaiseCanExecuteChanged();
            //通知所有连接/停止按钮可见性的属性
            RaisePropertyChanged(nameof(CanStartAllConnect));
            RaisePropertyChanged(nameof(CanStopAllConnect));
        }

        private async void OnConnectStation(DeviceCardViewModel card)
        {
            if (card == null || card.StationId == 0) return;

            // 获取该工位的设备配置
            var config = GetDeviceConfigByStationId(card.StationId);
            if (config == null)
            {
                HandyControl.Controls.MessageBox.Show("未找到该工位的设备配置", "提示");
                return;
            }

            await _loadingService.ExecuteAsync(async () =>
            {
                await _deviceManagement.StartDeviceAsync(config.StationId);
            }, $"正在连接 {card.StationName}...");
        }

        private async void OnDisconnectStation(DeviceCardViewModel card)
        {
            if (card == null || card.StationId == 0) return;

            var result = HandyControl.Controls.MessageBox.Show(
                $"确定要断开 {card.StationName} 的连接吗？",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                await _deviceManagement.StopDeviceAsync(card.StationId);
            }, $"正在断开 {card.StationName}...");
        }

        private device_ConnectInfo? GetDeviceConfigByStationId(int stationId)
        {
            // 从缓存获取设备配置
            var configs = _cacheService.GetData<List<device_ConnectInfo>>();
            return configs?.FirstOrDefault(c => c.StationId == stationId);
        }

        private bool CanConnectAll()
        {
            // 有任何一个工位未连接时才能执行
            return _deviceStatuses.Any(d => d.Value.ConnectionState != ConnectionState.Connected);
        }

        private bool CanDisconnectAll()
        {
            // 有任何一个工位已连接时才能执行
            return _deviceStatuses.Any(d => d.Value.ConnectionState == ConnectionState.Connected);
        }

        #endregion

        #region ===================== 自动刷新 =====================

        /// <summary>
        /// 兜底自动刷新（30 秒一次；连接/运行业务状态以 DeviceStatusChanged 推送为主）
        /// </summary>
        private void StartAutoRefresh()
        {
            _refreshTimer = new System.Timers.Timer(30000);
            _refreshTimer.Elapsed += async (s, e) =>
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    await RefreshDataAsync();
                });
            };
            _refreshTimer.Start();
        }

        /// <summary>
        /// 停止自动刷新
        /// </summary>
        private void StopAutoRefresh()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
        }

        #endregion

        #region ===================== IDisposable =====================

        public void Dispose()
        {
            _deviceManagement.DeviceStatusChanged -= OnDeviceStatusChanged;
            StopAutoRefresh();
        }

        #endregion
    }

    /// <summary>
    /// 产线下设备分组
    /// </summary>
    public class LineDeviceGroup : BindableBase
    {
        public int LineId { get; set; }
        public string LineName { get; set; } = string.Empty;
        public ObservableCollection<DeviceCardViewModel> Devices { get; set; } = new ObservableCollection<DeviceCardViewModel>();

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private string _statusSummary = string.Empty;
        public string StatusSummary
        {
            get => _statusSummary;
            set => SetProperty(ref _statusSummary, value);
        }
        /// <summary> 在线数量 </summary>
        public int Online => Devices.Count(d => d.ConnectionState == ConnectionState.Connected);//统计连接状态数量
        public int Unline => Devices.Count(d => d.ConnectionState == ConnectionState.Disconnected);//统计未连接状态数量
        /// <summary> 已配置数量 </summary>
        public int Configured => Devices.Count(d => d.HasDeviceConfig);//统计有连接配置的数量
        public void UpdateSummary()
        {
            var total = Devices.Count;
            var online = Devices.Count(d => d.ConnectionState == ConnectionState.Connected);//统计连接状态数量
            var unconfigured = Devices.Count(d => !d.HasDeviceConfig ||
            d.ConnectionState == ConnectionState.Connecting ||
            d.ConnectionState == ConnectionState.Reconnecting);//统计没有连接配置的数量

            StatusSummary = $"在线: {online} / 总数: {total}";
            if (unconfigured > 0)
            {
                StatusSummary += $" (未配置: {unconfigured})";
            }
        }
    }

    /// <summary>
    /// 设备卡片视图模型
    /// 基于工位创建，支持无设备配置状态
    /// </summary>
    public class DeviceCardViewModel : BindableBase
    {
        // 工位基础信息
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;

        // 设备连接状态
        private ConnectionState _connectionState;
        public ConnectionState ConnectionState
        {
            get => _connectionState;
            set => SetProperty(ref _connectionState, value);
        }

        // 运行状态
        private RunState _runState;
        public RunState RunState
        {
            get => _runState;
            set => SetProperty(ref _runState, value);
        }

        // 是否已配置设备连接
        private bool _hasDeviceConfig;
        public bool HasDeviceConfig
        {
            get => _hasDeviceConfig;
            set => SetProperty(ref _hasDeviceConfig, value);
        }

        // 设备编码（未配置时为空）
        private string _deviceCode = "--";
        public string DeviceCode
        {
            get => _deviceCode;
            set => SetProperty(ref _deviceCode, value);
        }

        // 流水码
        private string _currentFlowCode = "--";
        public string CurrentFlowCode
        {
            get => _currentFlowCode;
            set => SetProperty(ref _currentFlowCode, value);
        }

        // 今日产量
        private int _todayProcessedCount;
        public int TodayProcessedCount
        {
            get => _todayProcessedCount;
            set => SetProperty(ref _todayProcessedCount, value);
        }

        // 当前型号
        private string _productTypeName = "--";
        public string ProductTypeName
        {
            get => _productTypeName;
            set => SetProperty(ref _productTypeName, value);
        }

        // 工单号
        private string _workOrder = "--";
        public string WorkOrder
        {
            get => _workOrder;
            set => SetProperty(ref _workOrder, value);
        }

        //设备最后请求指令
        private string _deviceLastCommand = "--";
        public string DeviceLastCommand
        {
            get => _deviceLastCommand;
            set => SetProperty(ref _deviceLastCommand, value);
        }
        private string _lastDeviceCommandTime = string.Empty;
        public string LastDeviceCommandTime
        {
            get => _lastDeviceCommandTime;
            set => SetProperty(ref _lastDeviceCommandTime, value);
        }

        // 最后发送指令
        private string _lastCommand = "--";
        public string LastCommand
        {
            get => _lastCommand;
            set => SetProperty(ref _lastCommand, value);
        }
        private string _lastCommandTime = string.Empty;
        public string LastCommandTime
        {
            get => _lastCommandTime;
            set => SetProperty(ref _lastCommandTime, value);
        }

        // 最后心跳时间
        private string _lastHeartbeat = string.Empty;
        public string LastHeartbeat
        {
            get => _lastHeartbeat;
            set => SetProperty(ref _lastHeartbeat, value);
        }

        // 最后错误
        private string _lastError = string.Empty;
        public string LastError
        {
            get => _lastError;
            set => SetProperty(ref _lastError, value);
        }

        //手动连接断开按钮可视化
        private bool _canConnect = false;
        public bool CanConnect
        {
            get => _canConnect;
            set => SetProperty(ref _canConnect, value);
        }
        private bool _canDisconnect = false;
        public bool CanDisconnect
        {
            get => _canDisconnect;
            set => SetProperty(ref _canDisconnect, value);
        }

        #region ===================== 计算属性 =====================

        /// <summary>连接状态图标</summary>
        public string ConnectionStateIcon => HasDeviceConfig ? (ConnectionState switch
        {
            ConnectionState.Connected => "●",
            ConnectionState.Disconnected => "○",
            ConnectionState.Connecting => "◐",
            ConnectionState.Reconnecting => "◑",
            ConnectionState.Error => "⚠",
            _ => "○"
        }) : "○";

        /// <summary>连接状态颜色</summary>
        public Brush ConnectionStateColor => HasDeviceConfig ? (ConnectionState switch
        {
            ConnectionState.Connected => new SolidColorBrush(Colors.Green),
            ConnectionState.Disconnected => new SolidColorBrush(Colors.Gray),
            ConnectionState.Connecting => new SolidColorBrush(Colors.Orange),
            ConnectionState.Reconnecting => new SolidColorBrush(Colors.OrangeRed),
            ConnectionState.Error => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Gray)
        }) : new SolidColorBrush(Colors.Gray);

        /// <summary>连接状态文本</summary>
        public string ConnectionStateText
        {
            get
            {
                if (!HasDeviceConfig) return "未配置";
                return ConnectionState switch
                {
                    ConnectionState.Connected => "在线",
                    ConnectionState.Disconnected => "离线",
                    ConnectionState.Connecting => "连接中",
                    ConnectionState.Reconnecting => "重连中",
                    ConnectionState.Error => "错误",
                    _ => "未知"
                };
            }
        }

        /// <summary>运行状态颜色</summary>
        public Brush RunStateColor => RunState switch
        {
            RunState.Running => new SolidColorBrush(Colors.Green),
            RunState.Idle => new SolidColorBrush(Colors.Blue),
            RunState.Stopped => new SolidColorBrush(Colors.Gray),
            RunState.Alarm => new SolidColorBrush(Colors.Red),
            RunState.Maintenance => new SolidColorBrush(Colors.Orange),
            _ => new SolidColorBrush(Colors.Gray)
        };

        /// <summary>运行状态文本</summary>
        public string RunStateText => RunState switch
        {
            RunState.Running => "运行中",
            RunState.Idle => "待机",
            RunState.Stopped => "停止",
            RunState.Alarm => "报警",
            RunState.Maintenance => "维护中",
            _ => "未知"
        };

        /// <summary>最后通讯时间显示</summary>
        public string LastHeartbeatDisplay => LastHeartbeat ?? "--";

        /// <summary>是否有型号</summary>
        public bool HasProductType => ProductTypeName != "--";

        /// <summary>是否有工单</summary>
        public bool HasWorkOrder => WorkOrder != "--";

        /// <summary>是否设备有最后指令</summary>
        public bool HasDeviceLastCommand => DeviceLastCommand != "--";

        /// <summary>是否有最后指令</summary>
        public bool HasLastCommand => LastCommand != "--";

        /// <summary>是否有错误</summary>
        public bool HasError => !string.IsNullOrEmpty(LastError);

        #endregion

        /// <summary>
        /// 构造函数 - 基于工位和可选的设备状态创建卡片
        /// </summary>
        /// <param name="station">工位信息</param>
        /// <param name="status">设备状态（可能为 null）</param>
        public DeviceCardViewModel(craft_StationInfo station, DeviceStatus? status,
            Dictionary<int, device_ConnectInfo> deviceConfigs)
        {
            deviceConfigs ??= new Dictionary<int, device_ConnectInfo>();
            // 判断是否有设备配置（从配置字典判断）
            bool hasConfig = deviceConfigs.ContainsKey(station.Id);
            StationId = station.Id;
            StationName = station.DisplayText;
            HasDeviceConfig = hasConfig;

            if (status != null && hasConfig)
            {
                UpdateStatus(status);
            }
            else if (hasConfig)
            {
                // 有配置但未连接（连接管理器未启动或连接失败）
                ConnectionState = ConnectionState.Disconnected;
                RunState = RunState.Stopped;
                DeviceCode = deviceConfigs.GetValueOrDefault(station.Id)?.DeviceCode ?? "--";
                CurrentFlowCode = "--";
                TodayProcessedCount = 0;
                ProductTypeName = "--";
                WorkOrder = "--";
                LastCommand = "--";
                LastHeartbeat = "--";
                LastError = "设备离线";
            }
            else
            {
                // 未配置设备连接
                ConnectionState = ConnectionState.Disconnected;
                RunState = RunState.Stopped;
                DeviceCode = "--";
                CurrentFlowCode = "--";
                TodayProcessedCount = 0;
                ProductTypeName = "--";
                WorkOrder = "--";
                LastCommand = "--";
                LastHeartbeat = "--";
                LastError = "未配置设备连接";
            }
        }

        /// <summary>
        /// 更新设备状态
        /// </summary>
        public void UpdateStatus(DeviceStatus status)
        {
            if (status == null) return;

            HasDeviceConfig = true;
            DeviceCode = status.DeviceCode;
            ConnectionState = status.ConnectionState;
            RunState = status.RunState;
            CurrentFlowCode = string.IsNullOrEmpty(status.CurrentFlowCode) ? "--" : status.CurrentFlowCode;
            TodayProcessedCount = status.TodayProcessedCount;
            ProductTypeName = string.IsNullOrEmpty(status.ProductTypeName) ? "--" : status.ProductTypeName;
            WorkOrder = string.IsNullOrEmpty(status.WorkOrder) ? "--" : status.WorkOrder;
            DeviceLastCommand = string.IsNullOrEmpty(status.DeviceLastCommand) ? "--" : status.DeviceLastCommand;
            LastDeviceCommandTime = status.DeviceLastCommandTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
            LastCommand = string.IsNullOrEmpty(status.LastCommand) ? "--" : status.LastCommand;
            LastCommandTime = status.LastCommandTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
            LastHeartbeat = status.LastHeartbeat?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
            LastError = string.IsNullOrEmpty(status.LastError) ? string.Empty : status.LastError;

            CanDisconnect = ConnectionState == ConnectionState.Connected ||
                ConnectionState == ConnectionState.Connecting ||
                ConnectionState == ConnectionState.Reconnecting;
            CanConnect = ConnectionState == ConnectionState.Disconnected;
            // 触发所有计算属性更新
            RaiseAllPropertiesChanged();
        }

        private void RaiseAllPropertiesChanged()
        {
            RaisePropertyChanged(nameof(ConnectionState));
            RaisePropertyChanged(nameof(RunState));
            RaisePropertyChanged(nameof(DeviceCode));
            RaisePropertyChanged(nameof(CurrentFlowCode));
            RaisePropertyChanged(nameof(TodayProcessedCount));
            RaisePropertyChanged(nameof(ProductTypeName));
            RaisePropertyChanged(nameof(WorkOrder));
            RaisePropertyChanged(nameof(DeviceLastCommand));
            RaisePropertyChanged(nameof(LastCommand));
            RaisePropertyChanged(nameof(LastHeartbeat));
            RaisePropertyChanged(nameof(LastError));

            // 计算属性
            RaisePropertyChanged(nameof(ConnectionStateIcon));
            RaisePropertyChanged(nameof(ConnectionStateColor));
            RaisePropertyChanged(nameof(ConnectionStateText));
            RaisePropertyChanged(nameof(RunStateColor));
            RaisePropertyChanged(nameof(RunStateText));
            RaisePropertyChanged(nameof(LastHeartbeatDisplay));
            RaisePropertyChanged(nameof(HasProductType));
            RaisePropertyChanged(nameof(HasWorkOrder));
            RaisePropertyChanged(nameof(HasDeviceLastCommand));
            RaisePropertyChanged(nameof(HasLastCommand));
            RaisePropertyChanged(nameof(HasError));

            //连接和断开按钮显示
            RaisePropertyChanged(nameof(CanDisconnect));
            RaisePropertyChanged(nameof(CanConnect));
        }
    }
}
