using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Windows;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>
    /// 工艺 - 产线视图模型
    /// </summary>
    public class ProductionLineViewModel : BindableBase
    {
        #region ============================== 属性和字段 ==============================

        /// <summary>
        /// 编辑
        /// </summary>
        public DelegateCommand<craft_LineInfo> EditCommand { get; set; }
        /// <summary>
        /// 删除
        /// </summary>
        public DelegateCommand<craft_LineInfo> DeleteCommand { get; set; }
        /// <summary>
        /// 新增
        /// </summary>
        public DelegateCommand AddCommand { get; set; }
        /// <summary>
        /// 刷新，重新从数据库加载数据
        /// </summary>
        public DelegateCommand RefreshCommand { get; set; }
        /// <summary>
        /// 弹窗服务
        /// </summary>
        private readonly IDialogService _dialogService;
        /// <summary>
        /// 缓存数据服务
        /// </summary>
        private readonly IDataCacheService _cacheService;
        /// <summary>
        /// 事件聚合器
        /// </summary>
        private readonly IEventAggregator _eventAggregator;

        /// <summary>
        /// 数据库操作单例
        /// </summary>
        private IRepository<craft_LineInfo> _repo;

        private ObservableCollection<craft_LineInfo> _lines = new ObservableCollection<craft_LineInfo>();
        /// <summary>
        /// 型号集合
        /// </summary>
        public ObservableCollection<craft_LineInfo> Lines
        {
            get { return _lines; }
            set { SetProperty(ref _lines, value); }
        }

        private bool _isRefreshing;
        /// <summary>
        /// 刷新中，防止重复加载数据
        /// </summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }
        /// <summary>
        /// 是否为自己发布
        /// </summary>
        private bool isIPublish = false;

        #endregion ----------------------------------属性和字段


        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="dialogService">弹窗</param>
        /// <param name="eventAggregator">时间聚合器</param>
        /// <param name="cacheService">缓存数据单例</param>
        public ProductionLineViewModel(IDialogService dialogService,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            IRepository<craft_LineInfo> repo)
        {
            //弹窗服务
            _dialogService = dialogService;
            //缓存数据服务
            _cacheService = cacheService;
            //事件聚合器
            _eventAggregator = eventAggregator;
            //数据库操作单例
            _repo = repo;
            //实例化命令
            //编辑型号按钮指令
            EditCommand = new DelegateCommand<craft_LineInfo>(Edit);
            //删除型号按钮指令
            DeleteCommand = new DelegateCommand<craft_LineInfo>(Delete);
            //新增型号按钮指令
            AddCommand = new DelegateCommand(Add);
            //新增型号按钮指令
            RefreshCommand = new DelegateCommand(Refresh);
            // 先检查是否有缓存数据
            // 通过类型获取对应的缓存数据
            if (_cacheService.HasData<List<craft_LineInfo>>())
            {
                //获取对应类型的缓存数据
                var userData = _cacheService.GetData<List<craft_LineInfo>>();
                //将所有数据初始化加载到界面
                OnOrderUpdated(userData);
            }
            //订阅事件 订阅产线数据发布
            eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>().
                Subscribe(OnOrderUpdated, ThreadOption.UIThread);

        }

        #region ============================== 刷新 ==============================
        /// <summary>
        /// 刷新按钮，重新加载所有数据
        /// </summary>
        private async void Refresh()
        {
            //防止重复刷新
            if (IsRefreshing) return;
            IsRefreshing = true;
            try
            {
                //清空集合
                Lines.Clear();
                //获取所有数据
                var datas = await _repo.GetAllAsync();
                //更新到界面
                foreach (var data in datas)
                {
                    Lines.Add(data);
                }
                // 标记为自己发布，避免重复处理
                isIPublish = true;
                // 发布事件通知其他模块
                _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>().Publish(Lines.ToList());
                // 更新缓存
                _cacheService.SetData(Lines.ToList());

            }
            catch (Exception ex)
            {

                HandyControl.Controls.MessageBox.Show($"加载数据失败：{ex.Message}", "错误");
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        #endregion ----------------------------------刷新

        #region ============================== 编辑 ==============================

        /// <summary>
        /// 编辑产线
        /// </summary>
        /// <param name="info"></param>
        private void Edit(craft_LineInfo info)
        {
            // 传入参数到弹窗
            var parameters = new DialogParameters();
            parameters.Add("Line", info);
            // 调用 ShowDialog 打开第二个对话框
            _dialogService.ShowDialog("EditLineView", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    // 处理第二个对话框返回的结果
                    craft_LineInfo updateResult = result.Parameters.GetValue<craft_LineInfo>("Line");
                    //找到原位置并替换
                    var temp = Lines;
                    var index = Lines.IndexOf(info);
                    if (index >= 0)
                    {
                        Lines[index] = updateResult;  // 只更新这一项，UI 只刷新这一行
                    }
                    //发布之前声明为自己发布的
                    isIPublish = true;
                    //将集合发布
                    _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>().
                    Publish(Lines.ToList());
                    ////更新缓存的发布数据
                    _cacheService.SetData(Lines.ToList());
                }
            });
        }

        #endregion ----------------------------------编辑

        #region ============================== 删除 ==============================

        /// <summary>
        /// 删除产线
        /// </summary>
        /// <param name="info"></param>
        private void Delete(craft_LineInfo info)
        {
            MessageBoxResult result = HandyControl.Controls.MessageBox.Show($"确认删除\"" +
                $"{info.Name}\"该产线?",
                "提示", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                //移除数据库中的数据
                _repo.Delete(info.Id);
                //移除集合中的数据,并更新到界面
                Lines.Remove(info);
                //发布之前声明为自己发布的
                isIPublish = true;
                //将集合数据发布
                _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>().
                    Publish(Lines.ToList());
                //更新缓存的发布数据
                _cacheService.SetData(Lines.ToList());
                HandyControl.Controls.MessageBox.Show("删除成功");
            }
        }

        #endregion ----------------------------------删除

        #region ============================== 新增 ==============================
        /// <summary>
        /// 新增产线
        /// </summary>
        private void Add()
        {
            // 调用 ShowDialog 打开第二个对话框
            _dialogService.ShowDialog("NewAddLineView", result =>
            {
                //如果弹窗返回结果为OK
                if (result.Result == ButtonResult.OK)
                {
                    // 获取回调的参数
                    var line = result.Parameters.GetValue<craft_LineInfo>("Line");
                    //添加到产线集合，更新界面
                    Lines.Add(line);
                    //发布之前声明为自己发布的
                    isIPublish = true;
                    //将集合型号发布
                    _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>().
                    Publish(Lines.ToList());
                    //更新缓存的发布数据
                    _cacheService.SetData(Lines.ToList());
                }
            });
        }
        #endregion ----------------------------------新增

        #region ============================== 订阅事件聚合器的回调方法 ==============================
        /// <summary>
        /// 订阅事件聚合器的回调方法,发布方发布数据后,更新产线到界面
        /// </summary>
        /// <param name="lines">所有产线信息</param>
        private void OnOrderUpdated(List<craft_LineInfo> lines)
        {
            //判断是否为自己发布的
            if (isIPublish)
            {
                //清除自己发布的标志
                isIPublish = false;
                //直接返回
                return;
            }
            //清空集合
            Lines.Clear();
            //添加集合，更新界面
            foreach (var line in lines)
            {
                Lines.Add(line);
            }
        }
        #endregion ----------------------------------订阅事件聚合器的回调方法

    }
}
