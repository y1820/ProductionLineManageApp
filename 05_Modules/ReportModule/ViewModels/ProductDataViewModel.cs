using Microsoft.Win32;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using ProductionLineManage.Services.Report;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace ReportModule.ViewModels
{
    /// <summary>
    /// 报表 - 产品加工数据查询（report_ProcessHistory）
    /// </summary>
    public class ProductDataViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDataCacheService _cacheService; // 全局配置缓存
        private readonly IRepository<report_ProcessHistory> _historyRepo;
        private readonly ILoadingService _loadingService;
        private readonly ProductDataExcelExporter _excelExporter = new();
        private readonly ProductDataCsvExporter _csvExporter = new();

        private List<craft_LineInfo> _lines = new();
        private List<craft_TypeInfo> _types = new();
        private List<craft_StationInfo> _allStations = new();
        private Dictionary<int, craft_StationInfo> _stationDict = new();
        private Dictionary<int, craft_TypeInfo> _typeDict = new();
        private Dictionary<int, craft_LineInfo> _lineDict = new();

        private string _flowCodeFilter = string.Empty;
        private int _lineSelectedIndex = -1;
        private craft_LineInfo _lineSelectedItem = new();
        private int _stationSelectedIndex = -1;
        private craft_StationInfo _stationSelectedItem = new();
        private int _typeSelectedIndex = -1;
        private craft_TypeInfo _typeSelectedItem = new();
        private List<craft_StationInfo> _lineStations = new();
        private bool _isStationEnabled;

        private DateTime _queryStartTime = DateTime.Today;
        private DateTime _queryEndTime = DateTime.Today.AddDays(1).AddSeconds(-1);

        private int _pageSize = 100;
        private int _selectedPageSize = 100;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _totalCount;

        private bool _isSearching;
        private bool _isExporting;

        private readonly ObservableCollection<ProductDataDisplayItem> _productData = new(); // 查询结果集合

        #endregion

        #region ===================== 公共属性 =====================

        public ObservableCollection<ProductDataDisplayItem> ProductData => _productData;

        public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 50, 100, 200, 500 };

        public int SelectedPageSize
        {
            get => _selectedPageSize;
            set
            {
                if (!PageSizeOptions.Contains(value) || _selectedPageSize == value)
                    return;

                _selectedPageSize = value;
                _pageSize = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(PageSize));
            }
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

        public string FlowCodeFilter
        {
            get => _flowCodeFilter;
            set => SetProperty(ref _flowCodeFilter, value);
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
                RaisePropertyChanged();
            }
        }

        public bool IsStationEnabled
        {
            get => _isStationEnabled;
            set => SetProperty(ref _isStationEnabled, value);
        }

        public DateTime QueryStartTime
        {
            get => _queryStartTime;
            set => SetProperty(ref _queryStartTime, value);
        }

        public DateTime QueryEndTime
        {
            get => _queryEndTime;
            set => SetProperty(ref _queryEndTime, value);
        }

        public int PageSize
        {
            get => _pageSize;
            set => SetProperty(ref _pageSize, value);
        }

        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public int TotalPages
        {
            get => _totalPages;
            set => SetProperty(ref _totalPages, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public bool IsExporting
        {
            get => _isExporting;
            set => SetProperty(ref _isExporting, value);
        }

        public string TotalCountText => $"共 {TotalCount:N0} 条记录";
        public string PageInfo => $"{CurrentPage} / {TotalPages}";

        #endregion

        #region ===================== 命令 =====================

        public DelegateCommand SearchCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand FirstPageCommand { get; }
        public DelegateCommand PreviousPageCommand { get; }
        public DelegateCommand NextPageCommand { get; }
        public DelegateCommand LastPageCommand { get; }

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入缓存、仓储与加载服务，初始化查询/导出命令 </summary>
        public ProductDataViewModel(
            IDataCacheService cacheService,
            IRepository<report_ProcessHistory> historyRepo,
            ILoadingService loadingService)
        {
            _cacheService = cacheService;
            _historyRepo = historyRepo;
            _loadingService = loadingService;

            SearchCommand = new DelegateCommand(async () => await SearchAsync(resetPage: true), CanOperateQueryAndExport);
            RefreshCommand = new DelegateCommand(async () => await SearchAsync(resetPage: false), CanOperateQueryAndExport);
            ExportCommand = new DelegateCommand(async () => await ExportAsync(), CanOperateQueryAndExport);
            FirstPageCommand = new DelegateCommand(GoFirstPage, () => CurrentPage > 1);
            PreviousPageCommand = new DelegateCommand(GoPreviousPage, () => CurrentPage > 1);
            NextPageCommand = new DelegateCommand(GoNextPage, () => CurrentPage < TotalPages);
            LastPageCommand = new DelegateCommand(GoLastPage, () => CurrentPage < TotalPages);

            InitializeFilters(); // 从缓存加载筛选下拉数据
        }

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
            {
                Types = _cacheService.GetData<List<craft_TypeInfo>>();
                TypeSelectedIndex = -1;
            }

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
                var stations = _allStations
                    .Where(s => s.LineId == LineSelectedItem.Id)
                    .OrderBy(s => s.Code)
                    .ToList();
                bool hasStations = stations.Any();//是否有工位信息
                if (hasStations)
                {
                    IsStationEnabled = true;//使能工位选择下拉框控件
                    stations.Insert(0, new craft_StationInfo() { Name = "全部" });//增加一个全部工位项
                    LineStations = stations;
                }
                else
                {
                    IsStationEnabled = false;
                }
            }
            else
            {
                LineStations = new List<craft_StationInfo>();
                IsStationEnabled = false;
            }

            ResetStationSelection();
        }

        private void ResetStationSelection()
        {
            _stationSelectedItem = new craft_StationInfo();
            _stationSelectedIndex = -1;
            RaisePropertyChanged(nameof(StationSelectedItem));
            RaisePropertyChanged(nameof(StationSelectedIndex));
        }

        private ProductDataQueryContext BuildQueryContext()
        {
            return ProductDataQueryHelper.Build(
                FlowCodeFilter,
                LineSelectedItem?.Id ?? 0,
                StationSelectedItem?.Id ?? 0,
                TypeSelectedItem?.Id ?? 0,
                QueryStartTime,
                QueryEndTime,
                _allStations);
        }

        private bool ValidateTimeRange()
        {
            if (QueryStartTime <= QueryEndTime)
                return true;

            HandyControl.Controls.MessageBox.Show("起始时间不能晚于结束时间。", "提示");
            return false;
        }

        private async Task SearchAsync(bool resetPage)
        {
            if (!CanOperateQueryAndExport() || !ValidateTimeRange())
                return;

            if (resetPage)
                _currentPage = 1;

            SetOperationState(isSearching: true, isExporting: false);

            try
            {
                await _loadingService.ExecuteAsync(async () =>
                {
                    try
                    {
                        var context = BuildQueryContext();
                        if (context.IsEmptyLineScope)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                ClearResults();
                                UpdatePaginationState(0);
                            });
                            return;
                        }

                        var total = await _historyRepo.ExecuteScalarAsync<int>(
                            ProductDataQueryHelper.CountSql,
                            context.ToSqlParameters());

                        if (total <= 0)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                ClearResults();
                                UpdatePaginationState(0);
                            });
                            return;
                        }

                        UpdatePaginationBounds(total);

                        var offset = (CurrentPage - 1) * PageSize;
                        var rows = await _historyRepo.QueryAsync<report_ProcessHistory>(
                            ProductDataQueryHelper.PageSql,
                            context.ToSqlParameters(offset, PageSize));

                        var pageItems = (rows ?? Enumerable.Empty<report_ProcessHistory>())
                            .Select(ToDisplayItem)
                            .ToList();

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ReplacePageItems(pageItems);
                            TotalCount = total;
                            RaisePropertyChanged(nameof(TotalCountText));
                            RaisePropertyChanged(nameof(PageInfo));
                            RefreshPageCommands();
                        });
                    }
                    catch (Exception ex)
                    {
                        HandyControl.Controls.MessageBox.Show($"查询失败：{ex.Message}", "错误");
                    }
                }, "正在查询产品加工数据...");
            }
            finally
            {
                SetOperationState(isSearching: false, isExporting: false);
            }
        }

        private void UpdatePaginationBounds(int total)
        {
            TotalCount = total;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
            if (CurrentPage > TotalPages)
                CurrentPage = TotalPages;
            if (CurrentPage < 1)
                CurrentPage = 1;
        }

        private void UpdatePaginationState(int total)
        {
            TotalCount = total;
            TotalPages = 1;
            CurrentPage = 1;
            RaisePropertyChanged(nameof(TotalCountText));
            RaisePropertyChanged(nameof(PageInfo));
            RefreshPageCommands();
        }

        private void ClearResults()
        {
            _productData.Clear();
        }

        private void ReplacePageItems(IReadOnlyList<ProductDataDisplayItem> pageItems)
        {
            _productData.Clear();
            foreach (var item in pageItems)
            {
                _productData.Add(item);
            }
        }

        private bool CanOperateQueryAndExport() => !IsSearching && !IsExporting;

        private void SetOperationState(bool isSearching, bool isExporting)
        {
            _isSearching = isSearching;
            _isExporting = isExporting;
            RaisePropertyChanged(nameof(IsSearching));
            RaisePropertyChanged(nameof(IsExporting));
            RefreshOperationCommands();
        }

        private void RefreshOperationCommands()
        {
            SearchCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
        }

        private async Task ExportAsync()
        {
            if (!CanOperateQueryAndExport() || !ValidateTimeRange())
                return;

            var context = BuildQueryContext();
            if (context.IsEmptyLineScope)
            {
                HandyControl.Controls.MessageBox.Show("当前筛选条件下没有可导出的数据。", "提示");
                return;
            }

            var total = await _historyRepo.ExecuteScalarAsync<int>(
                ProductDataQueryHelper.CountSql,
                context.ToSqlParameters());

            if (total <= 0)
            {
                HandyControl.Controls.MessageBox.Show("当前筛选条件下没有可导出的数据。", "提示");
                return;
            }

            var lineName = LineSelectedItem?.Id > 0 ? LineSelectedItem.Name : "全部产线";
            var defaultFileName = ProductDataExportHelper.BuildDefaultFileName(lineName, QueryStartTime, QueryEndTime, ".csv");

            var dialog = new SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv|Excel 文件 (*.xlsx)|*.xlsx",
                FilterIndex = 1,
                FileName = defaultFileName,
                Title = "导出产品加工数据"
            };

            if (dialog.ShowDialog() != true)
                return;

            var exportCsv = ProductDataExportHelper.IsCsvExport(dialog.FileName);
            SetOperationState(isSearching: false, isExporting: true);

            try
            {
                await _loadingService.ExecuteAsync(async () =>
                {
                    try
                    {
                        DateTime? lastCreateTime = null;
                        var lastId = 0;

                        async Task<IReadOnlyList<report_ProcessHistory>> FetchBatchAsync(int _, int fetch)
                        {
                            var rows = await _historyRepo.QueryAsync<report_ProcessHistory>(
                                ProductDataQueryHelper.ExportBatchSql,
                                context.ToExportSqlParameters(lastCreateTime, lastId, fetch));

                            var batch = (rows ?? Enumerable.Empty<report_ProcessHistory>()).ToList();
                            if (batch.Count > 0)
                            {
                                var last = batch[^1];
                                lastCreateTime = last.CreateTime;
                                lastId = last.Id;
                            }

                            return batch;
                        }

                        var exportStopwatch = Stopwatch.StartNew();

                        ProductDataExportResult exportResult;
                        if (exportCsv)
                        {
                            exportResult = await _csvExporter.ExportAsync(
                                dialog.FileName,
                                FetchBatchAsync,
                                _stationDict,
                                _typeDict,
                                _lineDict);
                        }
                        else
                        {
                            exportResult = await _excelExporter.ExportAsync(
                                dialog.FileName,
                                FetchBatchAsync,
                                _stationDict,
                                _typeDict,
                                _lineDict);
                        }

                        exportStopwatch.Stop();
                        var formatName = exportCsv ? "CSV" : "Excel";
                        var pageInfo = exportResult.PageCount > 1
                            ? $"\n分页：{exportResult.PageCount} 个{(exportCsv ? "文件" : "Sheet")}（第一页、第二页…）"
                            : string.Empty;
                        var pathInfo = exportResult.OutputPaths.Count > 1
                            ? string.Join("\n", exportResult.OutputPaths)
                            : exportResult.PrimaryPath;

                        HandyControl.Controls.MessageBox.Show(
                            $"导出完成，共 {total:N0} 条。\n格式：{formatName}{pageInfo}\n用时：{FormatExportElapsed(exportStopwatch.Elapsed)}\n{pathInfo}",
                            "提示");
                    }
                    catch (Exception ex)
                    {
                        HandyControl.Controls.MessageBox.Show($"导出失败：{ex.Message}", "错误");
                    }
                }, "正在导出产品加工数据...");
            }
            finally
            {
                SetOperationState(isSearching: false, isExporting: false);
            }
        }

        private static string FormatExportElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalHours >= 1)
            {
                return $"{(int)elapsed.TotalHours}小时{elapsed.Minutes}分{elapsed.Seconds}秒";
            }

            if (elapsed.TotalMinutes >= 1)
            {
                return $"{elapsed.Minutes}分{elapsed.Seconds}.{elapsed.Milliseconds / 100}秒";
            }

            return $"{elapsed.TotalSeconds:F1} 秒";
        }

        private void GoFirstPage()
        {
            if (CurrentPage == 1) return;
            CurrentPage = 1;
            _ = SearchAsync(resetPage: false);
        }

        private void GoPreviousPage()
        {
            if (CurrentPage <= 1) return;
            CurrentPage--;
            _ = SearchAsync(resetPage: false);
        }

        private void GoNextPage()
        {
            if (CurrentPage >= TotalPages) return;
            CurrentPage++;
            _ = SearchAsync(resetPage: false);
        }

        private void GoLastPage()
        {
            if (CurrentPage >= TotalPages) return;
            CurrentPage = TotalPages;
            _ = SearchAsync(resetPage: false);
        }

        private void RefreshPageCommands()
        {
            FirstPageCommand.RaiseCanExecuteChanged();
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
            LastPageCommand.RaiseCanExecuteChanged();
        }

        private ProductDataDisplayItem ToDisplayItem(report_ProcessHistory row)
        {
            _stationDict.TryGetValue(row.StationId, out var station);
            _typeDict.TryGetValue(row.ProductTypeId, out var type);
            _lineDict.TryGetValue(row.LineId, out var line);

            return new ProductDataDisplayItem
            {
                Id = row.Id,
                StationRecordId = row.StationRecordId,
                FlowCode = row.FlowCode,
                StationName = station?.DisplayText ?? row.StationId.ToString(),
                ProductTypeName = type?.Name ?? row.ProductTypeId.ToString(),
                LineName = line?.Name ?? (row.LineId > 0 ? row.LineId.ToString() : "-"),
                DataName = row.DataName,
                DataValue = row.DataValue,
                DataType = row.DataType,
                DataUnit = row.DataUnit,
                IsRepairText = row.IsRepair ? "是" : "否",
                CreateTime = row.CreateTime
            };
        }

        #endregion
    }
}
