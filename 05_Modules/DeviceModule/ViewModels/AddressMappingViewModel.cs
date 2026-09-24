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

namespace DeviceModule.ViewModels
{
    /// <summary>
    /// 设备交互地址映射视图模型（列表展示 + 弹窗新增/编辑）
    /// </summary>
    public class AddressMappingViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDialogService _dialogService; // 弹窗服务（新增/编辑映射）
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataCacheService _cacheService;
        private readonly IRepository<device_AddressMapping> _mappingRepo;
        private readonly ILoadingService _loadingService;

        private List<device_AddressMapping> _allMappings = new();
        private List<craft_LineInfo> _lines = new();
        private List<craft_StationInfo> _allStations = new();
        private Dictionary<int, craft_StationInfo> _stationDict = new();
        private Dictionary<int, craft_LineInfo> _lineDict = new();

        private int _lineSelectedIndex = -1;
        private craft_LineInfo _lineSelectedItem = new();
        private int _stationSelectedIndex = -1;
        private craft_StationInfo _stationSelectedItem = new();
        private List<craft_StationInfo> _lineStations = new();
        private bool _isStationEnabled;
        private bool _isRefreshing;
        private bool _isIPublish;

        private ObservableCollection<MappingDisplayItem> _addressMapping = new(); // 界面展示集合

        #endregion

        #region ===================== 公共属性 =====================

        public ObservableCollection<MappingDisplayItem> AddressMapping
        {
            get => _addressMapping;
            set => SetProperty(ref _addressMapping, value);
        }

        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set => SetProperty(ref _lines, value);
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
                _ = FilterMappingsAsync();
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
                _ = FilterMappingsAsync();
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

        #endregion

        #region ===================== 命令 =====================

        /// <summary> 打开新增地址映射弹窗 </summary>
        public DelegateCommand AddCommand { get; }

        /// <summary> 刷新映射列表 </summary>
        public DelegateCommand RefreshCommand { get; }

        /// <summary> 编辑选中映射 </summary>
        public DelegateCommand<MappingDisplayItem> EditCommand { get; }

        /// <summary> 删除选中映射 </summary>
        public DelegateCommand<MappingDisplayItem> RemoveRowCommand { get; }

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入服务并订阅配置变更事件 </summary>
        public AddressMappingViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<device_AddressMapping> mappingRepo,
            ILoadingService loadingService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _mappingRepo = mappingRepo;
            _loadingService = loadingService;

            AddCommand = new DelegateCommand(OnAdd);
            RefreshCommand = new DelegateCommand(OnRefresh);
            EditCommand = new DelegateCommand<MappingDisplayItem>(OnEdit);
            RemoveRowCommand = new DelegateCommand<MappingDisplayItem>(OnRemoveRow);

            InitializeData(); // 从缓存加载并订阅事件
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 从缓存初始化产线/工位/映射数据 </summary>
        private void InitializeData()
        {
            if (_cacheService.HasData<List<craft_LineInfo>>())
            {
                Lines = _cacheService.GetData<List<craft_LineInfo>>();
                _lineDict = Lines.ToDictionary(l => l.Id);
            }

            if (_cacheService.HasData<List<craft_StationInfo>>())
            {
                _allStations = _cacheService.GetData<List<craft_StationInfo>>();
                _stationDict = _allStations.ToDictionary(s => s.Id);
            }

            if (_cacheService.HasData<List<device_AddressMapping>>())
                _allMappings = _cacheService.GetData<List<device_AddressMapping>>();

            _ = FilterMappingsAsync();

            _eventAggregator.GetEvent<AddressMappingUpdatedEvent>()
                .Subscribe(OnMappingsUpdated, ThreadOption.UIThread);
            _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>()
                .Subscribe(lines =>
                {
                    Lines = lines;
                    _lineDict = lines.ToDictionary(l => l.Id);
                }, ThreadOption.UIThread);
            _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>()
                .Subscribe(stations =>
                {
                    _allStations = stations;
                    _stationDict = stations.ToDictionary(s => s.Id);
                    FilterStationsByLine();
                    _ = FilterMappingsAsync();
                }, ThreadOption.UIThread);
        }

        private void OnMappingsUpdated(List<device_AddressMapping> mappings)
        {
            if (_isIPublish)
            {
                _isIPublish = false;
                return;
            }

            _allMappings = mappings;
            _ = FilterMappingsAsync();
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

        private async Task FilterMappingsAsync()
        {
            await _loadingService.ExecuteAsync(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (LineSelectedItem?.Id == 0)
                    {
                        AddressMapping.Clear();
                        return;
                    }

                    var filtered = _allMappings.AsEnumerable();

                    if (LineSelectedItem?.Id > 0)
                    {
                        filtered = filtered.Where(m =>
                            _stationDict.TryGetValue(m.StationId, out var station) &&
                            station.LineId == LineSelectedItem.Id);
                    }

                    if (StationSelectedItem?.Id > 0)
                        filtered = filtered.Where(m => m.StationId == StationSelectedItem.Id);

                    AddressMapping = new ObservableCollection<MappingDisplayItem>(
                        filtered
                            .OrderBy(m => m.StationId)
                            .ThenBy(m => m.DataName)
                            .Select(m =>
                            {
                                var station = _stationDict.GetValueOrDefault(m.StationId);
                                var lineName = station != null && _lineDict.TryGetValue(station.LineId, out var line)
                                    ? line.Name
                                    : "未知";

                                return new MappingDisplayItem
                                {
                                    RawData = m,
                                    LineName = lineName,
                                    StationName = station?.DisplayText ?? "未知"
                                };
                            }));
                });
            }, "正在加载地址映射...");
        }

        private void OnAdd()
        {
            var parameters = new DialogParameters();
            parameters.Add("Lines", Lines);
            parameters.Add("Stations", _allStations);
            parameters.Add("AllMappings", _allMappings);

            if (LineSelectedItem?.Id > 0)
                parameters.Add("DefaultLine", LineSelectedItem);
            if (StationSelectedItem?.Id > 0)
                parameters.Add("DefaultStation", StationSelectedItem);

            _dialogService.ShowDialog("AddAddressMappingView", parameters, async result =>
            {
                if (result.Result != ButtonResult.OK)
                    return;

                var mapping = result.Parameters.GetValue<device_AddressMapping>("AddressMapping");
                if (mapping == null)
                    return;

                _allMappings.Add(mapping);
                PublishMappings();
                await FilterMappingsAsync();
            });
        }

        private void OnEdit(MappingDisplayItem? item)
        {
            if (item == null)
                return;

            var parameters = new DialogParameters();
            parameters.Add("AddressMapping", item.RawData);
            parameters.Add("Lines", Lines);
            parameters.Add("Stations", _allStations);
            parameters.Add("AllMappings", _allMappings);

            _dialogService.ShowDialog("EditAddressMappingView", parameters, async result =>
            {
                if (result.Result != ButtonResult.OK)
                    return;

                var updated = result.Parameters.GetValue<device_AddressMapping>("AddressMapping");
                if (updated == null)
                    return;

                var index = _allMappings.FindIndex(m => m.Id == updated.Id);
                if (index >= 0)
                    _allMappings[index] = updated;

                PublishMappings();
                await FilterMappingsAsync();
            });
        }

        private async void OnRemoveRow(MappingDisplayItem? item)
        {
            if (item == null)
                return;

            var name = string.IsNullOrEmpty(item.DataName) ? "该行" : item.DataName;
            if (HandyControl.Controls.MessageBox.Show($"确认移除地址映射「{name}」？", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    if (item.RawData.Id > 0)
                        await _mappingRepo.DeleteAsync(item.RawData.Id);

                    _allMappings.RemoveAll(m => m.Id == item.RawData.Id && item.RawData.Id > 0);
                    AddressMapping.Remove(item);
                    PublishMappings();
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
                    _allMappings = (await _mappingRepo.GetAllAsync()).ToList();
                    await FilterMappingsAsync();
                    PublishMappings();
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

        /// <summary> 将最新映射列表写回缓存并发布更新事件 </summary>
        private void PublishMappings()
        {
            _isIPublish = true; // 标记为本 ViewModel 发布，避免事件回环
            _cacheService.SetData(_allMappings);
            _eventAggregator.GetEvent<AddressMappingUpdatedEvent>().Publish(_allMappings);
        }

        #endregion
    }
}
