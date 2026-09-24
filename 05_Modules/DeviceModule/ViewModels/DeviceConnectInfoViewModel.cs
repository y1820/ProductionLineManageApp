using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DeviceModule.ViewModels
{
    /// <summary>
    /// 设备连接配置视图模型
    /// 职责：负责 device_ConnectInfo 的增删改查（地址映射见 AddressMappingViewModel）
    /// </summary>
    public class DeviceConnectInfoViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================
        /// <summary>弹窗服务</summary>
        private readonly IDialogService _dialogService;
        /// <summary>事件聚合器</summary>
        private readonly IEventAggregator _eventAggregator;
        /// <summary>数据缓存服务</summary>
        private readonly IDataCacheService _cacheService;
        /// <summary>设备连接数据库操作服务</summary>
        private readonly IRepository<device_ConnectInfo> _configRepo;
        /// <summary>加载动画服务</summary>
        private readonly ILoadingService _loadingService;

        // 数据源
        /// <summary>所有设备连接配置</summary>
        private List<device_ConnectInfo> _allConfigs = new List<device_ConnectInfo>();
        /// <summary>所有产线信息</summary>
        private List<craft_LineInfo> _lines = new List<craft_LineInfo>();
        /// <summary>所有工位信息</summary>
        private List<craft_StationInfo> _allStations = new List<craft_StationInfo>();

        // 字典（加速查找）
        /// <summary>所有工位信息字典格式</summary>
        private Dictionary<int, craft_StationInfo> _stationDict = new Dictionary<int, craft_StationInfo>();

        // 筛选相关
        /// <summary>产线下拉框选择对象的索引值</summary>
        private int _lineSelectedIndex = -1;
        /// <summary>产线下拉框选择的对象</summary>
        private craft_LineInfo _lineSelectedItem = new craft_LineInfo();
        /// <summary>当前产线下的工位下拉框选择对象的索引值</summary>
        private int _stationSelectedIndex = -1;
        /// <summary>当前产线下的工位下拉框选择的对象</summary>
        private craft_StationInfo _stationSelectedItem = new craft_StationInfo();

        /// <summary> 当前产线下的工位列表</summary>
        private List<craft_StationInfo> _lineStations = new List<craft_StationInfo>();
        /// <summary>工位下拉框是否启用</summary>
        private bool _isStationEnabled = false;

        /// <summary> 界面显示设备连接信息集合</summary>
        private ObservableCollection<DeviceConfigDisplayItem> _deviceConfigs = new ObservableCollection<DeviceConfigDisplayItem>();
        /// <summary> 刷新中</summary>
        private bool _isRefreshing;
        /// <summary> 是否为自己发布</summary>
        private bool _isIPublish;

        #endregion

        #region ===================== 公共属性 =====================
        /// <summary> 界面显示设备连接信息集合</summary>
        public ObservableCollection<DeviceConfigDisplayItem> DeviceConfigs
        {
            get => _deviceConfigs;
            set => SetProperty(ref _deviceConfigs, value);
        }
        /// <summary> 界面显示产线集合</summary>
        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set => SetProperty(ref _lines, value);
        }
        /// <summary> 当前产线下的工位列表</summary>
        public List<craft_StationInfo> LineStations
        {
            get => _lineStations;
            set => SetProperty(ref _lineStations, value);
        }
        /// <summary>产线下拉框选择对象的索引值</summary>
        public int LineSelectedIndex
        {
            get => _lineSelectedIndex;
            set => SetProperty(ref _lineSelectedIndex, value);
        }
        /// <summary>产线下拉框选择的对象</summary>
        public craft_LineInfo LineSelectedItem
        {
            get => _lineSelectedItem;
            set
            {
                if (value == null) return;
                _lineSelectedItem = value;
                //手动更新索引值
                LineSelectedIndex = Lines.FindIndex(l => l.Id == value.Id);
                //筛选当前产线下的工位
                FilterStationsByLine();
                //异步加载设备连接配置
                _ = FilterConfigsAsync();
                //属性通知
                RaisePropertyChanged();
            }
        }
        /// <summary>当前产线下的工位下拉框选择对象的索引值</summary>
        public int StationSelectedIndex
        {
            get => _stationSelectedIndex;
            set => SetProperty(ref _stationSelectedIndex, value);
        }
        /// <summary>当前产线下的工位下拉框选择的对象</summary>
        public craft_StationInfo StationSelectedItem
        {
            get => _stationSelectedItem;
            set
            {
                if (value == null) return;
                _stationSelectedItem = value;
                //手动更新索引值
                StationSelectedIndex = LineStations.FindIndex(s => s.Id == value.Id);
                //异步加载设备连接配置
                _ = FilterConfigsAsync();
                //属性通知
                RaisePropertyChanged();
            }
        }
        /// <summary> 刷新中</summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }
        /// <summary>工位下拉框是否启用</summary>
        public bool IsStationEnabled
        {
            get => _isStationEnabled;
            set => SetProperty(ref _isStationEnabled, value);
        }
        #endregion

        #region ===================== 命令 =====================
        /// <summary> 新增按钮 </summary>
        public DelegateCommand AddCommand { get; set; }
        /// <summary> 编辑按钮 </summary>
        public DelegateCommand<DeviceConfigDisplayItem> EditCommand { get; set; }
        /// <summary> 删除按钮 </summary>
        public DelegateCommand<DeviceConfigDisplayItem> DeleteCommand { get; set; }
        /// <summary> 刷新按钮 </summary>
        public DelegateCommand RefreshCommand { get; set; }

        #endregion

        #region ===================== 构造 =====================
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dialogService">弹窗服务注入</param>
        /// <param name="eventAggregator">事件聚合器注入</param>
        /// <param name="cacheService">数据缓存服务注入</param>
        /// <param name="configRepo">设备连接数据库操作服务注入</param>
        /// <param name="loadingService">加载动画服务注入</param>
        public DeviceConnectInfoViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<device_ConnectInfo> configRepo,
            ILoadingService loadingService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _configRepo = configRepo;
            _loadingService = loadingService;

            AddCommand = new DelegateCommand(OnAdd);
            EditCommand = new DelegateCommand<DeviceConfigDisplayItem>(OnEdit);
            DeleteCommand = new DelegateCommand<DeviceConfigDisplayItem>(OnDelete);
            RefreshCommand = new DelegateCommand(OnRefresh);

            InitializeData();
        }

        #endregion

        #region ===================== 初始化 =====================
        /// <summary> 初始化数据 </summary>
        private void InitializeData()
        {
            // 加载产线缓存
            if (_cacheService.HasData<List<craft_LineInfo>>())
            {
                Lines = _cacheService.GetData<List<craft_LineInfo>>();
            }

            // 加载工位缓存
            if (_cacheService.HasData<List<craft_StationInfo>>())
            {
                _allStations = _cacheService.GetData<List<craft_StationInfo>>();
                _stationDict = _allStations.ToDictionary(s => s.Id);
            }

            // 加载设备连接配置缓存
            if (_cacheService.HasData<List<device_ConnectInfo>>())
            {
                _allConfigs = _cacheService.GetData<List<device_ConnectInfo>>();
            }

            // 显示数据
            _ = DisplayConfigsAsync();

            // 订阅事件
            _eventAggregator.GetEvent<DeviceConnectUpdatedEvent>()
                .Subscribe(OnConfigsUpdated, ThreadOption.UIThread);
            _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>()
                .Subscribe(OnLinesUpdated, ThreadOption.UIThread);
            _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>()
                .Subscribe(OnStationsUpdated, ThreadOption.UIThread);
        }

        /// <summary>根据选中的产线筛选工位列表</summary>
        private void FilterStationsByLine()
        {
            if (LineSelectedItem?.Id > 0)
            {
                LineStations = _allStations
                    .Where(s => s.LineId == LineSelectedItem.Id)
                    .OrderBy(s => s.Code).ToList();
                IsStationEnabled = LineStations.Any();
            }
            else
            {
                LineStations = new List<craft_StationInfo>();
                IsStationEnabled = false;
            }

            StationSelectedIndex = -1;
            StationSelectedItem = new craft_StationInfo();
        }

        #endregion

        #region ===================== 事件回调 =====================

        private void OnConfigsUpdated(List<device_ConnectInfo> configs)
        {
            if (_isIPublish)
            {
                _isIPublish = false;
                return;
            }
            _allConfigs = configs;
            _ = DisplayConfigsAsync();
        }

        private void OnLinesUpdated(List<craft_LineInfo> lines)
        {
            Lines = lines;
        }

        private void OnStationsUpdated(List<craft_StationInfo> stations)
        {
            _allStations = stations;
            _stationDict = stations.ToDictionary(s => s.Id);
            FilterStationsByLine();
            _ = DisplayConfigsAsync();
        }

        #endregion

        #region ===================== 数据筛选与显示 =====================
        /// <summary>异步加载设备连接信息到界面</summary>
        private async Task FilterConfigsAsync()
        {
            await _loadingService.ExecuteAsync(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (LineSelectedItem?.Id == 0)
                    {
                        DeviceConfigs.Clear();
                        return;
                    }

                    var filtered = _allConfigs.AsEnumerable();

                    // 按产线筛选
                    if (LineSelectedItem?.Id > 0)
                    {
                        filtered = filtered.Where(c =>
                            _stationDict.TryGetValue(c.StationId, out var station) &&
                            station.LineId == LineSelectedItem.Id);
                    }

                    // 按工位筛选
                    if (StationSelectedItem?.Id > 0)
                    {
                        filtered = filtered.Where(c => c.StationId == StationSelectedItem.Id);
                    }

                    var displayItems = filtered
                        .Select(c => new DeviceConfigDisplayItem
                        {
                            RawData = c,
                            StationName = _stationDict.GetValueOrDefault(c.StationId)?.DisplayText ?? "未知",
                        })
                        .OrderBy(c => c.StationName)
                        .ToList();
                    DeviceConfigs.Clear();
                    foreach (var item in displayItems)
                    {
                        DeviceConfigs.Add(item);
                    }
                });
            }, "正在加载设备配置...");
        }

        private async Task DisplayConfigsAsync()
        {
            await FilterConfigsAsync();
        }

        #endregion

        #region ===================== 命令实现 =====================

        private async void OnRefresh()
        {
            if (IsRefreshing) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    IsRefreshing = true;

                    // 刷新设备配置
                    var freshConfigs = await _configRepo.GetAllAsync();
                    _allConfigs = freshConfigs.ToList();

                    await DisplayConfigsAsync();

                    _isIPublish = true;
                    _eventAggregator.GetEvent<DeviceConnectUpdatedEvent>().Publish(_allConfigs);
                    _cacheService.SetData(_allConfigs);
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show($"刷新失败：{ex.Message}", "错误");
                }
                finally
                {
                    IsRefreshing = false;
                }
            }, "正在刷新数据...");
        }
        /// <summary>新增按钮命令关联方法</summary>
        private void OnAdd()
        {
            var parameters = new DialogParameters();
            parameters.Add("AllConfigs", _allConfigs);
            parameters.Add("Lines", Lines);
            parameters.Add("Stations", _allStations);
            _dialogService.ShowDialog("AddDeviceConnectView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newConfig = result.Parameters.GetValue<device_ConnectInfo>("DeviceConfig");

                    if (newConfig != null)
                    {
                        _allConfigs.Add(newConfig);
                        await DisplayConfigsAsync();

                        _isIPublish = true;
                        _eventAggregator.GetEvent<DeviceConnectUpdatedEvent>().Publish(_allConfigs);
                        _cacheService.SetData(_allConfigs);
                    }
                }
            });
        }
        /// <summary>编辑按钮命令关联方法</summary>
        private void OnEdit(DeviceConfigDisplayItem item)
        {
            if (item == null) return;

            var parameters = new DialogParameters();
            parameters.Add("DeviceConfig", item.RawData);
            parameters.Add("AllConfigs", _allConfigs);
            parameters.Add("Lines", Lines);
            parameters.Add("Stations", _allStations);

            _dialogService.ShowDialog("EditDeviceConnectView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var updatedConfig = result.Parameters.GetValue<device_ConnectInfo>("DeviceConfig");

                    if (updatedConfig != null)
                    {
                        var index = _allConfigs.FindIndex(c => c.Id == updatedConfig.Id);
                        if (index >= 0)
                            _allConfigs[index] = updatedConfig;

                        await DisplayConfigsAsync();

                        _isIPublish = true;
                        _eventAggregator.GetEvent<DeviceConnectUpdatedEvent>().Publish(_allConfigs);
                        _cacheService.SetData(_allConfigs);
                    }
                }
            });
        }
        /// <summary>删除按钮命令关联方法</summary>
        private async void OnDelete(DeviceConfigDisplayItem item)
        {
            if (item == null) return;

            var result = HandyControl.Controls.MessageBox.Show(
                $"确认删除工位 \"{item.StationName}\" 的设备连接配置？\n\n地址映射请在「设备交互地址配置」中单独维护。",
                "提示",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    // 删除设备配置
                    await _configRepo.DeleteAsync(item.Id);

                    _allConfigs.Remove(item.RawData);
                    await DisplayConfigsAsync();

                    _isIPublish = true;
                    _eventAggregator.GetEvent<DeviceConnectUpdatedEvent>().Publish(_allConfigs);
                    _cacheService.SetData(_allConfigs);

                    HandyControl.Controls.MessageBox.Show("删除成功", "提示");
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show($"删除失败：{ex.Message}", "错误");
                }
            }, "正在删除...");
        }

        #endregion
    }

    /// <summary>
    /// 设备配置显示模型
    /// </summary>
    public class DeviceConfigDisplayItem : BindableBase
    {
        public device_ConnectInfo RawData { get; set; } = new device_ConnectInfo();

        public int Id => RawData.Id;
        public int StationId => RawData.StationId;
        public string StationName { get; set; } = string.Empty;
        public string DeviceCode => RawData.DeviceCode;
        public string ProtocolType => RawData.ProtocolType;
        public string InteractionType => RawData.InteractionType;
        public string ConnectionMode => RawData.ConnectionMode;
        public string ConnectionString => RawData.ConnectionString;
        public int ScanIntervalMs => RawData.ScanIntervalMs;
        public int HeartbeatIntervalMs => RawData.HeartbeatIntervalMs;
        public bool IsEnabled => RawData.IsEnabled;
        public bool IsIssueModel => RawData.IsIssueModel;
        public string Remarks => RawData.Remarks;
    }
}
