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
    /// <summary>日期对照行（UI 绑定）</summary>
    public class MotorCodeDateMapRow : BindableBase
    {
        public int MapKey { get; set; }
        private string _mapCode = string.Empty;

        public string MapCode
        {
            get => _mapCode;
            set => SetProperty(ref _mapCode, value);
        }
    }

    /// <summary>电机码 - 按型号日期代号对照</summary>
    public class MotorCodeDateMapViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDataCacheService _cache; // 全局配置缓存
        private readonly IRepository<craft_MotorCodeDateMap> _repo;
        private readonly IMotorCodeCacheService _motorCodeCache;

        private List<craft_TypeInfo> _types = new();
        private craft_TypeInfo? _selectedType;
        private int _selectedTabIndex;
        private string _newYearInput = string.Empty;

        public MotorCodeDateMapViewModel(
            IDataCacheService cache,
            IRepository<craft_MotorCodeDateMap> repo,
            IMotorCodeCacheService motorCodeCache)
        {
            _cache = cache;
            _repo = repo;
            _motorCodeCache = motorCodeCache;

            MonthRows = new ObservableCollection<MotorCodeDateMapRow>(
                Enumerable.Range(1, 12).Select(i => new MotorCodeDateMapRow { MapKey = i }));
            DayRows = new ObservableCollection<MotorCodeDateMapRow>(
                Enumerable.Range(1, 31).Select(i => new MotorCodeDateMapRow { MapKey = i }));
            YearRows = new ObservableCollection<MotorCodeDateMapRow>();

            SaveCommand = new DelegateCommand(async () => await SaveAsync());
            RefreshCommand = new DelegateCommand(async () => await LoadAsync());
            AddYearCommand = new DelegateCommand(AddYearRow);
            AddCurrentYearCommand = new DelegateCommand(AddCurrentYearRow);
            DeleteYearCommand = new DelegateCommand<MotorCodeDateMapRow>(DeleteYearRow);

            LoadTypes();
        }

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

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public string NewYearInput
        {
            get => _newYearInput;
            set => SetProperty(ref _newYearInput, value);
        }

        public ObservableCollection<MotorCodeDateMapRow> MonthRows { get; }
        public ObservableCollection<MotorCodeDateMapRow> DayRows { get; }
        public ObservableCollection<MotorCodeDateMapRow> YearRows { get; }

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand AddYearCommand { get; }
        public DelegateCommand AddCurrentYearCommand { get; }
        public DelegateCommand<MotorCodeDateMapRow> DeleteYearCommand { get; }

        private void LoadTypes()
        {
            Types = _cache.GetData<List<craft_TypeInfo>>() ?? new List<craft_TypeInfo>();
            SelectedType = Types.FirstOrDefault();
        }

        private async Task LoadAsync()
        {
            if (SelectedType == null)
                return;

            var all = (await _repo.GetAllAsync())
                .Where(m => m.ProductTypeId == SelectedType.Id)
                .ToList();

            ApplyRows(MonthRows, all, MotorCodeMapType.Month);
            ApplyRows(DayRows, all, MotorCodeMapType.Day);

            YearRows.Clear();
            foreach (var y in all.Where(m => m.MapType == (int)MotorCodeMapType.Year).OrderBy(m => m.MapKey))
            {
                YearRows.Add(new MotorCodeDateMapRow { MapKey = y.MapKey, MapCode = y.MapCode });
            }
        }

        private static void ApplyRows(
            ObservableCollection<MotorCodeDateMapRow> rows,
            List<craft_MotorCodeDateMap> all,
            MotorCodeMapType type)
        {
            var dict = all.Where(m => m.MapType == (int)type)
                .ToDictionary(m => m.MapKey, m => m.MapCode);

            foreach (var row in rows)
                row.MapCode = dict.TryGetValue(row.MapKey, out var code) ? code : string.Empty;
        }

        private void AddYearRow()
        {
            if (!int.TryParse(NewYearInput?.Trim(), out var year) || year < 1900 || year > 9999)
            {
                MessageBox.Show("请输入有效年份（1900~9999）", "校验");
                return;
            }

            if (YearRows.Any(r => r.MapKey == year))
            {
                MessageBox.Show($"年份 {year} 已存在", "提示");
                return;
            }

            YearRows.Add(new MotorCodeDateMapRow { MapKey = year, MapCode = string.Empty });
            var sorted = YearRows.OrderBy(r => r.MapKey).ToList();
            YearRows.Clear();
            foreach (var r in sorted)
                YearRows.Add(r);
            NewYearInput = string.Empty;
        }

        private void AddCurrentYearRow()
        {
            NewYearInput = DateTime.Now.Year.ToString();
            AddYearRow();
        }

        private void DeleteYearRow(MotorCodeDateMapRow? row)
        {
            if (row != null)
                YearRows.Remove(row);
        }

        private async Task SaveAsync()
        {
            if (SelectedType == null)
                return;

            if (MonthRows.Any(r => string.IsNullOrWhiteSpace(r.MapCode)) ||
                DayRows.Any(r => string.IsNullOrWhiteSpace(r.MapCode)))
            {
                MessageBox.Show("月份 1~12 与日 1~31 的代号必须全部填写", "校验");
                return;
            }

            if (YearRows.GroupBy(r => r.MapKey).Any(g => g.Count() > 1))
            {
                MessageBox.Show("年份不能重复", "校验");
                return;
            }

            var typeId = SelectedType.Id;
            var existing = (await _repo.GetAllAsync()).Where(m => m.ProductTypeId == typeId).ToList();
            foreach (var e in existing)
                await _repo.DeleteAsync(e.Id);

            async Task InsertMap(MotorCodeMapType mapType, int key, string code)
            {
                await _repo.InsertAsync(new craft_MotorCodeDateMap
                {
                    ProductTypeId = typeId,
                    MapType = (int)mapType,
                    MapKey = key,
                    MapCode = code.Trim()
                });
            }

            foreach (var r in MonthRows)
                await InsertMap(MotorCodeMapType.Month, r.MapKey, r.MapCode);

            foreach (var r in DayRows)
                await InsertMap(MotorCodeMapType.Day, r.MapKey, r.MapCode);

            foreach (var r in YearRows.Where(r => !string.IsNullOrWhiteSpace(r.MapCode)))
                await InsertMap(MotorCodeMapType.Year, r.MapKey, r.MapCode);

            await _motorCodeCache.RefreshAsync();
            MessageBox.Show("日期代号已保存", "提示");
        }

        #endregion
    }
}
