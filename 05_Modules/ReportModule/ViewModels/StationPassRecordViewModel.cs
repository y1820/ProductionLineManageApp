using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using ReportModule.Query;
using System.Collections.ObjectModel;
using System.Windows;

namespace ReportModule.ViewModels
{
    /// <summary>
    /// 报表 - 过站明细查询（report_StationPassRecord）
    /// </summary>
    public class StationPassRecordViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDataCacheService _cacheService; // 全局配置缓存
        private readonly IRepository<report_StationPassRecord> _passRepo;
        private readonly ILoadingService _loadingService;

        private List<craft_LineInfo> _lines = new();
        private List<craft_TypeInfo> _types = new();
        private List<craft_StationInfo> _allStations = new();
        private Dictionary<int, craft_StationInfo> _stationDict = new();
        private Dictionary<int, craft_TypeInfo> _typeDict = new();
        private Dictionary<int, craft_LineInfo> _lineDict = new();

        private string _flowCodeFilter = string.Empty;
        private craft_LineInfo _lineSelectedItem = new();
        private craft_StationInfo _stationSelectedItem = new();
        private craft_TypeInfo _typeSelectedItem = new();
        private List<craft_StationInfo> _lineStations = new();
        private bool _isStationEnabled;
        private DateTime _queryStartTime = DateTime.Today.AddDays(-7);
        private DateTime _queryEndTime = DateTime.Today.AddDays(1).AddSeconds(-1);
        private int _pageSize = 100;
        private int _selectedPageSize = 100;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _totalCount;
        private bool _isSearching;

        private readonly ObservableCollection<StationPassRecordDisplayItem> _records = new(); // 查询结果集合

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入缓存、仓储与加载服务，初始化分页查询命令 </summary>
        public StationPassRecordViewModel(
            IDataCacheService cacheService,
            IRepository<report_StationPassRecord> passRepo,
            ILoadingService loadingService)
        {
            _cacheService = cacheService;
            _passRepo = passRepo;
            _loadingService = loadingService;

            SearchCommand = new DelegateCommand(async () => await SearchAsync(resetPage: true), CanSearch);
            RefreshCommand = new DelegateCommand(async () => await SearchAsync(resetPage: false), CanSearch);
            FirstPageCommand = new DelegateCommand(GoFirstPage, () => CurrentPage > 1);
            PreviousPageCommand = new DelegateCommand(GoPreviousPage, () => CurrentPage > 1);
            NextPageCommand = new DelegateCommand(GoNextPage, () => CurrentPage < TotalPages);
            LastPageCommand = new DelegateCommand(GoLastPage, () => CurrentPage < TotalPages);

            InitializeFilters(); // 从缓存加载筛选下拉数据
        }

        #endregion

        #region ===================== 公共属性 =====================

        public ObservableCollection<StationPassRecordDisplayItem> Records => _records;
        public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 50, 100, 200, 500 };

        public List<craft_LineInfo> Lines { get => _lines; set => SetProperty(ref _lines, value); }
        public List<craft_TypeInfo> Types
        {
            get => _types;
            set
            {
                SetProperty(ref _types, value);
                _typeDict = value?.ToDictionary(t => t.Id) ?? new Dictionary<int, craft_TypeInfo>();
            }
        }
        public List<craft_StationInfo> LineStations { get => _lineStations; set => SetProperty(ref _lineStations, value); }
        public string FlowCodeFilter { get => _flowCodeFilter; set => SetProperty(ref _flowCodeFilter, value); }
        public craft_LineInfo LineSelectedItem
        {
            get => _lineSelectedItem;
            set
            {
                if (value == null) return;
                SetProperty(ref _lineSelectedItem, value);
                FilterStationsByLine();
            }
        }
        public craft_StationInfo StationSelectedItem
        {
            get => _stationSelectedItem;
            set { if (value != null) SetProperty(ref _stationSelectedItem, value); }
        }
        public craft_TypeInfo TypeSelectedItem
        {
            get => _typeSelectedItem;
            set { if (value != null) SetProperty(ref _typeSelectedItem, value); }
        }
        public bool IsStationEnabled { get => _isStationEnabled; set => SetProperty(ref _isStationEnabled, value); }
        public DateTime QueryStartTime { get => _queryStartTime; set => SetProperty(ref _queryStartTime, value); }
        public DateTime QueryEndTime { get => _queryEndTime; set => SetProperty(ref _queryEndTime, value); }
        public int SelectedPageSize
        {
            get => _selectedPageSize;
            set
            {
                if (!PageSizeOptions.Contains(value) || _selectedPageSize == value) return;
                _selectedPageSize = value;
                _pageSize = value;
                RaisePropertyChanged();
            }
        }
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    RaisePropertyChanged(nameof(PageInfo));
                    RefreshPageCommands();
                }
            }
        }
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                if (SetProperty(ref _totalPages, value))
                {
                    RaisePropertyChanged(nameof(PageInfo));
                    RefreshPageCommands();
                }
            }
        }
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (SetProperty(ref _totalCount, value))
                    RaisePropertyChanged(nameof(TotalCountText));
            }
        }

        public string TotalCountText => $"共 {TotalCount:N0} 条记录";
        public string PageInfo => $"{CurrentPage} / {TotalPages}";

        public DelegateCommand SearchCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand FirstPageCommand { get; }
        public DelegateCommand PreviousPageCommand { get; }
        public DelegateCommand NextPageCommand { get; }
        public DelegateCommand LastPageCommand { get; }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 从缓存初始化产线/型号/工位筛选数据 </summary>
        private void InitializeFilters()
        {
            if (_cacheService.HasData<List<craft_LineInfo>>())
            {
                Lines = _cacheService.GetData<List<craft_LineInfo>>();
                _lineDict = Lines.ToDictionary(l => l.Id);
            }
            if (_cacheService.HasData<List<craft_TypeInfo>>())
                Types = _cacheService.GetData<List<craft_TypeInfo>>();
            if (_cacheService.HasData<List<craft_StationInfo>>())
            {
                _allStations = _cacheService.GetData<List<craft_StationInfo>>();
                _stationDict = _allStations.ToDictionary(s => s.Id);
            }
        }

        private void FilterStationsByLine()
        {
            if (LineSelectedItem?.Id > 0)
            {
                LineStations = _allStations.Where(s => s.LineId == LineSelectedItem.Id).OrderBy(s => s.Code).ToList();
                IsStationEnabled = LineStations.Any();
            }
            else
            {
                LineStations = new List<craft_StationInfo>();
                IsStationEnabled = false;
            }
            StationSelectedItem = new craft_StationInfo();
        }

        private StationPassRecordQueryContext BuildContext() => new()
        {
            FlowCode = FlowCodeFilter,
            LineId = LineSelectedItem?.Id ?? 0,
            StationId = StationSelectedItem?.Id ?? 0,
            ProductTypeId = TypeSelectedItem?.Id ?? 0,
            QueryStartTime = QueryStartTime,
            QueryEndTime = QueryEndTime
        };

        private bool CanSearch() => !_isSearching;

        private async Task SearchAsync(bool resetPage)
        {
            if (!CanSearch() || QueryStartTime > QueryEndTime)
            {
                if (QueryStartTime > QueryEndTime)
                    HandyControl.Controls.MessageBox.Show("起始时间不能晚于结束时间。", "提示");
                return;
            }

            if (resetPage) _currentPage = 1;
            _isSearching = true;
            SearchCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();

            try
            {
                await _loadingService.ExecuteAsync(async () =>
                {
                    try
                    {
                        var context = BuildContext();
                        var countSql = StationPassRecordQueryHelper.ApplyWhere(
                            StationPassRecordQueryHelper.CountSql, context);
                        var total = await _passRepo.ExecuteScalarAsync<int>(countSql, context.BuildParameters());

                        if (total <= 0)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                _records.Clear();
                                TotalCount = 0;
                                TotalPages = 1;
                                CurrentPage = 1;
                            });
                            return;
                        }

                        TotalPages = Math.Max(1, (int)Math.Ceiling((double)total / _pageSize));
                        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

                        var pageSql = StationPassRecordQueryHelper.ApplyWhere(
                            StationPassRecordQueryHelper.PageSql, context);
                        var rows = await _passRepo.QueryAsync<report_StationPassRecord>(
                            pageSql, context.ToPageParameters((CurrentPage - 1) * _pageSize, _pageSize));

                        var items = (rows ?? Enumerable.Empty<report_StationPassRecord>())
                            .Select(ToDisplayItem).ToList();

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _records.Clear();
                            foreach (var item in items) _records.Add(item);
                            TotalCount = total;
                        });
                    }
                    catch (Exception ex)
                    {
                        HandyControl.Controls.MessageBox.Show($"查询失败：{ex.Message}", "错误");
                    }
                }, "正在查询过站明细...");
            }
            finally
            {
                _isSearching = false;
                SearchCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }

        private StationPassRecordDisplayItem ToDisplayItem(report_StationPassRecord row)
        {
            _stationDict.TryGetValue(row.StationId, out var station);
            _typeDict.TryGetValue(row.ProductTypeId, out var type);
            _lineDict.TryGetValue(row.LineId, out var line);

            return new StationPassRecordDisplayItem
            {
                Id = row.Id,
                FlowCode = row.FlowCode,
                TrayCode = row.TrayCode,
                StationName = station?.DisplayText ?? row.StationId.ToString(),
                ProductTypeName = type?.Name ?? row.ProductTypeId.ToString(),
                LineName = line?.Name ?? row.LineId.ToString(),
                StartTime = row.StartTime,
                EndTime = row.EndTime,
                PassStateText = row.EndTime == null ? "进行中" : "已结束"
            };
        }

        private void GoFirstPage() { CurrentPage = 1; _ = SearchAsync(false); }
        private void GoPreviousPage() { if (CurrentPage > 1) CurrentPage--; _ = SearchAsync(false); }
        private void GoNextPage() { if (CurrentPage < TotalPages) CurrentPage++; _ = SearchAsync(false); }
        private void GoLastPage() { CurrentPage = TotalPages; _ = SearchAsync(false); }

        private void RefreshPageCommands()
        {
            FirstPageCommand.RaiseCanExecuteChanged();
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
            LastPageCommand.RaiseCanExecuteChanged();
        }

        #endregion
    }
}
