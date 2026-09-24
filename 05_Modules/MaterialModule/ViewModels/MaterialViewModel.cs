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
    /// 物料 - 物料管理视图模型
    /// </summary>
    public class MaterialViewModel : BindableBase
    {
        #region ============================== 私有字段 ==============================

        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataCacheService _cacheService;
        private readonly IRepository<material_Info> _repo;
        private readonly ILoadingService _loadingService;

        private List<material_Info> _allMaterials = new List<material_Info>();
        private Dictionary<int, craft_TypeInfo> _typeDict = new Dictionary<int, craft_TypeInfo>();

        private ObservableCollection<MaterialDisplayItem> _materials = new ObservableCollection<MaterialDisplayItem>();
        private bool _isRefreshing;
        private bool _isIPublish;

        // 型号筛选相关
        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        private int _typeSelectedIndex = -1;
        private craft_TypeInfo _typeSelectedItem = new craft_TypeInfo();

        #endregion

        #region ============================== 公共属性 ==============================

        /// <summary>
        /// 物料显示集合（用于界面绑定）
        /// </summary>
        public ObservableCollection<MaterialDisplayItem> Materials
        {
            get => _materials;
            set => SetProperty(ref _materials, value);
        }

        /// <summary>
        /// 型号列表（用于筛选下拉框）
        /// </summary>
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

        /// <summary>
        /// 型号筛选下拉框选中索引
        /// </summary>
        public int TypeSelectedIndex
        {
            get => _typeSelectedIndex;
            set => SetProperty(ref _typeSelectedIndex, value);
        }

        /// <summary>
        /// 型号筛选下拉框选中项
        /// </summary>
        public craft_TypeInfo TypeSelectedItem
        {
            get => _typeSelectedItem;
            set
            {
                if (value == null) return;
                _typeSelectedItem = value;
                _typeSelectedIndex = Types.FindIndex(t => t.Id == value.Id);
                _ = FilterMaterialsAsync();
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 是否正在刷新
        /// </summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        #endregion

        #region ============================== 命令 ==============================

        public DelegateCommand AddCommand { get; set; }
        public DelegateCommand<MaterialDisplayItem> EditCommand { get; set; }
        public DelegateCommand<MaterialDisplayItem> DeleteCommand { get; set; }
        public DelegateCommand RefreshCommand { get; set; }

        #endregion

        #region ============================== 构造函数 ==============================

        public MaterialViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<material_Info> repo,
            ILoadingService loadingService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _repo = repo;
            _loadingService = loadingService;

            // 初始化命令
            AddCommand = new DelegateCommand(OnAdd);
            EditCommand = new DelegateCommand<MaterialDisplayItem>(OnEdit);
            DeleteCommand = new DelegateCommand<MaterialDisplayItem>(OnDelete);
            RefreshCommand = new DelegateCommand(OnRefresh);

            // 初始化数据
            InitializeData();
        }

        #endregion

        #region ============================== 初始化 ==============================

        private void InitializeData()
        {
            // 加载型号缓存
            if (_cacheService.HasData<List<craft_TypeInfo>>())
            {
                Types = _cacheService.GetData<List<craft_TypeInfo>>();
            }

            // 加载物料缓存
            if (_cacheService.HasData<List<material_Info>>())
            {
                _allMaterials = _cacheService.GetData<List<material_Info>>();
                _ = DisplayMaterialsAsync();
            }

            // 订阅物料更新事件
            _eventAggregator.GetEvent<MaterialInfoUpdatedEvent>()
                .Subscribe(OnMaterialsUpdated, ThreadOption.UIThread);

            // 订阅型号更新事件（当型号变更时刷新下拉框）
            _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>()
                .Subscribe(OnTypesUpdated, ThreadOption.UIThread);
        }

        #endregion

        #region ============================== 事件订阅回调 ==============================

        private void OnMaterialsUpdated(List<material_Info> materials)
        {
            if (_isIPublish)
            {
                _isIPublish = false;
                return;
            }
            _allMaterials = materials;
            _ = DisplayMaterialsAsync();
        }

        private void OnTypesUpdated(List<craft_TypeInfo> types)
        {
            Types = types;
        }

        #endregion

        #region ============================== 数据筛选与显示 ==============================

        /// <summary>
        /// 筛选并显示物料（按型号）
        /// </summary>
        private async Task FilterMaterialsAsync()
        {
            await _loadingService.ExecuteAsync(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (TypeSelectedItem?.Id == 0)
                    {
                        Materials.Clear();
                        return;
                    }

                    var filtered = _allMaterials;

                    // 按型号筛选
                    if (TypeSelectedItem?.Id > 0)
                    {
                        filtered = filtered.Where(m => m.TypeId == TypeSelectedItem.Id).ToList();
                    }

                    // 转换为显示模型
                    var displayItems = filtered.Select(m => new MaterialDisplayItem
                    {
                        RawData = m,
                        TypeName = _typeDict.GetValueOrDefault(m.TypeId)?.Name ?? "未知"
                    }).OrderBy(m => m.Id).ToList();

                    Materials.Clear();
                    foreach (var item in displayItems)
                    {
                        Materials.Add(item);
                    }
                });
            }, "正在加载物料数据...");
        }

        /// <summary>
        /// 显示所有物料
        /// </summary>
        private async Task DisplayMaterialsAsync()
        {
            await FilterMaterialsAsync();
        }

        #endregion

        #region ============================== 命令实现 ==============================

        /// <summary>
        /// 刷新
        /// </summary>
        private async void OnRefresh()
        {
            if (IsRefreshing) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    IsRefreshing = true;

                    // 从数据库重新加载
                    var freshData = await _repo.GetAllAsync();
                    _allMaterials = freshData.ToList();

                    // 刷新显示
                    await FilterMaterialsAsync();

                    // 发布更新
                    _isIPublish = true;
                    _eventAggregator.GetEvent<MaterialInfoUpdatedEvent>().Publish(_allMaterials);
                    _cacheService.SetData(_allMaterials);
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show($"刷新失败：{ex.Message}", "错误");
                }
                finally
                {
                    IsRefreshing = false;
                }
            }, "正在刷新物料数据...");
        }

        /// <summary>
        /// 新增物料
        /// </summary>
        private void OnAdd()
        {
            var parameters = new DialogParameters();
            parameters.Add("Types", _types);
            parameters.Add("AllMaterials", _allMaterials);

            _dialogService.ShowDialog("NewAddMaterialView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newMaterial = result.Parameters.GetValue<material_Info>("Material");
                    if (newMaterial != null)
                    {
                        _allMaterials.Add(newMaterial);
                        await FilterMaterialsAsync();

                        _isIPublish = true;
                        _eventAggregator.GetEvent<MaterialInfoUpdatedEvent>().Publish(_allMaterials);
                        _cacheService.SetData(_allMaterials);
                    }
                }
            });
        }

        /// <summary>
        /// 编辑物料
        /// </summary>
        private void OnEdit(MaterialDisplayItem item)
        {
            if (item == null) return;

            var parameters = new DialogParameters();
            parameters.Add("Material", item.RawData);
            parameters.Add("Types", _types);
            parameters.Add("AllMaterials", _allMaterials);

            _dialogService.ShowDialog("EditMaterialView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var updatedMaterial = result.Parameters.GetValue<material_Info>("Material");
                    if (updatedMaterial != null)
                    {
                        var index = _allMaterials.FindIndex(m => m.Id == updatedMaterial.Id);
                        if (index >= 0)
                        {
                            _allMaterials[index] = updatedMaterial;
                        }
                        await FilterMaterialsAsync();

                        _isIPublish = true;
                        _eventAggregator.GetEvent<MaterialInfoUpdatedEvent>().Publish(_allMaterials);
                        _cacheService.SetData(_allMaterials);
                    }
                }
            });
        }

        /// <summary>
        /// 删除物料
        /// </summary>
        private async void OnDelete(MaterialDisplayItem item)
        {
            if (item == null) return;

            var result = HandyControl.Controls.MessageBox.Show(
                $"确认删除物料 \"{item.RawData.Name}\"？",
                "提示",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await _loadingService.ExecuteAsync(async () =>
            {
                try
                {
                    await _repo.DeleteAsync(item.Id);

                    _allMaterials.Remove(item.RawData);
                    await FilterMaterialsAsync();

                    _isIPublish = true;
                    _eventAggregator.GetEvent<MaterialInfoUpdatedEvent>().Publish(_allMaterials);
                    _cacheService.SetData(_allMaterials);

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
    /// 物料显示模型（用于 ListView 绑定）
    /// </summary>
    public class MaterialDisplayItem : BindableBase
    {
        public material_Info RawData { get; set; } = new material_Info();

        // 直接暴露属性，简化 XAML 绑定
        public int Id => RawData.Id;
        public string TypeName { get; set; } = string.Empty;
        public string Code => RawData.Code;
        public string Name => RawData.Name;
        public string Remarks => RawData.Remarks;
        public DateTime? CreateTime => RawData.CreateTime;
        public DateTime? UpdateTime => RawData.UpdateTime;
    }
}
