using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using ProductionLineManage.Core.Models.DataBase;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>
    /// 工艺 - 工位视图模型（列表展示 + 弹窗新增/编辑）
    /// </summary>
    public class StationConfigViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDialogService _dialogService; // 弹窗服务（新增/编辑工位）
        private readonly IDataCacheService _cacheService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRepository<craft_StationInfo> _repo;

        private bool _isIPublish;
        private bool _isRefreshing;
        private List<craft_StationInfo> _allStationInfos = new();
        private Dictionary<int, craft_LineInfo> _lineDict = new();

        private ObservableCollection<StationDisplayItem> _lineStations = new();
        private ObservableCollection<craft_LineInfo> _lines = new();
        private craft_LineInfo _selectedLine = new();
        private int _lineIndex = -1;

        #endregion

        #region ===================== 公共属性 =====================

        public ObservableCollection<StationDisplayItem> LineStations
        {
            get => _lineStations;
            set => SetProperty(ref _lineStations, value);
        }

        public ObservableCollection<craft_LineInfo> Lines
        {
            get => _lines;
            set => SetProperty(ref _lines, value);
        }

        public craft_LineInfo SelectedLine
        {
            get => _selectedLine;
            set
            {
                if (value == null) return;
                SetProperty(ref _selectedLine, value);
                LineIndex = Lines.ToList().FindIndex(l => l.Id == value.Id);
                _ = LoadDataAsync();
            }
        }

        public int LineIndex
        {
            get => _lineIndex;
            set => SetProperty(ref _lineIndex, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public required DelegateCommand<StationDisplayItem> DeleteCommand { get; set; }
        public required DelegateCommand<StationDisplayItem> EditCommand { get; set; }
        public required DelegateCommand AddCommand { get; set; }
        public required DelegateCommand RefreshCommand { get; set; }
        public required DelegateCommand CopyCommand { get; set; }

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入弹窗、事件、缓存与仓储服务，初始化命令 </summary>
        public StationConfigViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<craft_StationInfo> repo)
        {
            _dialogService = dialogService;
            _cacheService = cacheService;
            _eventAggregator = eventAggregator;
            _repo = repo;

            EditCommand = new DelegateCommand<StationDisplayItem>(Edit);
            DeleteCommand = new DelegateCommand<StationDisplayItem>(Delete);
            AddCommand = new DelegateCommand(Add);
            CopyCommand = new DelegateCommand(Copy);
            RefreshCommand = new DelegateCommand(Refresh);

            InitializeData(); // 从缓存加载产线/工位并订阅事件
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 从缓存初始化数据并订阅产线/工位变更 </summary>
        private void InitializeData()
        {
            if (_cacheService.HasData<List<craft_LineInfo>>())
                OnOrderUpdated(_cacheService.GetData<List<craft_LineInfo>>());

            if (_cacheService.HasData<List<craft_StationInfo>>())
                _allStationInfos = _cacheService.GetData<List<craft_StationInfo>>();

            _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>()
                .Subscribe(OnOrderUpdated, ThreadOption.UIThread);
            _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>()
                .Subscribe(OnOrderUpdated2, ThreadOption.UIThread);
        }

        private void Add()
        {
            var parameters = new DialogParameters();
            parameters.Add("Lines", Lines.ToList());

            if (SelectedLine?.Id > 0)
                parameters.Add("DefaultLine", SelectedLine);

            _dialogService.ShowDialog("NewAddWorkStationView", parameters, result =>
            {
                if (result.Result != ButtonResult.OK)
                    return;

                var stationInfo = result.Parameters.GetValue<craft_StationInfo>("StationInfo");
                if (stationInfo == null)
                    return;

                _allStationInfos.Add(stationInfo);
                _ = LoadDataAsync();
                PublishStations();
            });
        }

        private async void Delete(StationDisplayItem? item)
        {
            if (item == null)
                return;

            var info = item.RawData;
            if (HandyControl.Controls.MessageBox.Show(
                    $"确认删除「{info.DisplayText}」该工位?",
                    "提示", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            await _repo.DeleteAsync(info.Id);
            _allStationInfos.RemoveAll(s => s.Id == info.Id);
            _ = LoadDataAsync();
            PublishStations();
            HandyControl.Controls.MessageBox.Show("删除成功");
        }

        private void Edit(StationDisplayItem? item)
        {
            if (item == null)
                return;

            var parameters = new DialogParameters();
            parameters.Add("StationInfo", item.RawData);
            parameters.Add("Lines", Lines.ToList());

            _dialogService.ShowDialog("EditWorkStationView", parameters, result =>
            {
                if (result.Result != ButtonResult.OK)
                    return;

                var stationInfo = result.Parameters.GetValue<craft_StationInfo>("StationInfo");
                if (stationInfo == null)
                    return;

                var editIndex = _allStationInfos.FindIndex(s => s.Id == stationInfo.Id);
                if (editIndex >= 0)
                    _allStationInfos[editIndex] = stationInfo;

                PublishStations();
                _ = LoadDataAsync();
            });
        }

        private async void Refresh()
        {
            LineIndex = -1;
            LineStations.Clear();

            if (IsRefreshing)
                return;

            IsRefreshing = true;
            try
            {
                _allStationInfos = (await _repo.GetAllAsync()).ToList();
                PublishStations();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"加载数据失败：{ex.Message}", "错误");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void Copy()
        {
        }

        private async Task LoadDataAsync()
        {
            var filteredData = await Task.Run(() =>
            {
                if (SelectedLine?.Id <= 0)
                    return new List<craft_StationInfo>();

                return _allStationInfos
                    .Where(s => SelectedLine != null && s.LineId == SelectedLine.Id)
                    .OrderBy(s => s.Code)
                    .ToList();
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LineStations.Clear();
                foreach (var item in filteredData)
                {
                    var lineName = _lineDict.TryGetValue(item.LineId, out var line)
                        ? line.Name
                        : "未知";

                    LineStations.Add(new StationDisplayItem
                    {
                        RawData = item,
                        LineName = lineName
                    });
                }
            }, DispatcherPriority.Background);
        }

        private void OnOrderUpdated(List<craft_LineInfo> lines)
        {
            Lines.Clear();
            if (lines == null || lines.Count == 0)
                return;

            foreach (var line in lines)
                Lines.Add(line);

            _lineDict = lines.ToDictionary(l => l.Id);
            LineIndex = -1;
            SelectedLine = new craft_LineInfo();
        }

        private void OnOrderUpdated2(List<craft_StationInfo> stations)
        {
            if (_isIPublish)
            {
                _isIPublish = false;
                return;
            }

            _allStationInfos = stations;
        }

        private void PublishStations()
        {
            _isIPublish = true;
            _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>().Publish(_allStationInfos);
            _cacheService.SetData(_allStationInfos);
        }

        #endregion
    }
}
