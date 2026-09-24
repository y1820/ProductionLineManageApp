using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Windows;

namespace WorkmanshipModule.ViewModels
{
    public class MotorCodeFixedUsageOption
    {
        public int Value { get; init; }
        public string Display { get; init; } = string.Empty;
    }

    public class MotorCodeFixedSegmentRow : BindableBase
    {
        public int Id { get; set; }

        private string _fixedValue = string.Empty;
        private int _usageType;

        public string FixedValue
        {
            get => _fixedValue;
            set => SetProperty(ref _fixedValue, value);
        }

        public int UsageType
        {
            get => _usageType;
            set => SetProperty(ref _usageType, value);
        }
    }

    /// <summary>电机码 - 按型号固定信息（仅内容，不含片段代号）</summary>
    public class MotorCodeFixedSegmentViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDataCacheService _cache; // 全局配置缓存
        private readonly IRepository<craft_MotorCodeFixedSegment> _repo;
        private readonly IMotorCodeCacheService _motorCodeCache;

        private List<craft_TypeInfo> _types = new();
        private craft_TypeInfo? _selectedType;

        public MotorCodeFixedSegmentViewModel(
            IDataCacheService cache,
            IRepository<craft_MotorCodeFixedSegment> repo,
            IMotorCodeCacheService motorCodeCache)
        {
            _cache = cache;
            _repo = repo;
            _motorCodeCache = motorCodeCache;
            Rows = new ObservableCollection<MotorCodeFixedSegmentRow>();

            SaveCommand = new DelegateCommand(async () => await SaveAsync());
            RefreshCommand = new DelegateCommand(async () => await LoadAsync());
            AddRowCommand = new DelegateCommand(AddRow);
            DeleteRowCommand = new DelegateCommand<MotorCodeFixedSegmentRow>(DeleteRow);

            LoadTypes();
        }

        public static IReadOnlyList<MotorCodeFixedUsageOption> UsageOptionList { get; } =
        [
            new() { Value = (int)MotorCodeFixedSegmentUsage.General, Display = "普通" },
            new() { Value = (int)MotorCodeFixedSegmentUsage.Platform, Display = "平台代号" },
        ];

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
                    _ = LoadAsync();
            }
        }

        public ObservableCollection<MotorCodeFixedSegmentRow> Rows { get; }

        public IReadOnlyList<MotorCodeFixedUsageOption> UsageOptions => UsageOptionList;

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand AddRowCommand { get; }
        public DelegateCommand<MotorCodeFixedSegmentRow> DeleteRowCommand { get; }

        private void LoadTypes()
        {
            Types = _cache.GetData<List<craft_TypeInfo>>() ?? new List<craft_TypeInfo>();
            SelectedType = Types.FirstOrDefault();
        }

        private async Task LoadAsync()
        {
            if (SelectedType == null)
                return;

            Rows.Clear();
            var list = (await _repo.GetAllAsync())
                .Where(x => x.ProductTypeId == SelectedType.Id)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id);

            foreach (var item in list)
            {
                Rows.Add(new MotorCodeFixedSegmentRow
                {
                    Id = item.Id,
                    FixedValue = item.FixedValue,
                    UsageType = item.UsageType
                });
            }
        }

        private void AddRow()
        {
            Rows.Add(new MotorCodeFixedSegmentRow { FixedValue = string.Empty, UsageType = (int)MotorCodeFixedSegmentUsage.General });
        }

        private void DeleteRow(MotorCodeFixedSegmentRow? row)
        {
            if (row != null)
                Rows.Remove(row);
        }

        private async Task SaveAsync()
        {
            if (SelectedType == null)
                return;

            if (Rows.Any(r => string.IsNullOrWhiteSpace(r.FixedValue)))
            {
                MessageBox.Show("固定信息不能为空", "校验");
                return;
            }

            if (Rows.Count(r => r.UsageType == (int)MotorCodeFixedSegmentUsage.Platform) > 1)
            {
                MessageBox.Show("每个型号最多只能有一条「平台代号」固定信息", "校验");
                return;
            }

            var typeId = SelectedType.Id;
            var existing = (await _repo.GetAllAsync())
                .Where(x => x.ProductTypeId == typeId)
                .ToList();
            var existingById = existing.ToDictionary(e => e.Id);
            var keptIds = new HashSet<int>();

            var order = 0;
            foreach (var row in Rows)
            {
                var usage = row.UsageType == (int)MotorCodeFixedSegmentUsage.Platform
                    ? (int)MotorCodeFixedSegmentUsage.Platform
                    : (int)MotorCodeFixedSegmentUsage.General;

                if (row.Id > 0 && existingById.TryGetValue(row.Id, out var entity))
                {
                    entity.FixedValue = row.FixedValue.Trim();
                    entity.UsageType = usage;
                    entity.SortOrder = order;
                    if (string.IsNullOrWhiteSpace(entity.SegmentCode))
                        entity.SegmentCode = $"F{entity.Id:D4}";
                    await _repo.UpdateAsync(entity);
                    keptIds.Add(entity.Id);
                }
                else
                {
                    var tempCode = $"T{order:D2}{DateTime.UtcNow.Ticks % 10000000:D7}";
                    if (tempCode.Length > 10)
                        tempCode = tempCode[..10];

                    var newId = await _repo.InsertAsync(new craft_MotorCodeFixedSegment
                    {
                        ProductTypeId = typeId,
                        SegmentCode = tempCode,
                        FixedValue = row.FixedValue.Trim(),
                        UsageType = usage,
                        SortOrder = order
                    });

                    var newEntity = await _repo.GetByIdAsync(newId);
                    if (newEntity != null)
                    {
                        newEntity.SegmentCode = $"F{newId:D4}";
                        await _repo.UpdateAsync(newEntity);
                    }

                    row.Id = newId;
                    keptIds.Add(newId);
                }

                order++;
            }

            foreach (var e in existing.Where(e => !keptIds.Contains(e.Id)))
                await _repo.DeleteAsync(e.Id);

            await _motorCodeCache.RefreshAsync();
            MessageBox.Show("固定信息已保存", "提示");
        }

        #endregion
    }
}
