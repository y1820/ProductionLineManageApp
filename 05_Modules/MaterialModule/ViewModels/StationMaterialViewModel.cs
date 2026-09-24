using MaterialModule.ViewModels.Dialog;
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

namespace MaterialModule.ViewModels
{
    /// <summary>
    /// 工位物料配置视图模型
    /// </summary>
    public class StationMaterialViewModel : BindableBase
    {
        #region ============================== 私有字段 ==============================

        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataCacheService _cacheService;
        private readonly IRepository<material_Station> _repo;
        private readonly ILoadingService _loadingService;

        // 数据源
        private List<material_Station> _allStationMaterials = new List<material_Station>();
        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        private List<craft_LineInfo> _lines = new List<craft_LineInfo>();
        private List<craft_StationInfo> _stations = new List<craft_StationInfo>();
        private List<material_Info> _materials = new List<material_Info>();

        // 字典（加速查找）
        private Dictionary<int, craft_TypeInfo> _typeDict = new Dictionary<int, craft_TypeInfo>();
        private Dictionary<int, craft_LineInfo> _lineDict = new Dictionary<int, craft_LineInfo>();
        private Dictionary<int, craft_StationInfo> _stationDict = new Dictionary<int, craft_StationInfo>();
        private Dictionary<int, material_Info> _materialDict = new Dictionary<int, material_Info>();

        // 筛选
        private int _typeSelectedIndex = -1;
        private craft_TypeInfo _typeSelectedItem = new craft_TypeInfo();
        private int _lineSelectedIndex = -1;
        private craft_LineInfo _lineSelectedItem = new craft_LineInfo();
        private int _stationSelectedIndex = -1;
        private craft_StationInfo _stationSelectedItem = new craft_StationInfo();

        // 显示集合
        private ObservableCollection<StationMaterialDisplayItem> _stationMaterials = new ObservableCollection<StationMaterialDisplayItem>();

        private bool _isRefreshing;
        private bool _isIPublish;

        #endregion

        #region ============================== 公共属性 ==============================

        public ObservableCollection<StationMaterialDisplayItem> StationMaterials
        {
            get => _stationMaterials;
            set => SetProperty(ref _stationMaterials, value);
        }

        public List<craft_TypeInfo> Types
        {
            get => _types;
            set
            {
                _types = value ?? new List<craft_TypeInfo>();
                _typeDict = _types.ToDictionary(t => t.Id);
                RaisePropertyChanged();
            }
        }

        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set
            {
                _lines = value ?? new List<craft_LineInfo>();
                _lineDict = _lines.ToDictionary(l => l.Id);
                RaisePropertyChanged();
            }
        }

        public List<craft_StationInfo> Stations
        {
            get => _stations;
            set
            {
                _stations = value ?? new List<craft_StationInfo>();
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
                _ = FilterDataAsync();
                RaisePropertyChanged();
            }
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
                // 如果工位字典已有数据，才更新工位列表
                if (_stationDict.Any())
                {
                    UpdateStationsByLine(value.Id);
                }
                _ = FilterDataAsync();
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
                StationSelectedIndex = Stations.FindIndex(s => s.Id == value.Id);
                _ = FilterDataAsync();
                RaisePropertyChanged();
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        #endregion

        #region ============================== 命令 ==============================

        public DelegateCommand AddCommand { get; set; }
        public DelegateCommand<StationMaterialDisplayItem> EditCommand { get; set; }
        public DelegateCommand<StationMaterialDisplayItem> DeleteCommand { get; set; }
        public DelegateCommand RefreshCommand { get; set; }

        #endregion

        #region ============================== 构造函数 ==============================

        public StationMaterialViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<material_Station> repo,
            ILoadingService loadingService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _repo = repo;
            _loadingService = loadingService;

            AddCommand = new DelegateCommand(OnAdd);
            EditCommand = new DelegateCommand<StationMaterialDisplayItem>(OnEdit);
            DeleteCommand = new DelegateCommand<StationMaterialDisplayItem>(OnDelete);
            RefreshCommand = new DelegateCommand(OnRefresh);

            InitializeData();
        }

        #endregion

        #region ============================== 初始化 ==============================

        private void InitializeData()
        {
            // 加载缓存数据
            //型号缓存
            if (_cacheService.HasData<List<craft_TypeInfo>>())
            {
                Types = _cacheService.GetData<List<craft_TypeInfo>>();
            }
            //产线缓存
            if (_cacheService.HasData<List<craft_LineInfo>>())
            {
                Lines = _cacheService.GetData<List<craft_LineInfo>>();
            }
            //工位缓存
            if (_cacheService.HasData<List<craft_StationInfo>>())
            {
                var allStations = _cacheService.GetData<List<craft_StationInfo>>();
                _stationDict = allStations.ToDictionary(s => s.Id);
            }
            //物料缓存
            if (_cacheService.HasData<List<material_Info>>())
            {
                _materials = _cacheService.GetData<List<material_Info>>();
                _materialDict = _materials.ToDictionary(m => m.Id);
            }
            //工位物料缓存
            if (_cacheService.HasData<List<material_Station>>())
            {
                _allStationMaterials = _cacheService.GetData<List<material_Station>>();
                _ = DisplayDataAsync();
            }

            // 订阅型号更新事件（当型号变更时刷新下拉框）
            _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>()
                .Subscribe(OnOrderTypeUpdated, ThreadOption.UIThread);
            //订阅产线
            _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>()
                .Subscribe(OnOrderLineUpdated, ThreadOption.UIThread);
            //订阅工位
            _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>()
                .Subscribe(OnOrderStationUpdated, ThreadOption.UIThread);
            // 订阅物料更新事件
            _eventAggregator.GetEvent<MaterialInfoUpdatedEvent>()
                .Subscribe(OnMaterialsUpdated, ThreadOption.UIThread);
            // 订阅事件，工位物料
            _eventAggregator.GetEvent<StationMaterialUpdatedEvent>()
                .Subscribe(OnStationMaterialUpdated, ThreadOption.UIThread);
        }

        private void UpdateStationsByLine(int lineId)
        {
            var filtered = _stationDict.Values.Where(s => s.LineId == lineId).ToList();
            Stations = filtered;
            StationSelectedIndex = -1;
            StationSelectedItem = new craft_StationInfo();
        }

        #endregion

        #region ============================== 数据筛选与显示 ==============================

        private async Task FilterDataAsync()
        {
            await _loadingService.ExecuteAsync(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 未选择型号时清空列表 || 未选择产线时 
                    if (TypeSelectedItem?.Id == 0 || LineSelectedItem.Id == 0)
                    {
                        StationMaterials.Clear();
                        return;
                    }

                    var filtered = _allStationMaterials
                        .Where(s => s.TypeId == TypeSelectedItem?.Id);

                    if (LineSelectedItem?.Id > 0)
                    {
                        filtered = filtered.Where(s => s.LineId == LineSelectedItem.Id);
                    }

                    if (StationSelectedItem?.Id > 0)
                    {
                        filtered = filtered.Where(s => s.StationId == StationSelectedItem.Id);
                    }

                    var displayItems = filtered.Select(s => new StationMaterialDisplayItem
                    {
                        RawData = s,
                        TypeName = _typeDict.GetValueOrDefault(s.TypeId)?.Name ?? "未知",
                        LineName = _lineDict.GetValueOrDefault(s.LineId)?.Name ?? "未知",
                        StationName = _stationDict.GetValueOrDefault(s.StationId)?.DisplayText ?? "未知",
                        MaterialCode = _materialDict.GetValueOrDefault(s.MaterialId)?.Code ?? "未知",
                        MaterialName = _materialDict.GetValueOrDefault(s.MaterialId)?.Name ?? "未知",
                        CheckStationName = _stationDict.GetValueOrDefault(s.CheckMaterialStationId)?.DisplayText ?? "无",
                        ParentMaterialName = _materialDict.GetValueOrDefault(s.ParentMaterialId)?.Name ?? "无"
                    }).OrderBy(s => s.Sequence).ToList();

                    StationMaterials.Clear();
                    foreach (var item in displayItems)
                    {
                        StationMaterials.Add(item);
                    }
                });
            }, "正在加载工位物料数据...");
        }

        private async Task DisplayDataAsync()
        {
            await FilterDataAsync();
        }

        #endregion

        #region ============================== 事件聚合器订阅接收方法 ==============================

        private async void OnStationMaterialUpdated(List<material_Station> data)
        {
            try
            {
                if (_isIPublish)
                {
                    _isIPublish = false;
                    return;
                }
                _allStationMaterials = data;
                await DisplayDataAsync();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"在接收工位物料信息后更新数据失败：{ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 型号订阅回调方法
        /// </summary>
        /// <param name="list"></param>
        private async void OnOrderTypeUpdated(List<craft_TypeInfo> list)
        {
            try
            {
                Types = list;
                _typeDict = list.ToDictionary(t => t.Id);
                TypeSelectedIndex = -1;
                TypeSelectedItem = new craft_TypeInfo();
                await DisplayDataAsync();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"在接收型号信息后更新数据失败：{ex.Message}", "错误");
            }
        }
        /// <summary>
        /// 产线订阅回调方法
        /// </summary>
        /// <param name="list"></param>
        private async void OnOrderLineUpdated(List<craft_LineInfo> list)
        {
            try
            {
                Lines = list;
                _lineDict = list.ToDictionary(l => l.Id);
                LineSelectedIndex = -1;
                LineSelectedItem = new craft_LineInfo();
                StationSelectedIndex = -1;
                await DisplayDataAsync();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"在接收产线信息后更新数据失败：{ex.Message}", "错误");
            }
        }
        /// <summary>
        /// 工位订阅回调方法
        /// </summary>
        /// <param name="list"></param>
        private async void OnOrderStationUpdated(List<craft_StationInfo> list)
        {
            try
            {
                //填充字典
                _stationDict = list.ToDictionary(s => s.Id);
                //筛选产线下的工位
                if (LineSelectedItem.Id > 0)
                {
                    //先赋值给界面工位
                    Stations = list;
                    //再筛选产线下的工位
                    UpdateStationsByLine(LineSelectedItem.Id);
                }
                else
                {
                    Stations = new List<craft_StationInfo>();
                }
                StationSelectedIndex = -1;
                //更新集合列表信息
                await DisplayDataAsync();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"在接收工位信息后更新数据失败：{ex.Message}", "错误");
            }
        }

        private void OnMaterialsUpdated(List<material_Info> list)
        {
            try
            {
                _materials = list;
                _materialDict = _materials.ToDictionary(m => m.Id);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"在接收物料信息后更新数据失败：{ex.Message}", "错误");
            }
        }
        #endregion ----------------------------------

        #region ============================== 命令实现 ==============================

        private async void OnRefresh()
        {
            if (IsRefreshing) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    IsRefreshing = true;
                    var freshData = await _repo.GetAllAsync();
                    _allStationMaterials = freshData.ToList();

                    await DisplayDataAsync();

                    _isIPublish = true;
                    _eventAggregator.GetEvent<StationMaterialUpdatedEvent>().Publish(_allStationMaterials);
                    _cacheService.SetData(_allStationMaterials);
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

        private void OnAdd()
        {
            var parameters = new DialogParameters();
            parameters.Add("Types", _types);
            parameters.Add("Lines", _lines);
            parameters.Add("Stations", _stations);
            parameters.Add("Materials", _materials);
            parameters.Add("AllStationMaterials", _allStationMaterials);

            _dialogService.ShowDialog("AddStationMaterialView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newItem = result.Parameters.GetValue<material_Station>("StationMaterial");
                    if (newItem != null)
                    {
                        _allStationMaterials.Add(newItem);
                        await DisplayDataAsync();

                        _isIPublish = true;
                        _eventAggregator.GetEvent<StationMaterialUpdatedEvent>().Publish(_allStationMaterials);
                        _cacheService.SetData(_allStationMaterials);
                    }
                }
            });
        }

        private void OnEdit(StationMaterialDisplayItem item)
        {
            if (item == null) return;

            var parameters = new DialogParameters();
            parameters.Add("StationMaterial", item.RawData);
            parameters.Add("Types", _types);
            parameters.Add("Lines", _lines);
            parameters.Add("Stations", _stations);
            parameters.Add("Materials", _materials);
            parameters.Add("AllStationMaterials", _allStationMaterials);

            _dialogService.ShowDialog("EditStationMaterialView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var updatedItem = result.Parameters.GetValue<material_Station>("StationMaterial");
                    if (updatedItem != null)
                    {
                        var index = _allStationMaterials.FindIndex(s => s.Id == updatedItem.Id);
                        if (index >= 0)
                        {
                            _allStationMaterials[index] = updatedItem;
                        }
                        await DisplayDataAsync();

                        _isIPublish = true;
                        _eventAggregator.GetEvent<StationMaterialUpdatedEvent>().Publish(_allStationMaterials);
                        _cacheService.SetData(_allStationMaterials);
                    }
                }
            });
        }

        private async void OnDelete(StationMaterialDisplayItem item)
        {
            if (item == null) return;

            var result = HandyControl.Controls.MessageBox.Show(
                $"确认删除物料 \"{item.MaterialName}\" 的工位配置？",
                "提示",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    await _repo.DeleteAsync(item.Id);

                    _allStationMaterials.Remove(item.RawData);
                    await DisplayDataAsync();

                    _isIPublish = true;
                    _eventAggregator.GetEvent<StationMaterialUpdatedEvent>().Publish(_allStationMaterials);
                    _cacheService.SetData(_allStationMaterials);

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
    /// 工位物料显示模型
    /// </summary>
    public class StationMaterialDisplayItem : BindableBase
    {
        public material_Station RawData { get; set; } = new material_Station();

        public int Id => RawData.Id;
        public string TypeName { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public string MaterialCode { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public int Sequence => RawData.Sequence;
        public string CheckStationName { get; set; } = string.Empty;
        public string ParentMaterialName { get; set; } = string.Empty;
        public string Remarks => RawData.Remarks;
    }
}
