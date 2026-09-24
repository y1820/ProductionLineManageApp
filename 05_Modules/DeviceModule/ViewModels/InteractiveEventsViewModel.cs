using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Infrastructure.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeviceModule.ViewModels
{
    /// <summary>
    /// 设备 -交互事件日志
    /// </summary>
    public class InteractiveEventsViewModel : BindableBase, IDisposable
    {
        #region ===================== 私有字段 =====================
        /// <summary>弹窗服务</summary>
        private readonly IDialogService _dialogService;
        /// <summary>事件聚合器</summary>
        private readonly IEventAggregator _eventAggregator;
        /// <summary>数据缓存服务</summary>
        private readonly IDataCacheService _cacheService;
        /// <summary>设备日志服务</summary>
        private readonly IDeviceLogService _logService;
        /// <summary>加载动画服务</summary>
        private readonly ILoadingService _loadingService;

        // 数据源
        /// <summary>所有产线</summary>
        private List<craft_LineInfo> _lines = new List<craft_LineInfo>();
        /// <summary>所有工位</summary>
        private List<craft_StationInfo> _allStations = new List<craft_StationInfo>();
        /// <summary>产线下的工位</summary>
        private List<craft_StationInfo> _stations = new List<craft_StationInfo>();
        /// <summary>所有日志信息</summary>
        private List<DeviceLogEntry> _allLogs = new List<DeviceLogEntry>();

        // 筛选
        /// <summary>产线下拉框选择索引值</summary>
        private int _lineSelectedIndex = -1;
        /// <summary>产线下拉框选择的对象</summary>
        private craft_LineInfo _lineSelectedItem = new craft_LineInfo();
        /// <summary>产线下的工位下拉框选择索引值</summary>
        private int _stationSelectedIndex = -1;
        /// <summary>产线下的工位下拉框选择的对象</summary>
        private craft_StationInfo _stationSelectedItem = new craft_StationInfo();
        /// <summary>选择的时间</summary>
        private DateTime _selectedDate = DateTime.Today;

        private List<string> _levelFilters = new List<string> { "全部", "Info", "Warning", "Error" };
        private int _levelFilterIndex = 0;
        private string _levelFilterItem = "全部";

        // 分页
        /// <summary>每页最大日志条数</summary>
        private int _pageSize = 50;
        /// <summary>当前页</summary>
        private int _currentPage = 1;
        /// <summary>总页数</summary>
        private int _totalPages = 1;

        // 实时刷新
        private bool _isAutoRefreshEnabled = true;
        private System.Timers.Timer? _autoRefreshTimer;

        /// <summary> 显示集合 </summary>
        private ObservableCollection<LogEntryDisplayItem> _logEntries = new ObservableCollection<LogEntryDisplayItem>();

        #endregion

        #region ===================== 公共属性 =====================

        /// <summary> UI当前页日志条目显示集合</summary>
        public ObservableCollection<LogEntryDisplayItem> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }
        /// <summary>产线</summary>
        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set => SetProperty(ref _lines, value);
        }
        /// <summary>工位</summary>
        public List<craft_StationInfo> Stations
        {
            get => _stations;
            set => SetProperty(ref _stations, value);
        }
        /// <summary>产线选择索引</summary>
        public int LineSelectedIndex
        {
            get => _lineSelectedIndex;
            set => SetProperty(ref _lineSelectedIndex, value);
        }
        /// <summary>产线选择项</summary>
        public craft_LineInfo LineSelectedItem
        {
            get => _lineSelectedItem;
            set
            {
                if (value == null) return;
                _lineSelectedItem = value;
                LineSelectedIndex = Lines.FindIndex(l => l.Id == value.Id);
                FilterStationsByLine();//更新工位下拉框
                //_ = QueryAsync();
                RaisePropertyChanged();
            }
        }
        /// <summary>工位选择索引</summary>
        public int StationSelectedIndex
        {
            get => _stationSelectedIndex;
            set => SetProperty(ref _stationSelectedIndex, value);
        }
        /// <summary>工位选择项</summary>
        public craft_StationInfo StationSelectedItem
        {
            get => _stationSelectedItem;
            set
            {
                if (value == null) return;
                _stationSelectedItem = value;
                StationSelectedIndex = Stations.FindIndex(s => s.Id == value.Id);
                _ = QueryAsync();
                RaisePropertyChanged();
            }
        }
        /// <summary>选择时间</summary>
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                SetProperty(ref _selectedDate, value);
                _ = QueryAsync();
            }
        }
        /// <summary>日志等级选择</summary>
        public List<string> LevelFilters
        {
            get => _levelFilters;
            set => SetProperty(ref _levelFilters, value);
        }
        /// <summary>日志等级选择索引</summary>
        public int LevelFilterIndex
        {
            get => _levelFilterIndex;
            set => SetProperty(ref _levelFilterIndex, value);
        }
        /// <summary>日志等级选择项</summary>
        public string LevelFilterItem
        {
            get => _levelFilterItem;
            set
            {
                if (value == null) return;
                _levelFilterItem = value;
                LevelFilterIndex = LevelFilters.IndexOf(value);
                ApplyLevelFilter();
                RaisePropertyChanged();
            }
        }
        /// <summary>是否自动刷新勾选框</summary>
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                SetProperty(ref _isAutoRefreshEnabled, value);
                if (value)
                {
                    StartAutoRefresh();
                }
                else
                {
                    StopAutoRefresh();
                }
            }
        }

        /// <summary> 底部分页信息显示</summary>
        public string TotalCountText => $"共 {_allLogs.Count} 条记录";
        public string PageInfo => $"{_currentPage} / {_totalPages}";
        public bool CanGoPrevious => _currentPage > 1;
        /*{
            get => _canGoPrevious;
            set
            {
                SetProperty(ref _canGoPrevious, value);
            }
        }*/
        public bool CanGoNext => _currentPage < _totalPages;
        /*{
            get => _canGoNext;
            set
            {
                SetProperty(ref _canGoNext, value);
            }
        }*/
        #endregion

        #region ===================== 命令 =====================

        public DelegateCommand QueryCommand { get; set; }
        public DelegateCommand RefreshCommand { get; set; }
        public DelegateCommand<LogEntryDisplayItem> ShowDetailCommand { get; set; }
        /// <summary> 首页 </summary>
        public DelegateCommand FirstPageCommand { get; set; }
        /// <summary> 上一页 </summary>
        public DelegateCommand PreviousPageCommand { get; set; }
        /// <summary> 下一页 </summary>
        public DelegateCommand NextPageCommand { get; set; }
        /// <summary> 最后一页 </summary>
        public DelegateCommand LastPageCommand { get; set; }
        #endregion

        #region ===================== 构造 =====================

        public InteractiveEventsViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IDeviceLogService logService,
            ILoadingService loadingService,
            ILogger logger
            )
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _logService = logService;
            _loadingService = loadingService;

            QueryCommand = new DelegateCommand(OnQuery);
            RefreshCommand = new DelegateCommand(OnRefresh);
            ShowDetailCommand = new DelegateCommand<LogEntryDisplayItem>(OnShowDetail);
            FirstPageCommand = new DelegateCommand(OnFirstPage, () => CanGoPrevious);
            PreviousPageCommand = new DelegateCommand(OnPreviousPage, () => CanGoPrevious);
            NextPageCommand = new DelegateCommand(OnNextPage, () => CanGoNext);
            LastPageCommand = new DelegateCommand(OnLastPage, () => CanGoNext);
            InitializeData();
            StartAutoRefresh();
        }

        #endregion

        #region ===================== 初始化 =====================

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
            }

            // 默认选中第一个产线
            if (Lines.Any())
            {
                LineSelectedItem = Lines.First();
            }

            //订阅代码未写 需要补充订阅产线、工位
        }
        /// <summary> 筛选产线下的工位 </summary>
        private void FilterStationsByLine()
        {
            if (LineSelectedItem?.Id > 0)
            {
                Stations = _allStations
                    .Where(s => s.LineId == LineSelectedItem.Id)
                    .OrderBy(s => s.Code).ToList();
            }
            else
            {
                Stations = new List<craft_StationInfo>();
            }

            StationSelectedIndex = -1;
            StationSelectedItem = new craft_StationInfo();
        }

        #endregion

        #region ===================== 数据加载 =====================
        /// <summary>查询日志</summary>
        private async Task QueryAsync()
        {
            if (StationSelectedItem?.Id == 0) return;
            try
            {
                var logs = await _logService.GetLogsAsync(StationSelectedItem!.Id, SelectedDate);//获取选择工位和日期的日志
                _allLogs = logs;//暂存
                ApplyLevelFilter();//筛选日志等级
                UpdatePagination();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"查询失败：{ex.Message}", "错误");
            }
        }
        /// <summary>筛选日志等级</summary>
        private void ApplyLevelFilter()
        {
            var filtered = _allLogs.AsEnumerable();//转只读

            if (LevelFilterItem != "全部")//根据UI下拉框筛选日志等级
            {
                filtered = filtered.Where(l => l.Level == LevelFilterItem);
            }

            _allLogs = filtered.ToList();//将筛选完工位、日期、日志等级暂存
            UpdatePagination();//分页
        }
        /// <summary>日志分页处理</summary>
        private void UpdatePagination()
        {
            _totalPages = (int)Math.Ceiling((double)_allLogs.Count / _pageSize);//向上取整
            if (_totalPages == 0) _totalPages = 1;
            //限制当前页
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            RefreshDisplay();

            RaisePropertyChanged(nameof(TotalCountText));
            RaisePropertyChanged(nameof(PageInfo));
            FirstPageCommand?.RaiseCanExecuteChanged();
            NextPageCommand?.RaiseCanExecuteChanged();
            PreviousPageCommand?.RaiseCanExecuteChanged();
            LastPageCommand?.RaiseCanExecuteChanged();
        }

        private void RefreshDisplay()
        {
            var pagedLogs = _allLogs
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            LogEntries.Clear();
            foreach (var log in pagedLogs)
            {
                LogEntries.Add(new LogEntryDisplayItem(log));
            }
        }

        #endregion

        #region ===================== 命令 =====================实现
        /// <summary>查询按钮方法</summary>

        private async void OnQuery()
        {
            _currentPage = 1;
            await QueryAsync();
        }
        /// <summary>刷新按钮方法</summary>
        private async void OnRefresh()
        {
            await _loadingService.ExecuteAsync(QueryAsync, "正在加载日志...");
        }

        private void OnShowDetail(LogEntryDisplayItem item)
        {
            if (item == null) return;

            var parameters = new DialogParameters();
            parameters.Add("LogEntry", item.RawEntry);

            _dialogService.ShowDialog("LogDetailView", parameters, result => { });
        }
        /// <summary>首页命令</summary>
        private void OnFirstPage()
        {
            _currentPage = 1;
            RefreshDisplay();
            RaisePropertyChanged(nameof(PageInfo));
            FirstPageCommand?.RaiseCanExecuteChanged();
            NextPageCommand?.RaiseCanExecuteChanged();
            PreviousPageCommand?.RaiseCanExecuteChanged();
            LastPageCommand?.RaiseCanExecuteChanged();
        }
        /// <summary>上一页命令</summary>
        private void OnPreviousPage()
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                RefreshDisplay();
                RaisePropertyChanged(nameof(PageInfo));
                FirstPageCommand?.RaiseCanExecuteChanged();
                NextPageCommand?.RaiseCanExecuteChanged();
                PreviousPageCommand?.RaiseCanExecuteChanged();
                LastPageCommand?.RaiseCanExecuteChanged();
            }
        }

        private void OnNextPage()
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                RefreshDisplay();
                RaisePropertyChanged(nameof(PageInfo));
                FirstPageCommand?.RaiseCanExecuteChanged();
                NextPageCommand?.RaiseCanExecuteChanged();
                PreviousPageCommand?.RaiseCanExecuteChanged();
                LastPageCommand?.RaiseCanExecuteChanged();
            }
        }

        private void OnLastPage()
        {
            _currentPage = _totalPages;
            RefreshDisplay();
            RaisePropertyChanged(nameof(PageInfo));
            FirstPageCommand?.RaiseCanExecuteChanged();
            NextPageCommand?.RaiseCanExecuteChanged();
            PreviousPageCommand?.RaiseCanExecuteChanged();
            LastPageCommand?.RaiseCanExecuteChanged();
        }

        #endregion

        #region ===================== 实时刷新 =====================

        private void StartAutoRefresh()
        {
            if (_autoRefreshTimer != null) return;

            _autoRefreshTimer = new System.Timers.Timer(5000); // 5秒刷新一次
            _autoRefreshTimer.Elapsed += async (s, e) =>
            {
                if (Application.Current == null) return;
                await Application.Current.Dispatcher.Invoke(async () =>
                 {
                     await QueryAsync();
                 });
            };
            _autoRefreshTimer.Start();
        }

        private void StopAutoRefresh()
        {
            if (_autoRefreshTimer != null)
            {
                _autoRefreshTimer.Stop();
                _autoRefreshTimer.Dispose();
                _autoRefreshTimer = null;
            }
        }

        #endregion

        #region ===================== IDisposable =====================

        public void Dispose()
        {
            StopAutoRefresh();
        }

        #endregion
    }

    /// <summary>
    /// 日志条目显示模型
    /// </summary>
    public class LogEntryDisplayItem : BindableBase
    {
        private readonly DeviceLogEntry _entry;

        public DeviceLogEntry RawEntry => _entry;

        public LogEntryDisplayItem(DeviceLogEntry entry)
        {
            _entry = entry;
        }

        public string TimeDisplay => _entry.Timestamp.ToString("HH:mm:ss.fff");
        public string Level => _entry.Level;
        public string DeviceCode => _entry.DeviceCode;
        public string Message => _entry.Message;

        public bool HasDetail => !string.IsNullOrEmpty(_entry.Command) ||
                                   !string.IsNullOrEmpty(_entry.Response) ||
                                   !string.IsNullOrEmpty(_entry.Exception);

        public Style LevelStyle
        {
            get
            {
                var style = new Style(typeof(TextBlock));
                switch (_entry.Level)
                {
                    case "Info":
                        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.Blue)));
                        break;
                    case "Warning":
                        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.Orange)));
                        break;
                    case "Error":
                        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.Red)));
                        break;
                }
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
                return style;
            }
        }

    }
}
