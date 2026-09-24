using Prism.Events;
using Prism.Services.Dialogs;
using System.Windows;
using ProductionLineManage.Core.Events;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using ProductionLineManage.Core.Models.DataBase;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>
    /// 工艺-工艺流程视图模型类
    /// </summary>
    public class ProcessFlowViewModel : BindableBase
    {

        #region ============================== 属性 命令 字段 ==============================

        #region 公共属性

        //界面显示，通过型号、产线筛选
        public ObservableCollection<ProcessFlowItem> DisplayProcess { get; set; } = new ObservableCollection<ProcessFlowItem>();
        //型号下拉框
        public List<craft_TypeInfo> Types
        {
            get => _types;
            set
            {
                SetProperty(ref _types, value);
                //给型号字典赋值,id为key
                _typeDict = _types.ToDictionary(t => t.Id);
            }
        }
        //产线下拉框
        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set
            {
                SetProperty(ref _lines, value);
                //给产线字典赋值,id为key
                _lineDict = value.ToDictionary(l => l.Id);
            }
        }

        //加载动画是否可见
        public bool LoadingVisbility
        {
            get { return _loadingVisbility; }
            set { SetProperty(ref _loadingVisbility, value); }
        }
        //型号下拉框选择索引
        public int TypeSelectedIndex
        {
            get { return _typeSelectedIndex; }
            set { SetProperty(ref _typeSelectedIndex, value); }
        }
        //产线下拉框选择索引
        public int LineSelectedIndex
        {
            get { return _lineSelectedIndex; }
            set { SetProperty(ref _lineSelectedIndex, value); }
        }
        //型号下拉框选择项
        public craft_TypeInfo TypeSelectedItem
        {
            get { return _typeSelectedItem; }
            set
            {
                _typeSelectedItem = value;
                //筛选、转换并更新界面
                _ = FilterProcess();
            }
        }
        //产线下拉框选择项
        public craft_LineInfo LineSelectedItem
        {
            get { return _lineSelectedItem; }
            set
            {
                _lineSelectedItem = value;
                //筛选、转换并更新界面
                _ = FilterProcess();
            }
        }
        //防止重复刷新
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }
        #endregion

        #region 命令
        //新建
        public DelegateCommand NewAddCommand { get; set; }
        //编辑
        public DelegateCommand<ProcessFlowItem> EditCommand { get; set; }
        //删除
        public DelegateCommand<ProcessFlowItem> DeleteCommand { get; set; }
        //刷新
        public DelegateCommand RefreshCommand { get; set; }
        //复制
        public DelegateCommand CopyCommand { get; set; }
        #endregion

        #region 私有字段
        //数据库表模型，存放所有工艺流程
        private List<craft_ProcessInfo> _allProcess = new List<craft_ProcessInfo>();

        //使用 Dictionary 加速查找 缓存查找用的 Dictionary
        //型号    
        private Dictionary<int, craft_TypeInfo> _typeDict = new Dictionary<int, craft_TypeInfo>();
        //产线
        private Dictionary<int, craft_LineInfo> _lineDict = new Dictionary<int, craft_LineInfo>();
        //工位
        private Dictionary<int, craft_StationInfo> _stationDict = new Dictionary<int, craft_StationInfo>();
        //加载动画是否可见
        private bool _loadingVisbility = false;
        //型号下拉框选择索引
        private int _typeSelectedIndex;
        //产线下拉框选择索引
        private int _lineSelectedIndex;
        //型号下拉框选择项
        private craft_TypeInfo _typeSelectedItem = new craft_TypeInfo();
        //产线下拉框选择项
        private craft_LineInfo _lineSelectedItem = new craft_LineInfo();
        //是否为自己发布
        //private bool isIPublish = false;
        //防止重复刷新
        private bool _isRefreshing = false;
        //弹窗服务
        private IDialogService _dialogService;
        //事件聚合器
        private IEventAggregator _eventAggregator;
        //缓存服务
        private IDataCacheService _cacheService;
        //数据库操作单例
        private IRepository<craft_ProcessInfo> _repo;
        //加载动画服务
        private ILoadingService _loadingService;
        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        private List<craft_LineInfo> _lines = new List<craft_LineInfo>();
        private List<craft_StationInfo> _stations = new List<craft_StationInfo>();

        #endregion

        #endregion ----------------------------------

        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="dialogService"></param>
        /// <param name="eventAggregator"></param>
        /// <param name="cacheService"></param>
        /// <param name="repo"></param>
        public ProcessFlowViewModel(IDialogService dialogService, IEventAggregator eventAggregator
            , IDataCacheService cacheService, IRepository<craft_ProcessInfo> repo, ILoadingService loadingService)
        {
            NewAddCommand = new DelegateCommand(NewAdd);
            EditCommand = new DelegateCommand<ProcessFlowItem>(Edit);
            DeleteCommand = new DelegateCommand<ProcessFlowItem>(Delete);
            RefreshCommand = new DelegateCommand(Refresh);
            CopyCommand = new DelegateCommand(Copy);
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _repo = repo;
            _loadingService = loadingService;
            //初始化数据，订阅工位、型号、产线信息
            InitializationData();

        }

        #region ============================== 初始化数据 ==============================

        /// <summary>
        /// 初始化数据 获取缓存型号、产线、工艺流程信息，订阅型号、产线、工位信息
        /// </summary>
        private void InitializationData()
        {
            //型号缓存数据
            if (_cacheService.HasData<List<craft_TypeInfo>>())
            {
                //获取型号缓存数据
                Types = _cacheService.GetData<List<craft_TypeInfo>>();
                TypeSelectedIndex = -1;
            }
            //获取产线缓存数据
            if (_cacheService.HasData<List<craft_LineInfo>>())
            {
                //获取产线缓存数据
                Lines = _cacheService.GetData<List<craft_LineInfo>>();
                LineSelectedIndex = -1;
            }
            //获取工位缓存数据
            if (_cacheService.HasData<List<craft_StationInfo>>())
            {
                //获取工位缓存数据
                _stations = _cacheService.GetData<List<craft_StationInfo>>();
                _stationDict = _stations.ToDictionary(s => s.Id);
            }
            //获取工艺流程缓存数据
            if (_cacheService.HasData<List<craft_ProcessInfo>>())
            {
                //获取工艺流程缓存数据
                _allProcess = _cacheService.GetData<List<craft_ProcessInfo>>();
            }

            //订阅型号
            _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>()
                .Subscribe(OnOrderTypeUpdated, ThreadOption.UIThread);
            //订阅产线
            _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>()
                .Subscribe(OnOrderLineUpdated, ThreadOption.UIThread);
            //订阅工位
            _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>()
                .Subscribe(OnOrderStationUpdated, ThreadOption.UIThread);
        }

        #endregion ----------------------------------

        #region ============================== 事件聚合器订阅接收方法 ==============================
        /// <summary>
        /// 型号
        /// </summary>
        /// <param name="list"></param>
        private void OnOrderTypeUpdated(List<craft_TypeInfo> list)
        {
            Types = list;
            TypeSelectedIndex = -1;
        }
        /// <summary>
        /// 产线
        /// </summary>
        /// <param name="list"></param>
        private void OnOrderLineUpdated(List<craft_LineInfo> list)
        {
            Lines = list;
            LineSelectedIndex = -1;
        }
        /// <summary>
        /// 工位
        /// </summary>
        /// <param name="list"></param>
        private async void OnOrderStationUpdated(List<craft_StationInfo> list)
        {
            try
            {
                _stations = list;
                _stationDict = list.ToDictionary(s => s.Id);
                //重新加载界面数据，工位在listView中需要重新加载刷新
                await FilterProcess();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"在接收工位信息后更新工位数据失败：{ex.Message}", "错误");
            }
        }
        #endregion ----------------------------------

        #region ============================== 工艺流程增删改查操作 ==============================

        //刷新按钮指令 重新加载数据
        private async void Refresh()
        {
            //  防重复刷新
            if (IsRefreshing) return;
            IsRefreshing = true;
            try
            {
                LoadingVisbility = true;
                // 从数据库重新加载所有工艺流程
                _allProcess = (await _repo.GetAllAsync()).ToList();
                // 根据当前筛选条件刷新显示
                await FilterProcess();
                // 发布更新
                //isIPublish = true;
                _eventAggregator.GetEvent<ProcessFlowInfoUpdatedEvent>().Publish(_allProcess);
                _cacheService.SetData(_allProcess);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"刷新失败：{ex.Message}", "错误");
            }
            finally
            {
                LoadingVisbility = false;
                IsRefreshing = false;
            }
        }

        //删除按钮
        private async void Delete(ProcessFlowItem deleteRow)
        {
            if (HandyControl.Controls.MessageBox.Show("确认删除该工艺流程", "提示",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                //从数据库删除
                await _repo.DeleteAsync(deleteRow.Id);
                //更新原数据
                _allProcess.RemoveAt(_allProcess.FindIndex(p => p.Id == deleteRow.Id));
                //删除所选择的行
                DisplayProcess.Remove(deleteRow);
                //声明自己发布
                //isIPublish = true;
                //发布到事件聚合器
                _eventAggregator.GetEvent<ProcessFlowInfoUpdatedEvent>().Publish(_allProcess);
                //更新缓存的发布数据
                _cacheService.SetData(_allProcess);
                HandyControl.Controls.MessageBox.Show("删除成功");
            }
        }

        //编辑工艺流程按钮
        private void Edit(ProcessFlowItem editRow)
        {
            //传入参数
            var parameters = new DialogParameters();
            //工艺流程信息
            parameters.Add("Process", editRow);
            //所有产线
            parameters.Add("Lines", Lines);
            //所有型号
            parameters.Add("Types", Types);
            //所有工位
            parameters.Add("Stations", _stations);
            //传入已存在的工艺流程，编辑窗口内写逻辑：防止修改为已存在的工艺流程工位相同的冲突
            parameters.Add("AllProcess", _allProcess);

            //打开弹窗
            _dialogService.ShowDialog("EditProcessView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    //获取回调参数
                    var returnInfo = result.Parameters.GetValue<craft_ProcessInfo>("Process");
                    //更新原数据
                    _allProcess[_allProcess.IndexOf(editRow.RawData)] = returnInfo;
                    //重新加载数据
                    await FilterProcess();
                    //声明自己发布
                    //isIPublish = true;
                    //发布到事件聚合器
                    _eventAggregator.GetEvent<ProcessFlowInfoUpdatedEvent>().
                       Publish(_allProcess);
                    //更新缓存的发布数据
                    _cacheService.SetData(_allProcess);
                }
            });
        }

        //新增工艺流程按钮
        private void NewAdd()
        {
            //传入参数 型号、产线、工位
            var parameters = new DialogParameters();
            parameters.Add("Types", Types);
            parameters.Add("Lines", Lines);
            parameters.Add("Stations", _stations);
            parameters.Add("AllProcess", _allProcess);
            //打开弹窗
            _dialogService.ShowDialog("NewAddProcessView", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    //获取弹窗回调写入的参数
                    var returnInfo = result.Parameters.GetValue<craft_ProcessInfo>("Process");
                    //添加到所有工艺流程集合中
                    _allProcess.Add(returnInfo);
                    //根据筛选条件加载到界面
                    await FilterProcess();
                    //声明自己发布
                    //isIPublish = true;
                    //发布到事件聚合器
                    _eventAggregator.GetEvent<ProcessFlowInfoUpdatedEvent>().
                       Publish(_allProcess);
                    //更新缓存的发布数据
                    _cacheService.SetData(_allProcess);

                }
            });
        }

        //复制
        private void Copy()
        {

        }

        #endregion ----------------------------------

        #region ============================== 筛选和转换 ==============================
        //筛选
        private async Task FilterProcess()
        {
            //如果工艺流程为空 直接退出
            if (_allProcess == null)
                return;
            //显示加载动画
            LoadingVisbility = true;
            //清空视图集合 
            DisplayProcess.Clear();
            try
            {
                var items = new List<craft_ProcessInfo>();
                //如果选择了下拉框
                if (TypeSelectedIndex != -1)
                {
                    //筛选型号
                    items = _allProcess.Where(p => p.TypeId == TypeSelectedItem.Id).ToList();
                }
                //如果选择了下拉框
                if (LineSelectedIndex != -1)
                {
                    //筛选产线
                    items = items.Where(l => l.LineId == LineSelectedItem.Id).ToList();
                }
                //转换类型，显示到界面
                if (items.Count > 0) await DataConvert(items);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show("工艺流程筛选异常：" + ex.Message);
            }
            finally
            {
                LoadingVisbility = false;
            }
        }

        //将原数据转换为视图模型,筛选完后调用
        private async Task DataConvert(List<craft_ProcessInfo> items)
        {
            // 按 Sequence 排序
            items = items.OrderBy(p => p.Sequence).ToList();
            try
            {
                await _loadingService.ExecuteAsync(async () =>
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        DisplayProcess.Clear();
                        foreach (var item in items)
                        {
                            DisplayProcess.Add(new ProcessFlowItem()
                            {
                                RawData = item,
                                TypeInfo = _typeDict.GetValueOrDefault(item.TypeId),
                                LineInfo = _lineDict.GetValueOrDefault(item.LineId),
                                StationInfo = _stationDict.GetValueOrDefault(item.StationId),
                                UpperWorkstation = _stationDict.GetValueOrDefault(item.UpperWorkstationId),
                            });
                        }
                    });
                }, "正在加载工艺流程...");
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show("工艺流程数据转换异常：" + ex.Message);
            }
            finally
            {
                LoadingVisbility = false;
            }
        }

        #endregion ----------------------------------

    }

    /// <summary>
    /// 界面ListView显示的视图模型
    /// </summary>
    public class ProcessFlowItem : BindableBase
    {
        // 原始数据
        public craft_ProcessInfo RawData { get; set; } = new craft_ProcessInfo();
        // 关联数据
        public craft_TypeInfo? TypeInfo { get; set; } = new craft_TypeInfo();
        public craft_LineInfo? LineInfo { get; set; } = new craft_LineInfo();
        public craft_StationInfo? StationInfo { get; set; } = new craft_StationInfo();
        public craft_StationInfo? UpperWorkstation { get; set; } = new craft_StationInfo();
        // 直接暴露需要的属性，简化 XAML 绑定
        public int Id => RawData.Id;
        public string StationDisplayText => StationInfo?.DisplayText ?? "未知";
        public string UpperStationDisplayText => UpperWorkstation?.DisplayText ?? "无";
        public bool IsRepeatWork
        {
            get => RawData.IsRepeatWork;
            set => RawData.IsRepeatWork = value;
        }
        public bool IsRepairStation
        {
            get => RawData.IsRepairStation;
            set => RawData.IsRepairStation = value;
        }
        public int Sequence
        {
            get => RawData.Sequence;
            set => RawData.Sequence = value;
        }
        public bool IsEnable
        {
            get => RawData.IsEnable;
            set => RawData.IsEnable = value;
        }
        public DateTime? CreateTime => RawData.CreateTime;
        public DateTime? UpdateTime => RawData.UpdateTime;
        public string Remarks => RawData.Remarks;

    }
}
