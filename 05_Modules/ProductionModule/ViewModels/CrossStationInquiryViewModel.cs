using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using ProductionModule.Query;
using System.Collections.ObjectModel;
using System.Windows;

namespace ProductionModule.ViewModels
{
    /// <summary>
    /// 生产 - 产品工位状态查询（production_ProductStationStatus）
    /// </summary>
    public class CrossStationInquiryViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDataCacheService _cacheService; // 全局配置缓存
        private readonly IRepository<production_ProductStationStatus> _statusRepo;
        private readonly ILoadingService _loadingService;

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

        private int _statusSelectedIndex;
        private StatusFilterOption _statusSelectedItem = new() { Value = -1, Display = "全部" };
        private int _repairSelectedIndex;
        private RepairFilterOption _repairSelectedItem = new() { Value = -1, Display = "全部" };

        private DateTime _queryStartTime = DateTime.Today.AddDays(-7);
        private DateTime _queryEndTime = DateTime.Today.AddDays(1).AddSeconds(-1);

        private int _pageSize = 100;
        private int _selectedPageSize = 100;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _totalCount;
        private bool _isSearching;
        private bool _isModifying;

        private List<craft_StationInfo> _editStations = new();
        private craft_StationInfo _editStationSelectedItem = new();
        private StatusFilterOption _editStatusSelectedItem = new() { Value = 0, Display = "待过站" };
        private StationRecordDisplayItem? _selectedRecord;

        private readonly ObservableCollection<StationRecordDisplayItem> _records = new(); // 查询结果集合

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入缓存、仓储与加载服务，初始化命令与筛选条件 </summary>
        public CrossStationInquiryViewModel(
            IDataCacheService cacheService,
            IRepository<production_ProductStationStatus> statusRepo,
            ILoadingService loadingService)
        {
            _cacheService = cacheService;
            _statusRepo = statusRepo;
            _loadingService = loadingService;

            SearchCommand = new DelegateCommand(async () => await SearchAsync(resetPage: true), CanSearch);
            RefreshCommand = new DelegateCommand(async () => await SearchAsync(resetPage: false), CanSearch);
            FirstPageCommand = new DelegateCommand(GoFirstPage, () => CurrentPage > 1);
            PreviousPageCommand = new DelegateCommand(GoPreviousPage, () => CurrentPage > 1);
            NextPageCommand = new DelegateCommand(GoNextPage, () => CurrentPage < TotalPages);
            LastPageCommand = new DelegateCommand(GoLastPage, () => CurrentPage < TotalPages);
            ConfirmModifyCommand = new DelegateCommand(async () => await ConfirmModifyAsync(), CanConfirmModify);

            InitializeFilters(); // 从缓存加载产线/型号/工位
        }

        #endregion

        #region ===================== 公共属性 =====================

        /// <summary>手动修改：工位选项（全部工位）</summary>
        public IReadOnlyList<StatusFilterOption> EditStatusOptions { get; } = new[]
        {
            new StatusFilterOption { Value = 0, Display = "待过站" },
            new StatusFilterOption { Value = 1, Display = "合格" },
            new StatusFilterOption { Value = 2, Display = "不合格" }
        };

        public List<craft_StationInfo> EditStations
        {
            get => _editStations;
            set => SetProperty(ref _editStations, value);
        }

        public craft_StationInfo EditStationSelectedItem
        {
            get => _editStationSelectedItem;
            set => SetProperty(ref _editStationSelectedItem, value);
        }

        public StatusFilterOption EditStatusSelectedItem
        {
            get => _editStatusSelectedItem;
            set => SetProperty(ref _editStatusSelectedItem, value);
        }

        public StationRecordDisplayItem? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (!SetProperty(ref _selectedRecord, value))
                    return;

                SyncEditPanelFromSelection();
                ConfirmModifyCommand.RaiseCanExecuteChanged();
            }
        }

        public string SelectedRecordHint =>
            SelectedRecord == null
                ? "请先在左侧表格选择一条记录"
                : $"当前：{SelectedRecord.FlowCode} / {SelectedRecord.StationName} / {SelectedRecord.StatusText}";

        public DelegateCommand ConfirmModifyCommand { get; }

        public ObservableCollection<StationRecordDisplayItem> Records => _records;

        public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 50, 100, 200, 500 };

        public IReadOnlyList<StatusFilterOption> StatusOptions { get; } = new[]
        {
            new StatusFilterOption { Value = -1, Display = "全部" },
            new StatusFilterOption { Value = 0, Display = "待过站" },
            new StatusFilterOption { Value = 1, Display = "合格" },
            new StatusFilterOption { Value = 2, Display = "不合格" }
        };

        public IReadOnlyList<RepairFilterOption> RepairOptions { get; } = new[]
        {
            new RepairFilterOption { Value = -1, Display = "全部" },
            new RepairFilterOption { Value = 1, Display = "是" },
            new RepairFilterOption { Value = 0, Display = "否" }
        };

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

        public int StatusSelectedIndex
        {
            get => _statusSelectedIndex;
            set => SetProperty(ref _statusSelectedIndex, value);
        }

        public StatusFilterOption StatusSelectedItem
        {
            get => _statusSelectedItem;
            set
            {
                if (value == null) return;
                _statusSelectedItem = value;
                StatusSelectedIndex = StatusOptions.ToList().FindIndex(o => o.Value == value.Value);
                RaisePropertyChanged();
            }
        }

        public int RepairSelectedIndex
        {
            get => _repairSelectedIndex;
            set => SetProperty(ref _repairSelectedIndex, value);
        }

        public RepairFilterOption RepairSelectedItem
        {
            get => _repairSelectedItem;
            set
            {
                if (value == null) return;
                _repairSelectedItem = value;
                RepairSelectedIndex = RepairOptions.ToList().FindIndex(o => o.Value == value.Value);
                RaisePropertyChanged();
            }
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

        /// <summary> 从缓存初始化筛选下拉数据 </summary>
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
                EditStations = _allStations.OrderBy(s => s.Code).ToList();
            }

            EditStatusSelectedItem = EditStatusOptions[0];

            StatusSelectedIndex = 0;
            RepairSelectedIndex = 0;
        }

        private void FilterStationsByLine()
        {
            if (LineSelectedItem?.Id > 0)
            {
                LineStations = _allStations
                    .Where(s => s.LineId == LineSelectedItem.Id)
                    .OrderBy(s => s.Code)
                    .ToList();
                LineStations.Insert(0, new craft_StationInfo() { Name = "全部" });
                IsStationEnabled = LineStations.Any();
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

        private StationRecordQueryContext BuildQueryContext() => new()
        {
            FlowCode = FlowCodeFilter,
            LineId = LineSelectedItem?.Id ?? 0,
            StationId = StationSelectedItem?.Id ?? 0,
            ProductTypeId = TypeSelectedItem?.Id ?? 0,
            Status = StatusSelectedItem?.Value ?? -1,
            IsRepair = RepairSelectedItem?.Value ?? -1,
            QueryStartTime = QueryStartTime,
            QueryEndTime = QueryEndTime
        };

        private bool ValidateTimeRange()
        {
            if (QueryStartTime <= QueryEndTime)
                return true;
            HandyControl.Controls.MessageBox.Show("起始时间不能晚于结束时间。", "提示");
            return false;
        }

        private bool CanSearch() => !_isSearching && !_isModifying;

        private bool CanConfirmModify() =>
            !_isSearching && !_isModifying && SelectedRecord != null;

        private void SyncEditPanelFromSelection()
        {
            if (SelectedRecord == null)
                return;

            EditStationSelectedItem = EditStations.FirstOrDefault(s => s.Id == SelectedRecord.StationId)
                ?? new craft_StationInfo();

            EditStatusSelectedItem = EditStatusOptions.FirstOrDefault(o => o.Value == SelectedRecord.Status)
                ?? EditStatusOptions[0];

            RaisePropertyChanged(nameof(SelectedRecordHint));
        }

        private async Task ConfirmModifyAsync()
        {
            if (SelectedRecord == null)
            {
                HandyControl.Controls.MessageBox.Show("请先在表格中选择一条记录。", "提示");
                return;
            }

            if (EditStationSelectedItem?.Id <= 0)
            {
                HandyControl.Controls.MessageBox.Show("请选择工位。", "提示");
                return;
            }

            if (EditStatusSelectedItem == null)
            {
                HandyControl.Controls.MessageBox.Show("请选择状态。", "提示");
                return;
            }

            var stationName = EditStationSelectedItem.DisplayText;
            var statusText = EditStatusSelectedItem.Display;
            var confirmMessage =
                $"确认修改以下产品工位状态？\n\n" +
                $"流水码：{SelectedRecord.FlowCode}\n" +
                $"型号：{SelectedRecord.ProductTypeName}\n" +
                $"产线：{SelectedRecord.LineName}\n" +
                $"工位：{stationName}\n" +
                $"新状态：{statusText}";

            if (HandyControl.Controls.MessageBox.Show(
                    confirmMessage,
                    "确认修改",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _isModifying = true;
            ConfirmModifyCommand.RaiseCanExecuteChanged();
            SearchCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();

            var affectedRows = 0;
            try
            {
                const string updateSql = @"
                    UPDATE production_ProductStationStatus
                    SET Status = @Status, UpdateTime = GETDATE()
                    WHERE FlowCode = @FlowCode
                      AND ProductTypeId = @ProductTypeId
                      AND LineId = @LineId
                      AND StationId = @StationId";

                affectedRows = await _statusRepo.ExecuteAsync(updateSql, new
                {
                    Status = EditStatusSelectedItem.Value,
                    FlowCode = SelectedRecord.FlowCode,
                    ProductTypeId = SelectedRecord.ProductTypeId,
                    LineId = SelectedRecord.LineId,
                    StationId = EditStationSelectedItem.Id
                });

                if (affectedRows > 0)
                {
                    var newStatus = EditStatusSelectedItem.Value;
                    var targetStationId = EditStationSelectedItem.Id;

                    await RunOnUiThreadAsync(() => ApplyLocalGridUpdate(newStatus, targetStationId));

                    HandyControl.Controls.MessageBox.Show("修改成功。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show(
                        "修改失败：未找到匹配的工位状态记录，请确认该流水码在此工位下已有状态数据。",
                        "失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"修改失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isModifying = false;
                ConfirmModifyCommand.RaiseCanExecuteChanged();
                SearchCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }

        private static Task RunOnUiThreadAsync(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        /// <summary>修改成功后立即刷新表格中对应行（ObservableCollection 需 Remove+Insert 才能触发 UI 更新）</summary>
        private void ApplyLocalGridUpdate(int newStatus, int targetStationId)
        {
            if (SelectedRecord == null)
                return;
            var selectedRecord = SelectedRecord;

            var flowCode = selectedRecord.FlowCode;
            var productTypeId = selectedRecord.ProductTypeId;
            var lineId = selectedRecord.LineId;
            var newStatusText = StationRecordQueryHelper.FormatStatus(newStatus);
            var now = DateTime.Now;

            for (var i = 0; i < _records.Count; i++)
            {
                var item = _records[i];
                if (!string.Equals(item.FlowCode, flowCode, StringComparison.OrdinalIgnoreCase) ||
                    item.ProductTypeId != productTypeId ||
                    item.LineId != lineId ||
                    item.StationId != targetStationId)
                    continue;

                var updated = new StationRecordDisplayItem
                {
                    Id = item.Id,
                    FlowCode = item.FlowCode,
                    TrayCode = item.TrayCode,
                    StationId = item.StationId,
                    ProductTypeId = item.ProductTypeId,
                    LineId = item.LineId,
                    StationName = item.StationName,
                    ProductTypeName = item.ProductTypeName,
                    LineName = item.LineName,
                    Status = newStatus,
                    StatusText = newStatusText,
                    IsRepairText = item.IsRepairText,
                    RepairTargetStationName = item.RepairTargetStationName,
                    RepairCount = item.RepairCount,
                    UpdateTime = now,
                    CreateTime = item.CreateTime
                };

                _records.RemoveAt(i);
                _records.Insert(i, updated);

                if (ReferenceEquals(selectedRecord, item) || selectedRecord.Id == item.Id)
                    SelectedRecord = updated;

                break;
            }
        }

        private async Task SearchAsync(bool resetPage, bool forceRefresh = false)
        {
            if (!forceRefresh && (!CanSearch() || !ValidateTimeRange()))
                return;

            if (resetPage)
                _currentPage = 1;

            _isSearching = true;
            SearchCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();

            try
            {
                await _loadingService.ExecuteAsync(async () =>
                {
                    try
                    {
                        var context = BuildQueryContext();
                        var countSql = StationRecordQueryHelper.ApplyWhere(
                            StationRecordQueryHelper.CountSql, context);
                        var total = await _statusRepo.ExecuteScalarAsync<int>(
                            countSql, context.ToCountParameters());

                        if (total <= 0)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                _records.Clear();
                                UpdatePaginationState(0);
                            });
                            return;
                        }

                        UpdatePaginationBounds(total);

                        var pageSql = StationRecordQueryHelper.ApplyWhere(
                            StationRecordQueryHelper.PageSql, context);
                        var offset = (CurrentPage - 1) * _pageSize;
                        var rows = await _statusRepo.QueryAsync<production_ProductStationStatus>(
                            pageSql, context.ToPageParameters(offset, _pageSize));

                        var pageItems = (rows ?? Enumerable.Empty<production_ProductStationStatus>())
                            .Select(ToDisplayItem)
                            .ToList();

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _records.Clear();
                            foreach (var item in pageItems)
                                _records.Add(item);
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
                }, "正在查询产品工位状态...");
            }
            finally
            {
                _isSearching = false;
                SearchCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }

        private StationRecordDisplayItem ToDisplayItem(production_ProductStationStatus row)
        {
            _stationDict.TryGetValue(row.StationId, out var station);
            _typeDict.TryGetValue(row.ProductTypeId, out var type);
            _lineDict.TryGetValue(row.LineId, out var line);

            string repairTargetName = "-";
            if (row.RepairTargetStationId > 0 &&
                _stationDict.TryGetValue(row.RepairTargetStationId, out var target))
                repairTargetName = target.DisplayText;

            return new StationRecordDisplayItem
            {
                Id = row.Id,
                FlowCode = row.FlowCode,
                TrayCode = row.TrayCode,
                StationId = row.StationId,
                ProductTypeId = row.ProductTypeId,
                LineId = row.LineId,
                StationName = station?.DisplayText ?? row.StationId.ToString(),
                ProductTypeName = type?.Name ?? row.ProductTypeId.ToString(),
                LineName = line?.Name ?? (row.LineId > 0 ? row.LineId.ToString() : "-"),
                Status = row.Status,
                StatusText = StationRecordQueryHelper.FormatStatus(row.Status),
                IsRepairText = row.IsRepair ? "是" : "否",
                RepairTargetStationName = repairTargetName,
                RepairCount = row.RepairCount,
                UpdateTime = row.UpdateTime,
                CreateTime = row.CreateTime
            };
        }

        private void UpdatePaginationBounds(int total)
        {
            TotalCount = total;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)total / _pageSize));
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

        private void GoFirstPage()
        {
            CurrentPage = 1;
            _ = SearchAsync(resetPage: false);
        }

        private void GoPreviousPage()
        {
            if (CurrentPage > 1)
                CurrentPage--;
            _ = SearchAsync(resetPage: false);
        }

        private void GoNextPage()
        {
            if (CurrentPage < TotalPages)
                CurrentPage++;
            _ = SearchAsync(resetPage: false);
        }

        private void GoLastPage()
        {
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

        #endregion
    }
}
