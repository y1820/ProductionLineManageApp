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
    /// 工位传值配置视图模型：管理跨工位历史数据写入 PLC 的传值规则（列表展示 + 弹窗新增/编辑）。
    /// </summary>
    public class StationDataTransferViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDialogService _dialogService; // 弹窗服务
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataCacheService _cacheService;
        private readonly IRepository<craft_StationDataTransfer> _transferRepo;
        private readonly ILoadingService _loadingService;

        private List<craft_StationDataTransfer> _allConfigs = new();
        private List<craft_LineInfo> _lines = new();
        private List<craft_TypeInfo> _types = new();
        private List<craft_StationInfo> _allStations = new();
        private Dictionary<int, craft_StationInfo> _stationDict = new();
        private Dictionary<int, craft_TypeInfo> _typeDict = new();
        private Dictionary<int, craft_LineInfo> _lineDict = new();

        private int _lineSelectedIndex = -1;
        private craft_LineInfo _lineSelectedItem = new();
        private int _typeSelectedIndex = -1;
        private craft_TypeInfo _typeSelectedItem = new();
        private int _requestStationSelectedIndex = -1;
        private craft_StationInfo _requestStationSelectedItem = new();
        private List<craft_StationInfo> _lineStations = new();
        private bool _isStationEnabled;
        private bool _isRefreshing;
        private bool _isPublish;

        private ObservableCollection<StationDataTransferDisplayItem> _displayConfigs = new(); // 界面展示集合

        #endregion

        #region ===================== 公共属性 =====================

        public ObservableCollection<StationDataTransferDisplayItem> DisplayConfigs
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

        public int RequestStationSelectedIndex
        {
            get => _requestStationSelectedIndex;
            set => SetProperty(ref _requestStationSelectedIndex, value);
        }

        public craft_StationInfo RequestStationSelectedItem
        {
            get => _requestStationSelectedItem;
            set
            {
                if (value == null) return;
                _requestStationSelectedItem = value;
                RequestStationSelectedIndex = LineStations.FindIndex(s => s.Id == value.Id);
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
        public DelegateCommand<StationDataTransferDisplayItem> EditCommand { get; }
        public DelegateCommand<StationDataTransferDisplayItem> RemoveRowCommand { get; }

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入服务并初始化命令与缓存数据 </summary>
        public StationDataTransferViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<craft_StationDataTransfer> transferRepo,
            ILoadingService loadingService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _transferRepo = transferRepo;
            _loadingService = loadingService;

            AddCommand = new DelegateCommand(OnAdd);
            RefreshCommand = new DelegateCommand(OnRefresh);
            EditCommand = new DelegateCommand<StationDataTransferDisplayItem>(OnEdit);
            RemoveRowCommand = new DelegateCommand<StationDataTransferDisplayItem>(OnRemoveRow);

            InitializeData(); // 从缓存加载并订阅事件
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 从缓存初始化产线/型号/工位/传值配置数据 </summary>
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

            if (_cacheService.HasData<List<craft_StationDataTransfer>>())
                _allConfigs = _cacheService.GetData<List<craft_StationDataTransfer>>();

            _ = FilterConfigsAsync();

            _eventAggregator.GetEvent<DataTransferUpdatedEvent>()
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

        private void OnConfigsUpdated(List<craft_StationDataTransfer> configs)
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

            RequestStationSelectedIndex = -1;
            RequestStationSelectedItem = new craft_StationInfo();
        }

        private async Task FilterConfigsAsync()
        {
            await _loadingService.ExecuteAsync(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var filtered = _allConfigs.AsEnumerable();

                    if (LineSelectedItem?.Id > 0)
                        filtered = filtered.Where(c => c.LineId == LineSelectedItem.Id);

                    if (TypeSelectedItem?.Id > 0)
                        filtered = filtered.Where(c => c.ProductTypeId == TypeSelectedItem.Id);

                    if (RequestStationSelectedItem?.Id > 0)
                        filtered = filtered.Where(c => c.RequestStationId == RequestStationSelectedItem.Id);

                    DisplayConfigs = new ObservableCollection<StationDataTransferDisplayItem>(
                        filtered
                            .OrderBy(c => c.RequestStationId)
                            .ThenBy(c => c.SourceStationId)
                            .ThenBy(c => c.RequestDataName)
                            .Select(ToDisplayItem));
                });
            }, "正在加载工位传值配置...");
        }

        private StationDataTransferDisplayItem ToDisplayItem(craft_StationDataTransfer config)
        {
            _stationDict.TryGetValue(config.RequestStationId, out var requestStation);
            _stationDict.TryGetValue(config.SourceStationId, out var sourceStation);
            _typeDict.TryGetValue(config.ProductTypeId, out var type);
            _lineDict.TryGetValue(config.LineId, out var line);

            return new StationDataTransferDisplayItem
            {
                RawData = config,
                LineName = line?.Name ?? config.LineId.ToString(),
                ProductTypeName = type?.Name ?? "未知",
                RequestStationName = requestStation?.DisplayText ?? config.RequestStationId.ToString(),
                SourceStationName = sourceStation?.DisplayText ?? config.SourceStationId.ToString()
            };
        }

        private void OnAdd()
        {
            var parameters = new DialogParameters
            {
                { "Types", Types },
                { "Lines", Lines },
                { "Stations", _allStations },
                { "AllConfigs", _allConfigs }
            };

            if (LineSelectedItem?.Id > 0)
                parameters.Add("DefaultLine", LineSelectedItem);
            if (TypeSelectedItem?.Id > 0)
                parameters.Add("DefaultType", TypeSelectedItem);
            if (RequestStationSelectedItem?.Id > 0)
                parameters.Add("DefaultRequestStation", RequestStationSelectedItem);

            _dialogService.ShowDialog("AddStationDataTransferView", parameters, async result =>
            {
                if (result.Result != ButtonResult.OK)
                    return;

                var config = result.Parameters.GetValue<craft_StationDataTransfer>("StationDataTransfer");
                if (config == null)
                    return;

                _allConfigs.Add(config);
                PublishConfigs();
                await FilterConfigsAsync();
            });
        }

        private void OnEdit(StationDataTransferDisplayItem? item)
        {
            if (item == null)
                return;

            var parameters = new DialogParameters
            {
                { "StationDataTransfer", item.RawData },
                { "Types", Types },
                { "Lines", Lines },
                { "Stations", _allStations },
                { "AllConfigs", _allConfigs }
            };

            _dialogService.ShowDialog("EditStationDataTransferView", parameters, async result =>
            {
                if (result.Result != ButtonResult.OK)
                    return;

                var updated = result.Parameters.GetValue<craft_StationDataTransfer>("StationDataTransfer");
                if (updated == null)
                    return;

                var index = _allConfigs.FindIndex(c => c.Id == updated.Id);
                if (index >= 0)
                    _allConfigs[index] = updated;

                PublishConfigs();
                await FilterConfigsAsync();
            });
        }

        private async void OnRemoveRow(StationDataTransferDisplayItem? item)
        {
            if (item == null)
                return;

            var name = string.IsNullOrEmpty(item.RequestDataName) ? "该行" : item.RequestDataName;
            if (HandyControl.Controls.MessageBox.Show($"确认删除传值配置「{name}」？", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    if (item.RawData.Id > 0)
                        await _transferRepo.DeleteAsync(item.RawData.Id);

                    _allConfigs.RemoveAll(c => c.Id == item.RawData.Id && item.RawData.Id > 0);
                    DisplayConfigs.Remove(item);
                    PublishConfigs();
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show($"删除失败：{ex.Message}", "错误");
                }
            }, "正在删除...");
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
                    _allConfigs = (await _transferRepo.GetAllAsync()).ToList();
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
            _eventAggregator.GetEvent<DataTransferUpdatedEvent>().Publish(_allConfigs);
        }

        #endregion
    }
}
