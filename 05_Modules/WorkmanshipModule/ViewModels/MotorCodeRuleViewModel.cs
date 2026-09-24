using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.MotorCode;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Core.Services.RepositoryGrop;
using ProductionLineManage.Services.MotorCode;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;

namespace WorkmanshipModule.ViewModels
{
    public class MotorCodeFixedInfoOption
    {
        public int Id { get; init; }
        public string Display { get; init; } = string.Empty;
    }

    public class MotorCodeSegmentBindRow : BindableBase
    {
        public int Id { get; set; }

        private string _segmentCode = string.Empty;
        private int _segmentType = (int)MotorCodeSegmentType.Year;
        private int _fixedSegmentId;
        private string _previewValue = string.Empty;
        private string _remarks = string.Empty;

        public string SegmentCode
        {
            get => _segmentCode;
            set => SetProperty(ref _segmentCode, value);
        }

        public int SegmentType
        {
            get => _segmentType;
            set => SetProperty(ref _segmentType, value);
        }

        public int FixedSegmentId
        {
            get => _fixedSegmentId;
            set => SetProperty(ref _fixedSegmentId, value);
        }

        public string PreviewValue
        {
            get => _previewValue;
            set => SetProperty(ref _previewValue, value);
        }

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        public string TypeDisplay => MotorCodeSegmentTypeOption.GetDisplay(SegmentType);
    }

    /// <summary>电机码 - 生成规则与模拟取号</summary>
    public class MotorCodeRuleViewModel : BindableBase, INavigationAware
    {
        #region ===================== 私有字段 =====================

        private readonly IDataCacheService _cache; // 全局配置缓存
        private readonly IMotorCodeService _motorCodeService;
        private readonly IRepository<craft_MotorCodeRule> _ruleRepo;
        private readonly IRepository<craft_MotorCodeSegmentBind> _bindRepo;
        private readonly IMotorCodeCacheService _motorCodeCache;
        private readonly IEventAggregator _eventAggregator;

        private List<craft_TypeInfo> _types = new();
        private craft_TypeInfo? _selectedType;
        private int _filterCategory;
        private string _formula = string.Empty;
        private bool _isEnabled = true;
        private string _previewCode = string.Empty;
        private int _previewSequence;
        private int _simulateClickCount;
        private int _simulateBaseValue;
        private int _simulateStep = 1;
        private string _simulateHint = string.Empty;
        private bool _isRebuildingDisplayRows;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入电机码相关服务并初始化命令与绑定集合 </summary>
        public MotorCodeRuleViewModel(
            IDataCacheService cache,
            IMotorCodeService motorCodeService,
            IRepository<craft_MotorCodeRule> ruleRepo,
            IRepository<craft_MotorCodeSegmentBind> bindRepo,
            IMotorCodeCacheService motorCodeCache,
            IEventAggregator eventAggregator)
        {
            _cache = cache;
            _motorCodeService = motorCodeService;
            _ruleRepo = ruleRepo;
            _bindRepo = bindRepo;
            _motorCodeCache = motorCodeCache;
            _eventAggregator = eventAggregator;

            _eventAggregator.GetEvent<MotorCodeConfigUpdatedEvent>()
                .Subscribe(OnMotorCodeConfigUpdated, ThreadOption.UIThread);

            BindRows = new ObservableCollection<MotorCodeSegmentBindRow>();
            DisplayBindRows = new ObservableCollection<MotorCodeSegmentBindRow>();
            BindRows.CollectionChanged += OnBindRowsCollectionChanged;

            FormulaSegmentCodes = new ObservableCollection<string>();
            FixedInfoOptions = new ObservableCollection<MotorCodeFixedInfoOption>();

            SaveCommand = new DelegateCommand(async () => await SaveAsync());
            RefreshCommand = new DelegateCommand(async () => await RefreshAllAsync());
            SimulateCommand = new DelegateCommand(async () => await SimulateAsync());
            AddBindCommand = new DelegateCommand(AddBindRow);
            DeleteBindCommand = new DelegateCommand<MotorCodeSegmentBindRow>(DeleteBindRow);
            AppendSegmentCommand = new DelegateCommand<string?>(AppendSegment);

            LoadTypes(); // 加载型号列表
        }

        #endregion

        #region ===================== 公共属性 =====================

        public List<craft_TypeInfo> Types
        {
            get => _types;
            set => SetProperty(ref _types, value);
        }

        public craft_TypeInfo? SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value) && value != null)
                {
                    ResetSimulateSession();
                    _ = RefreshAllAsync();
                }
            }
        }

        public int FilterCategory
        {
            get => _filterCategory;
            set
            {
                if (SetProperty(ref _filterCategory, value))
                    RebuildDisplayBindRows();
            }
        }

        public string Formula
        {
            get => _formula;
            set => SetProperty(ref _formula, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public string PreviewCode
        {
            get => _previewCode;
            set => SetProperty(ref _previewCode, value);
        }

        public int PreviewSequence
        {
            get => _previewSequence;
            set => SetProperty(ref _previewSequence, value);
        }

        public string SimulateHint
        {
            get => _simulateHint;
            set => SetProperty(ref _simulateHint, value);
        }

        /// <summary>全部片段（数据源）</summary>
        public ObservableCollection<MotorCodeSegmentBindRow> BindRows { get; }

        /// <summary>DataGrid 绑定：按信息类型筛选后的同一批 row 对象</summary>
        public ObservableCollection<MotorCodeSegmentBindRow> DisplayBindRows { get; }

        public ObservableCollection<string> FormulaSegmentCodes { get; }
        public ObservableCollection<MotorCodeFixedInfoOption> FixedInfoOptions { get; }

        public IReadOnlyList<MotorCodeSegmentTypeOption> SegmentTypeOptions => MotorCodeSegmentTypeOption.All;
        public IReadOnlyList<MotorCodeFilterCategoryOption> FilterCategoryOptions => MotorCodeFilterCategoryOption.All;

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand SimulateCommand { get; }
        public DelegateCommand AddBindCommand { get; }
        public DelegateCommand<MotorCodeSegmentBindRow> DeleteBindCommand { get; }
        public DelegateCommand<string?> AppendSegmentCommand { get; }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            ResetSimulateSession();
            _ = RefreshSimulateBaseAsync();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) => ResetSimulateSession();

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 从缓存加载型号列表 </summary>
        private void LoadTypes()
        {
            Types = _cache.GetData<List<craft_TypeInfo>>() ?? new List<craft_TypeInfo>();
            SelectedType = Types.FirstOrDefault();
        }

        private void ResetSimulateSession()
        {
            _simulateClickCount = 0;
            PreviewCode = string.Empty;
            PreviewSequence = 0;
            SimulateHint = "模拟不占号；离开本页后重新计数";
        }

        private async Task RefreshAllAsync()
        {
            await RefreshSimulateBaseAsync();
            LoadFixedInfoOptions();
            await LoadRuleAsync();
        }

        private async Task RefreshSimulateBaseAsync()
        {
            if (SelectedType != null)
            {
                var status = await _motorCodeService.GetSequenceStatusAsync(SelectedType.Id);
                if (status != null)
                {
                    _simulateBaseValue = status.CurrentValue;
                    _simulateStep = status.Step;
                }
            }

            ResetSimulateSession();
            RefreshAllPreviews();
        }

        private void LoadFixedInfoOptions()
        {
            FixedInfoOptions.Clear();
            if (SelectedType == null)
                return;

            var snapshot = _motorCodeCache.GetSnapshot();
            foreach (var f in snapshot.FixedSegments
                         .Where(x => x.ProductTypeId == SelectedType.Id)
                         .OrderBy(x => x.SortOrder)
                         .ThenBy(x => x.Id))
            {
                FixedInfoOptions.Add(new MotorCodeFixedInfoOption
                {
                    Id = f.Id,
                    Display = f.FixedValue
                });
            }
        }

        private async Task LoadRuleAsync()
        {
            if (SelectedType == null)
                return;

            foreach (var row in BindRows.ToList())
                row.PropertyChanged -= OnBindRowPropertyChanged;

            BindRows.Clear();

            var typeId = SelectedType.Id;
            var rule = (await _ruleRepo.GetAllAsync()).FirstOrDefault(r => r.ProductTypeId == typeId);
            Formula = rule?.Formula ?? string.Empty;
            IsEnabled = rule?.IsEnabled ?? true;

            var binds = (await _bindRepo.GetAllAsync())
                .Where(b => b.ProductTypeId == typeId)
                .OrderBy(b => b.SortOrder);

            foreach (var b in binds)
            {
                BindRows.Add(new MotorCodeSegmentBindRow
                {
                    Id = b.Id,
                    SegmentCode = b.SegmentCode,
                    SegmentType = b.SegmentType,
                    FixedSegmentId = b.FixedSegmentId,
                    Remarks = b.Remarks
                });
            }

            RebuildDisplayBindRows();
            UpdateFormulaSegmentCodes();
            RefreshAllPreviews();
        }

        private void OnBindRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                if (!_isRebuildingDisplayRows)
                    DisplayBindRows.Clear();
                UpdateFormulaSegmentCodes();
                RefreshAllPreviews();
                return;
            }

            if (e.NewItems != null)
            {
                foreach (MotorCodeSegmentBindRow row in e.NewItems)
                {
                    row.PropertyChanged += OnBindRowPropertyChanged;
                    if (PassesFilter(row))
                        DisplayBindRows.Add(row);
                }
            }

            if (e.OldItems != null)
            {
                foreach (MotorCodeSegmentBindRow row in e.OldItems)
                {
                    row.PropertyChanged -= OnBindRowPropertyChanged;
                    DisplayBindRows.Remove(row);
                }
            }

            UpdateFormulaSegmentCodes();
            RefreshAllPreviews();
        }

        private void OnBindRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MotorCodeSegmentBindRow row)
                return;

            if (e.PropertyName == nameof(MotorCodeSegmentBindRow.SegmentType))
            {
                if (row.SegmentType != (int)MotorCodeSegmentType.Fixed)
                    row.FixedSegmentId = 0;

                RebuildDisplayBindRows();
            }

            if (e.PropertyName is nameof(MotorCodeSegmentBindRow.SegmentCode)
                or nameof(MotorCodeSegmentBindRow.SegmentType)
                or nameof(MotorCodeSegmentBindRow.FixedSegmentId))
            {
                RefreshRowPreview(row);
                UpdateFormulaSegmentCodes();
            }
        }

        private bool PassesFilter(MotorCodeSegmentBindRow row)
        {
            if (FilterCategory == (int)MotorCodeSegmentFilterCategory.All)
                return true;

            return (int)MotorCodeSegmentTypeOption.GetCategory(row.SegmentType) == FilterCategory;
        }

        private void RebuildDisplayBindRows()
        {
            _isRebuildingDisplayRows = true;
            try
            {
                DisplayBindRows.Clear();
                foreach (var row in BindRows.Where(PassesFilter))
                    DisplayBindRows.Add(row);
            }
            finally
            {
                _isRebuildingDisplayRows = false;
            }
        }

        private void RefreshAllPreviews()
        {
            foreach (var row in BindRows)
                RefreshRowPreview(row);
        }

        private void RefreshRowPreview(MotorCodeSegmentBindRow? row)
        {
            if (row == null || SelectedType == null)
                return;

            var snapshot = _motorCodeCache.GetSnapshot();
            var digitLength = snapshot.GetSequenceConfig(SelectedType.Id)?.DigitLength ?? 4;
            var seq = _simulateBaseValue + _simulateStep;

            var bind = new craft_MotorCodeSegmentBind
            {
                SegmentType = row.SegmentType,
                FixedSegmentId = row.FixedSegmentId
            };

            row.PreviewValue = MotorCodePreviewHelper.PreviewSegmentValue(
                snapshot,
                SelectedType.Id,
                bind,
                DateTime.Now,
                seq,
                digitLength);
        }

        private void UpdateFormulaSegmentCodes()
        {
            FormulaSegmentCodes.Clear();
            foreach (var code in BindRows
                         .Select(r => r.SegmentCode?.Trim())
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                FormulaSegmentCodes.Add(code!);
            }
        }

        private void AddBindRow()
        {
            var next = BindRows.Count + 1;
            BindRows.Add(new MotorCodeSegmentBindRow
            {
                SegmentCode = $"D{next}",
                SegmentType = (int)MotorCodeSegmentType.Fixed
            });
        }

        private void DeleteBindRow(MotorCodeSegmentBindRow? row)
        {
            if (row != null)
                BindRows.Remove(row);
        }

        private void AppendSegment(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return;

            Formula = string.IsNullOrWhiteSpace(Formula) ? code : $"{Formula}+{code}";
        }

        private async Task SaveAsync()
        {
            if (SelectedType == null)
                return;

            if (string.IsNullOrWhiteSpace(Formula))
            {
                MessageBox.Show("请填写组合公式", "校验");
                return;
            }

            if (BindRows.Count == 0)
            {
                MessageBox.Show("请至少配置一个片段", "校验");
                return;
            }

            if (BindRows.Any(r => string.IsNullOrWhiteSpace(r.SegmentCode)))
            {
                MessageBox.Show("代号不能为空", "校验");
                return;
            }

            if (BindRows.GroupBy(r => r.SegmentCode.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            {
                MessageBox.Show("代号不能重复", "校验");
                return;
            }

            foreach (var row in BindRows.Where(r => r.SegmentType == (int)MotorCodeSegmentType.Fixed))
            {
                if (row.FixedSegmentId <= 0)
                {
                    MessageBox.Show($"代号 {row.SegmentCode} 为固定信息类型，请选择对应固定信息", "校验");
                    return;
                }
            }

            var typeId = SelectedType.Id;

            var existingRules = (await _ruleRepo.GetAllAsync()).Where(r => r.ProductTypeId == typeId);
            foreach (var r in existingRules)
                await _ruleRepo.DeleteAsync(r.Id);

            await _ruleRepo.InsertAsync(new craft_MotorCodeRule
            {
                ProductTypeId = typeId,
                Formula = Formula.Trim(),
                IsEnabled = IsEnabled
            });

            var existingBinds = (await _bindRepo.GetAllAsync()).Where(b => b.ProductTypeId == typeId);
            foreach (var b in existingBinds)
                await _bindRepo.DeleteAsync(b.Id);

            var order = 0;
            foreach (var row in BindRows)
            {
                await _bindRepo.InsertAsync(new craft_MotorCodeSegmentBind
                {
                    ProductTypeId = typeId,
                    SegmentCode = row.SegmentCode.Trim(),
                    SegmentType = row.SegmentType,
                    FixedSegmentId = row.SegmentType == (int)MotorCodeSegmentType.Fixed ? row.FixedSegmentId : 0,
                    SortOrder = order++,
                    Remarks = row.Remarks?.Trim() ?? string.Empty
                });
            }

            await _motorCodeCache.RefreshAsync();
            LoadFixedInfoOptions();
            RefreshAllPreviews();
            MessageBox.Show("生成规则已保存", "提示");
        }

        private void OnMotorCodeConfigUpdated(MotorCodeCacheSnapshot snapshot)
        {
            if (SelectedType != null)
                _simulateStep = snapshot.GetSequenceConfig(SelectedType.Id)?.Step ?? _simulateStep;
            LoadFixedInfoOptions();
            _ = RefreshSimulateBaseAsync();
        }

        private async Task SimulateAsync()
        {
            if (SelectedType == null)
                return;

            var seq = _simulateBaseValue + (_simulateClickCount + 1) * _simulateStep;

            var result = await _motorCodeService.GenerateAsync(SelectedType.Id, new MotorCodeGenerateOptions
            {
                Simulate = true,
                SimulateSequenceValue = seq
            });

            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "模拟失败");
                return;
            }

            _simulateClickCount++;
            PreviewCode = result.MotorCode;
            PreviewSequence = result.SequenceValue;
            SimulateHint = $"本页已模拟 {_simulateClickCount} 次（不占号）";
        }

        #endregion
    }
}
