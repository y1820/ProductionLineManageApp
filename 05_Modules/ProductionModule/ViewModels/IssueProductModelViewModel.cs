using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ProductionModule.ViewModels
{
    /// <summary>
    /// 生产 - 型号下发视图模型：从缓存选择型号并发布 IssueModelEvent 通知设备下发。
    /// </summary>
    public class IssueProductModelViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IEventAggregator _eventAggregator; // 事件总线，发布下发型号事件
        private readonly IDataCacheService _cacheService; // 全局缓存，读写当前下发型号

        private List<craft_TypeInfo> _types = new(); // 可选型号列表
        private craft_TypeInfo? _selectedType; // 当前选中型号
        private string _currentIssueSummary = "未下发"; // 已下发型号摘要文本

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入事件与缓存服务，初始化命令并订阅型号更新 </summary>
        public IssueProductModelViewModel(IEventAggregator eventAggregator, IDataCacheService cacheService)
        {
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;

            IssueCommand = new DelegateCommand(Issue, CanIssue)
                .ObservesProperty(() => SelectedType); // 选中型号变化时刷新命令可用性

            LoadTypes(); // 从缓存加载型号列表
            RefreshCurrentIssueSummary(); // 显示当前已下发型号

            _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>()
                .Subscribe(OnTypesUpdated, ThreadOption.UIThread); // 型号配置变更时刷新列表
        }

        #endregion

        #region ===================== 公共属性 =====================

        /// <summary>可选型号列表</summary>
        public List<craft_TypeInfo> Types
        {
            get => _types;
            set => SetProperty(ref _types, value ?? new List<craft_TypeInfo>());
        }

        /// <summary>当前选中的型号</summary>
        public craft_TypeInfo? SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value))
                    IssueCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>当前已下发型号摘要</summary>
        public string CurrentIssueSummary
        {
            get => _currentIssueSummary;
            set => SetProperty(ref _currentIssueSummary, value);
        }

        /// <summary>下发型号</summary>
        public DelegateCommand IssueCommand { get; }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 从缓存加载型号列表 </summary>
        private void LoadTypes()
        {
            if (_cacheService.HasData<List<craft_TypeInfo>>())
                Types = _cacheService.GetData<List<craft_TypeInfo>>();

            SyncSelectedTypeFromCache();
        }

        /// <summary> 将下拉选中项与缓存中当前下发型号对齐 </summary>
        private void SyncSelectedTypeFromCache()
        {
            if (!_cacheService.HasData<craft_TypeInfo>() || Types.Count == 0)
                return;

            var current = _cacheService.GetData<craft_TypeInfo>();
            SelectedType = Types.FirstOrDefault(t => t.Id == current.Id) ?? current;
        }

        /// <summary> 刷新界面上的已下发型号摘要 </summary>
        private void RefreshCurrentIssueSummary()
        {
            if (!_cacheService.HasData<craft_TypeInfo>())
            {
                CurrentIssueSummary = "未下发";
                return;
            }

            var current = _cacheService.GetData<craft_TypeInfo>();
            CurrentIssueSummary = $"{current.Name}（代号 {current.IssueCode}）";
        }

        /// <summary> 型号配置更新事件回调 </summary>
        private void OnTypesUpdated(List<craft_TypeInfo> types)
        {
            Types = types ?? new List<craft_TypeInfo>();
            SyncSelectedTypeFromCache();
        }

        /// <summary> 下发命令是否可执行（需选中有效型号且配置了下发代号） </summary>
        private bool CanIssue()
        {
            return SelectedType != null && SelectedType.Id > 0 && SelectedType.IssueCode > 0;
        }

        /// <summary> 确认后将选中型号写入缓存并发布下发事件 </summary>
        private void Issue()
        {
            if (SelectedType == null || SelectedType.Id <= 0)
            {
                HandyControl.Controls.MessageBox.Show("请选择要下发的型号", "提示");
                return;
            }

            if (SelectedType.IssueCode <= 0)
            {
                HandyControl.Controls.MessageBox.Show("所选型号未配置下发型号代号，请先在型号管理中维护", "提示");
                return;
            }

            var confirm = HandyControl.Controls.MessageBox.Show(
                $"确认下发型号：{SelectedType.Name}（代号 {SelectedType.IssueCode}）？\n\n下发后将通知所有已连接且启用软件下发型号的设备。",
                "型号下发",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            _cacheService.SetData(SelectedType);
            _eventAggregator.GetEvent<IssueModelEvent>().Publish(SelectedType);
            RefreshCurrentIssueSummary();

            HandyControl.Controls.MessageBox.Show(
                $"型号已下发：{SelectedType.Name}（代号 {SelectedType.IssueCode}）",
                "提示");
        }

        #endregion
    }
}
