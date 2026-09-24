using ProductionLineManage.Core.Enums;
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
    /// 物料 - 条码规则管理视图模型：维护物料扫码/绑定规则（列表展示 + 弹窗新增/编辑）。
    /// </summary>
    public class CodeRulesViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataCacheService _cacheService;
        private readonly IRepository<material_CodeRules> _repo;
        private readonly ILoadingService _loadingService;

        private List<material_CodeRules> _allCodeRules = new List<material_CodeRules>();
        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        private List<material_Info> _allMaterials = new List<material_Info>();
        private List<material_Info> _materials = new List<material_Info>();

        private Dictionary<int, craft_TypeInfo> _typeDict = new Dictionary<int, craft_TypeInfo>();
        private Dictionary<int, material_Info> _materialDict = new Dictionary<int, material_Info>();

        private int _typeSelectedIndex = -1;
        private craft_TypeInfo _typeSelectedItem = new craft_TypeInfo();
        private int _materialSelectedIndex = -1;
        private material_Info _materialSelectedItem = new material_Info();
        private bool _isMaterialEnabled = false;

        private ObservableCollection<CodeRuleDisplayItem> _codeRules = new ObservableCollection<CodeRuleDisplayItem>();
        private bool _isRefreshing;
        private bool _isIPublish;

        #endregion

        #region ===================== 公共属性 =====================

        public ObservableCollection<CodeRuleDisplayItem> CodeRules
        {
            get => _codeRules;
            set => SetProperty(ref _codeRules, value);
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

        public List<material_Info> Materials
        {
            get => _materials;
            set => SetProperty(ref _materials, value);
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
                FilterMaterialsByType(value.Id);
                _ = FilterDataAsync();
                RaisePropertyChanged();
            }
        }

        public int MaterialSelectedIndex
        {
            get => _materialSelectedIndex;
            set => SetProperty(ref _materialSelectedIndex, value);
        }

        public material_Info MaterialSelectedItem
        {
            get => _materialSelectedItem;
            set
            {
                if (value == null) return;
                _materialSelectedItem = value;
                MaterialSelectedIndex = Materials.FindIndex(m => m.Id == value.Id);
                _ = FilterDataAsync();
                RaisePropertyChanged();
            }
        }

        public bool IsMaterialEnabled
        {
            get => _isMaterialEnabled;
            set => SetProperty(ref _isMaterialEnabled, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        #endregion

        #region ===================== 命令 =====================

        public DelegateCommand AddCommand { get; set; }
        public DelegateCommand<CodeRuleDisplayItem> EditCommand { get; set; }
        public DelegateCommand<CodeRuleDisplayItem> DeleteCommand { get; set; }
        public DelegateCommand RefreshCommand { get; set; }

        #endregion

        #region ===================== 构造 =====================

        public CodeRulesViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<material_CodeRules> repo,
            ILoadingService loadingService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _repo = repo;
            _loadingService = loadingService;

            AddCommand = new DelegateCommand(OnAdd);
            EditCommand = new DelegateCommand<CodeRuleDisplayItem>(OnEdit);
            DeleteCommand = new DelegateCommand<CodeRuleDisplayItem>(OnDelete);
            RefreshCommand = new DelegateCommand(OnRefresh);

            InitializeData();
        }

        #endregion

        #region ===================== 初始化 =====================

        private void InitializeData()
        {
            if (_cacheService.HasData<List<craft_TypeInfo>>())
            {
                Types = _cacheService.GetData<List<craft_TypeInfo>>();
            }

            if (_cacheService.HasData<List<material_Info>>())
            {
                _allMaterials = _cacheService.GetData<List<material_Info>>();
                _materialDict = _allMaterials.ToDictionary(m => m.Id);
            }

            if (_cacheService.HasData<List<material_CodeRules>>())
            {
                _allCodeRules = _cacheService.GetData<List<material_CodeRules>>();
                _ = DisplayDataAsync();
            }

            _eventAggregator.GetEvent<CodeRulesUpdatedEvent>()
                .Subscribe(OnCodeRulesUpdated, ThreadOption.UIThread);
            _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>()
                .Subscribe(OnTypesUpdated, ThreadOption.UIThread);
        }

        private void FilterMaterialsByType(int typeId)
        {
            if (typeId <= 0)
            {
                Materials = new List<material_Info>();
                IsMaterialEnabled = false;
                return;
            }

            var filtered = _allMaterials.Where(m => m.TypeId == typeId).ToList();
            Materials = filtered;
            IsMaterialEnabled = filtered.Any();
            MaterialSelectedIndex = -1;
            MaterialSelectedItem = new material_Info();
        }

        #endregion

        #region ===================== 事件回调 =====================

        private void OnCodeRulesUpdated(List<material_CodeRules> data)
        {
            if (_isIPublish)
            {
                _isIPublish = false;
                return;
            }
            _allCodeRules = data;
            _ = DisplayDataAsync();
        }

        private void OnTypesUpdated(List<craft_TypeInfo> types)
        {
            Types = types;
        }

        #endregion

        #region ===================== 数据筛选与显示 =====================

        private async Task FilterDataAsync()
        {
            await _loadingService.ExecuteAsync(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (TypeSelectedItem?.Id == 0 || MaterialSelectedItem?.Id == 0)
                    {
                        CodeRules.Clear();
                        return;
                    }
                    //筛选型号id和物料id
                    var filtered = _allCodeRules
                        .Where(r => r.TypeId == TypeSelectedItem?.Id && r.MaterialId == MaterialSelectedItem?.Id)
                        .ToList();

                    var ruleTypeNames = GetRuleTypeNames();

                    var displayItems = filtered.Select(r => new CodeRuleDisplayItem
                    {
                        RawData = r,
                        RuleTypeName = ruleTypeNames.ContainsKey(r.RuleType) ? ruleTypeNames[r.RuleType] : "未知",
                        StartBitDisplay = r.StartBit == -1 ? "-" : r.StartBit.ToString(),
                        LengthDisplay = r.Length == -1 ? "-" : r.Length.ToString()
                    }).OrderBy(r => r.GroupNo).ThenBy(r => r.RawData.StartBit).ToList();

                    CodeRules.Clear();
                    foreach (var item in displayItems)
                    {
                        CodeRules.Add(item);
                    }
                });
            }, "正在加载编码规则数据...");
        }

        private Dictionary<int, string> GetRuleTypeNames()
        {
            return new Dictionary<int, string>
            {
                { (int)CodeRuleType.Left, "左侧内容验证" },
                { (int)CodeRuleType.Right, "右侧内容验证" },
                { (int)CodeRuleType.Middle, "中间内容验证" },
                { (int)CodeRuleType.Year, "年代码验证" },
                { (int)CodeRuleType.Month, "月代码验证" },
                { (int)CodeRuleType.Day, "日代码验证" },
                { (int)CodeRuleType.TotalLength, "总长度验证" }
            };
        }

        private async Task DisplayDataAsync()
        {
            await FilterDataAsync();
        }

        #endregion

        #region ===================== 命令实现 =====================

        private async void OnRefresh()
        {
            if (IsRefreshing) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    IsRefreshing = true;
                    var freshData = await _repo.GetAllAsync();
                    _allCodeRules = freshData.ToList();
                    await DisplayDataAsync();

                    _isIPublish = true;
                    _eventAggregator.GetEvent<CodeRulesUpdatedEvent>().Publish(_allCodeRules);
                    _cacheService.SetData(_allCodeRules);
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
            parameters.Add("Types", _types);           // 所有型号
            parameters.Add("Materials", _allMaterials); // 所有物料
            parameters.Add("AllCodeRules", _allCodeRules);

            _dialogService.ShowDialog("AddCodeRuleView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newRule = result.Parameters.GetValue<material_CodeRules>("CodeRule");
                    if (newRule != null)
                    {
                        _allCodeRules.Add(newRule);
                        await DisplayDataAsync();

                        _isIPublish = true;
                        _eventAggregator.GetEvent<CodeRulesUpdatedEvent>().Publish(_allCodeRules);
                        _cacheService.SetData(_allCodeRules);
                    }
                }
            });
        }

        private void OnEdit(CodeRuleDisplayItem item)
        {
            if (item == null) return;

            var parameters = new DialogParameters();
            parameters.Add("CodeRule", item.RawData);
            parameters.Add("AllCodeRules", _allCodeRules);

            _dialogService.ShowDialog("EditCodeRuleView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var updatedRule = result.Parameters.GetValue<material_CodeRules>("CodeRule");
                    if (updatedRule != null)
                    {
                        var index = _allCodeRules.FindIndex(r => r.Id == updatedRule.Id);
                        if (index >= 0)
                        {
                            _allCodeRules[index] = updatedRule;
                        }
                        await DisplayDataAsync();

                        _isIPublish = true;
                        _eventAggregator.GetEvent<CodeRulesUpdatedEvent>().Publish(_allCodeRules);
                        _cacheService.SetData(_allCodeRules);
                    }
                }
            });
        }

        private async void OnDelete(CodeRuleDisplayItem item)
        {
            if (item == null) return;

            var result = HandyControl.Controls.MessageBox.Show(
                $"确认删除规则 \"{item.RuleName}\"？",
                "提示",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    await _repo.DeleteAsync(item.Id);

                    _allCodeRules.Remove(item.RawData);
                    await DisplayDataAsync();

                    _isIPublish = true;
                    _eventAggregator.GetEvent<CodeRulesUpdatedEvent>().Publish(_allCodeRules);
                    _cacheService.SetData(_allCodeRules);

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

    public class CodeRuleDisplayItem : BindableBase
    {
        public material_CodeRules RawData { get; set; } = new material_CodeRules();

        public int Id => RawData.Id;
        public int GroupNo => RawData.GroupNo;
        public string RuleTypeName { get; set; } = string.Empty;
        public string RuleName => RawData.RuleName;
        public string StartBitDisplay { get; set; } = string.Empty;
        public string LengthDisplay { get; set; } = string.Empty;
        public string RuleContent => RawData.RuleContent;
        public bool IsActive
        {
            get => RawData.IsEnabled;
            set => RawData.IsEnabled = value;
        }
        public string Remarks => RawData.Remarks;
    }
}
