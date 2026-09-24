using Microsoft.IdentityModel.Tokens;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using ProductionLineManage.Infrastructure.Data.Repository;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>
    /// 工艺 - 产品型号视图模型
    /// </summary>
    public class ProductTypeViewModel : BindableBase
    {
        #region ============================== 属性和字段 ==============================

        /// <summary>
        /// 编辑
        /// </summary>
        public DelegateCommand<craft_TypeInfo> EditCommand { get; set; }
        /// <summary>
        /// 删除
        /// </summary>
        public DelegateCommand<craft_TypeInfo> DeleteCommand { get; set; }
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
        private IRepository<craft_TypeInfo> _repo;

        private ObservableCollection<craft_TypeInfo> _types = new ObservableCollection<craft_TypeInfo>();
        /// <summary>
        /// 型号集合
        /// </summary>
        public ObservableCollection<craft_TypeInfo> Types
        {
            get { return _types; }
            set { SetProperty(ref _types, value); }
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
        public ProductTypeViewModel(IDialogService dialogService,
            IEventAggregator eventAggregator, IDataCacheService cacheService,
            IRepository<craft_TypeInfo> repo)
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
            EditCommand = new DelegateCommand<craft_TypeInfo>(Edit);
            //删除型号按钮指令
            DeleteCommand = new DelegateCommand<craft_TypeInfo>(Delete);
            //新增型号按钮指令
            AddCommand = new DelegateCommand(Add);
            //新增型号按钮指令
            RefreshCommand = new DelegateCommand(Refresh);

            // 先检查是否有缓存数据
            // 通过类型获取对应的缓存数据
            if (_cacheService.HasData<List<craft_TypeInfo>>())
            {
                //获取对应类型的缓存数据
                var types = _cacheService.GetData<List<craft_TypeInfo>>();
                //将所有型号初始化加载到界面
                OnOrderUpdated(types);
            }
            //订阅事件 订阅型号发布
            eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>().Subscribe(OnOrderUpdated, ThreadOption.UIThread);
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
                Types.Clear();
                //获取所有数据
                var datas = await _repo.GetAllAsync();
                //更新到界面
                foreach (var data in datas)
                {
                    Types.Add(data);
                }
                //声明自己发布
                isIPublish = true;
                //将集合型号发布
                _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>().Publish(Types.ToList());
                //更新缓存的发布数据
                _cacheService.SetData(Types.ToList());
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
        /// 编辑型号
        /// </summary>
        /// <param name="info">所选择行的数据</param>
        private void Edit(craft_TypeInfo info)
        {
            // 传入参数到弹窗
            var parameters = new DialogParameters();
            parameters.Add("Type", info);
            //打开弹窗
            _dialogService.ShowDialog("EditTypeView", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    //回调参数
                    craft_TypeInfo typeInfo = result.Parameters.GetValue<craft_TypeInfo>("Type");
                    //找到原位置并替换
                    var temp = Types;
                    var index = Types.IndexOf(info);
                    if (index >= 0)
                    {
                        Types[index] = typeInfo;  // 只更新这一项，UI 只刷新这一行
                    }
                    //声明自己发布
                    isIPublish = true;
                    //将集合型号发布
                    _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>().Publish(Types.ToList());
                    //更新缓存的发布数据
                    _cacheService.SetData(Types.ToList());

                }
            });
        }
        #endregion ----------------------------------编辑

        #region ============================== 删除 ==============================

        /// <summary>
        /// 删除型号
        /// </summary>
        /// <param name="info"></param>
        private async void Delete(craft_TypeInfo info)
        {

            MessageBoxResult result = HandyControl.Controls.MessageBox.Show($"确认删除\"" +
               $"{info.Name}\"该型号?",
               "提示", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                //移除数据库中的数据
                await _repo.DeleteAsync(info.Id);
                //移除集合中的数据,并更新到界面
                Types.Remove(info);
                //声明自己发布
                isIPublish = true;
                //将集合型号发布
                _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>().Publish(Types.ToList());
                //更新缓存的发布数据
                _cacheService.SetData(Types.ToList());
                HandyControl.Controls.MessageBox.Show("删除成功");
            }
        }

        #endregion ----------------------------------删除

        #region ============================== 新增 ==============================
        /// <summary>
        /// 新增型号
        /// </summary>
        private void Add()
        {
            // 调用 ShowDialog 打开第二个对话框
            _dialogService.ShowDialog("NewAddTypeView", result =>
            {
                //如果弹窗返回结果为OK
                if (result.Result == ButtonResult.OK)
                {
                    // 回调参数
                    var type = result.Parameters.GetValue<craft_TypeInfo>("Type");
                    //更新界面
                    Types.Add(type);
                    //声明自己发布
                    isIPublish = true;
                    //将集合型号发布
                    _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>().Publish(Types.ToList());
                    //更新缓存的发布数据
                    _cacheService.SetData(Types.ToList());
                }
            });
        }
        #endregion ----------------------------------新增

        #region ============================== 订阅事件聚合器的回调方法 ==============================
        /// <summary>
        /// 订阅事件聚合器的回调方法,发布方发布数据后,更新型号到界面
        /// </summary>
        /// <param name="types">所有型号信息</param>
        private void OnOrderUpdated(List<craft_TypeInfo> types)
        {
            //是否为自己发布的
            if (isIPublish)
            {
                isIPublish = false;
                return;
            }
            //清空集合
            Types.Clear();
            //如果集合数量小于0直接返回
            if (types?.Count > 0)
                foreach (var type in types)
                {
                    Types.Add(type);
                }
        }
        #endregion ----------------------------------订阅事件聚合器的回调方法

    }
}
