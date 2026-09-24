using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Windows;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>
    /// 加工数据采集地址配置视图模型（列表展示 + 弹窗新增/编辑）
    /// </summary>
    public class DataCollectConfigViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDialogService _dialogService; // 弹窗服务（新增/编辑采集配置）
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataCacheService _cacheService;
        private readonly IRepository<craft_DataCollectConfig> _configRepo;
        private readonly ILoadingService _loadingService;

        private List<craft_DataCollectConfig> _allConfigs = new();
        private List<craft_LineInfo> _lines = new();
        private List<craft_TypeInfo> _types = new();
        private List<craft_StationInfo> _allStations = new();
        private Dictionary<int, craft_StationInfo> _stationDict = new();
        private Dictionary<int, craft_TypeInfo> _typeDict = new();
        private Dictionary<int, craft_LineInfo> _lineDict = new();

        private int _lineSelectedIndex = -1;
        private craft_LineInfo _lineSelectedItem = new();
        private int _stationSelectedIndex = -1;
        private craft_StationInfo _stationSelectedItem = new();
        private int _typeSelectedIndex = -1;
        private craft_TypeInfo _typeSelectedItem = new();
        private List<craft_StationInfo> _lineStations = new();
        private bool _isStationEnabled;
        private bool _isRefreshing;
        private bool _isPublish;

        private ObservableCollection<DataCollectDisplayItem> _displayConfigs = new(); // 界面展示集合

        #endregion

        #region ===================== 公共属性 =====================

        public ObservableCollection<DataCollectDisplayItem> DisplayConfigs
        {
            get => _displayConfigs;
            set => SetProperty(ref _displayConfigs, value);
        }

        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set => SetProperty(ref _lines, value);
        }

        public List<craft_TypeInfo> Types
        {
            get => _types;
            set
            {
                SetProperty(ref _types, value);
                _typeDict = value?.ToDictionary(t => t.Id) ?? new Dictionary<int, craft_TypeInfo>();
            }
        }

        public List<craft_StationInfo> LineStations
        {
            get => _lineStations;
            set => SetProperty(ref _lineStations, value);
        }

        public int LineSelectedIndex
        {
            get => _lineSelectedIndex;
            set => SetProperty(ref _lineSelectedIndex, value);
        }

        public craft_LineInfo LineSelectedItem
        {
            get => _lineSelectedItem;
            set
            {
                if (value == null) return;
                _lineSelectedItem = value;
                LineSelectedIndex = Lines.FindIndex(l => l.Id == value.Id);
                FilterStationsByLine();
                _ = FilterConfigsAsync();
                RaisePropertyChanged();
            }
        }

        public int StationSelectedIndex
        {
            get => _stationSelectedIndex;
            set => SetProperty(ref _stationSelectedIndex, value);
        }

        public craft_StationInfo StationSelectedItem
        {
            get => _stationSelectedItem;
            set
            {
                if (value == null) return;
                _stationSelectedItem = value;
                StationSelectedIndex = LineStations.FindIndex(s => s.Id == value.Id);
                _ = FilterConfigsAsync();
                RaisePropertyChanged();
            }
        }

        public int TypeSelectedIndex
        {
            get => _typeSelectedIndex;
            set => SetProperty(ref _typeSelectedIndex, value);
        }

        public craft_TypeInfo TypeSelectedItem
        {
            get => _typeSelectedItem;
            set
            {
                if (value == null) return;
                _typeSelectedItem = value;
                TypeSelectedIndex = Types.FindIndex(t => t.Id == value.Id);
                _ = FilterConfigsAsync();
                RaisePropertyChanged();
            }
        }

        public bool IsStationEnabled
        {
            get => _isStationEnabled;
            set => SetProperty(ref _isStationEnabled, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public DelegateCommand AddCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand<DataCollectDisplayItem> EditCommand { get; }
        public DelegateCommand<DataCollectDisplayItem> RemoveRowCommand { get; }

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入服务并初始化命令与缓存数据 </summary>
        public DataCollectConfigViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<craft_DataCollectConfig> configRepo,
            ILoadingService loadingService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _configRepo = configRepo;
            _loadingService = loadingService;

            AddCommand = new DelegateCommand(OnAdd);
            RefreshCommand = new DelegateCommand(OnRefresh);
            EditCommand = new DelegateCommand<DataCollectDisplayItem>(OnEdit);
            RemoveRowCommand = new DelegateCommand<DataCollectDisplayItem>(OnRemoveRow);

            InitializeData(); // 从缓存加载并订阅事件
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 从缓存初始化产线/型号/工位/配置数据 </summary>
        private void InitializeData()
        {
            if (_cacheService.HasData<List<craft_LineInfo>>())
            {
                Lines = _cacheService.GetData<List<craft_LineInfo>>();
                _lineDict = Lines.ToDictionary(l => l.Id);
            }

            if (_cacheService.HasData<List<craft_TypeInfo>>())
            {
                Types = _cacheService.GetData<List<craft_TypeInfo>>();
                TypeSelectedIndex = -1;
            }

            if (_cacheService.HasData<List<craft_StationInfo>>())
            {
                _allStations = _cacheService.GetData<List<craft_StationInfo>>();
                _stationDict = _allStations.ToDictionary(s => s.Id);
            }

            if (_cacheService.HasData<List<craft_DataCollectConfig>>())
                _allConfigs = _cacheService.GetData<List<craft_DataCollectConfig>>();

            _ = FilterConfigsAsync();

            _eventAggregator.GetEvent<DataCollectConfigUpdatedEvent>()
                .Subscribe(OnConfigsUpdated, ThreadOption.UIThread);
            _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>()
                .Subscribe(lines =>
                {
                    Lines = lines;
                    _lineDict = lines.ToDictionary(l => l.Id);
                    FilterStationsByLine();
                    _ = FilterConfigsAsync();
                }, ThreadOption.UIThread);
            _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>()
                .Subscribe(types =>
                {
                    Types = types;
                    TypeSelectedIndex = -1;
                    _ = FilterConfigsAsync();
                }, ThreadOption.UIThread);
            _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>()
                .Subscribe(stations =>
                {
                    _allStations = stations;
                    _stationDict = stations.ToDictionary(s => s.Id);
                    FilterStationsByLine();
                    _ = FilterConfigsAsync();
                }, ThreadOption.UIThread);
        }

        private void OnConfigsUpdated(List<craft_DataCollectConfig> configs)
        {
            if (_isPublish)
            {
                _isPublish = false;
                return;
            }

            _allConfigs = configs;
            _ = FilterConfigsAsync();
        }

        private void FilterStationsByLine()
        {
            if (LineSelectedItem?.Id > 0)
            {
                LineStations = _allStations
                    .Where(s => s.LineId == LineSelectedItem.Id)
                    .OrderBy(s => s.Code)
                    .ToList();
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

        private async Task FilterConfigsAsync()
        {
            await _loadingService.ExecuteAsync(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var filtered = _allConfigs.AsEnumerable();

                    if (LineSelectedItem?.Id > 0)
                    {
                        filtered = filtered.Where(c =>
                            c.LineId == LineSelectedItem.Id ||
                            (_stationDict.TryGetValue(c.StationId, out var station) &&
                             station.LineId == LineSelectedItem.Id));
                    }

                    if (StationSelectedItem?.Id > 0)
                        filtered = filtered.Where(c => c.StationId == StationSelectedItem.Id);

                    if (TypeSelectedItem?.Id > 0)
                        filtered = filtered.Where(c => c.ProductTypeId == TypeSelectedItem.Id);

                    DisplayConfigs = new ObservableCollection<DataCollectDisplayItem>(
                        filtered
                            .OrderBy(c => c.StationId)
                            .ThenBy(c => c.ProductTypeId)
                            .ThenBy(c => c.DataName)
                            .Select(ToDisplayItem));
                });
            }, "正在加载数据采集配置...");
        }

        private DataCollectDisplayItem ToDisplayItem(craft_DataCollectConfig config)
        {
            _stationDict.TryGetValue(config.StationId, out var station);
            _typeDict.TryGetValue(config.ProductTypeId, out var type);
            _lineDict.TryGetValue(config.LineId, out var line);

            return new DataCollectDisplayItem
            {
                RawData = config,
                StationName = station?.DisplayText ?? "未知",
                ProductTypeName = type?.Name ?? "未知",
                LineName = line?.Name ?? (config.LineId > 0 ? config.LineId.ToString() : "-")
            };
        }

        private void OnAdd()
        {
            var parameters = new DialogParameters();
            parameters.Add("Types", Types);
            parameters.Add("Lines", Lines);
            parameters.Add("Stations", _allStations);
            parameters.Add("AllConfigs", _allConfigs);

            if (LineSelectedItem?.Id > 0)
                parameters.Add("DefaultLine", LineSelectedItem);
            if (StationSelectedItem?.Id > 0)
                parameters.Add("DefaultStation", StationSelectedItem);
            if (TypeSelectedItem?.Id > 0)
                parameters.Add("DefaultType", TypeSelectedItem);

            _dialogService.ShowDialog("AddDataCollectConfigView", parameters, async result =>
            {
                if (result.Result != ButtonResult.OK)
                    return;

                var config = result.Parameters.GetValue<craft_DataCollectConfig>("DataCollectConfig");
                if (config == null)
                    return;

                _allConfigs.Add(config);
                PublishConfigs();
                await FilterConfigsAsync();
            });
        }

        private void OnEdit(DataCollectDisplayItem? item)
        {
            if (item == null)
                return;

            var parameters = new DialogParameters();
            parameters.Add("DataCollectConfig", item.RawData);
            parameters.Add("Types", Types);
            parameters.Add("Lines", Lines);
            parameters.Add("Stations", _allStations);
            parameters.Add("AllConfigs", _allConfigs);

            _dialogService.ShowDialog("EditDataCollectConfigView", parameters, async result =>
            {
                if (result.Result != ButtonResult.OK)
                    return;

                var updated = result.Parameters.GetValue<craft_DataCollectConfig>("DataCollectConfig");
                if (updated == null)
                    return;

                var index = _allConfigs.FindIndex(c => c.Id == updated.Id);
                if (index >= 0)
                    _allConfigs[index] = updated;

                PublishConfigs();
                await FilterConfigsAsync();
            });
        }

        private async void OnRemoveRow(DataCollectDisplayItem? item)
        {
            if (item == null)
                return;

            var name = string.IsNullOrEmpty(item.DataName) ? "该行" : item.DataName;
            if (HandyControl.Controls.MessageBox.Show($"确认移除采集配置「{name}」？", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    if (item.RawData.Id > 0)
                        await _configRepo.DeleteAsync(item.RawData.Id);

                    _allConfigs.RemoveAll(c => c.Id == item.RawData.Id && item.RawData.Id > 0);
                    DisplayConfigs.Remove(item);
                    PublishConfigs();
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show($"移除失败：{ex.Message}", "错误");
                }
            }, "正在移除...");
        }

        private async void OnRefresh()
        {
            if (IsRefreshing)
                return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    IsRefreshing = true;
                    _allConfigs = (await _configRepo.GetAllAsync()).ToList();
                    await FilterConfigsAsync();
                    PublishConfigs();
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show($"刷新失败：{ex.Message}", "错误");
                }
                finally
                {
                    IsRefreshing = false;
                }
            }, "正在刷新...");
        }

        private void PublishConfigs()
        {
            _isPublish = true;
            _cacheService.SetData(_allConfigs);
            _eventAggregator.GetEvent<DataCollectConfigUpdatedEvent>().Publish(_allConfigs);
        }

        #endregion
    }
}
