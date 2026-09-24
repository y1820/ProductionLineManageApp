using DeviceModule.Views;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Host.ExternalWeb;
using ProductionLineManage.Host.SetupModule.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Unity;

namespace ProductionLineManage.Host.SetupModule.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel。负责启动流程编排（登录 → 数据加载 → 发布完成事件 → 启动设备）
    /// 以及菜单导航。启动入口：MainView.Loaded 调用 RunStartupFlow()。
    /// </summary>
    public class MainViewModel : BindableBase
    {
        #region ===================== 字段与依赖 =====================

        /// <summary>Prism 区域导航，用于菜单切换子页面</summary>
        private readonly IRegionManager _regionManager;
        /// <summary>Prism 弹窗服务，ShowDialog 非阻塞，结果在回调中处理</summary>
        private readonly IDialogService _dialogService;
        /// <summary>事件聚合器，数据加载完成后发布 DataLoadCompletedEvent</summary>
        private readonly IEventAggregator _eventAggregator;
        /// <summary>设备管理，数据加载完成后 StartAllDevicesAsync，退出时 StopAllDevicesAsync</summary>
        private readonly IDeviceManagementService _deviceManagement;
        /// <summary>Win7 SOAP 宿主：数据加载完成后 StartAsync，退出时 StopAsync</summary>
        private readonly ExternalWebStationHost _externalWebStationHost;
        /// <summary>防止 RunStartupFlow 被 Loaded 重复触发</summary>
        private bool _startupFlowStarted;
        /// <summary>数据加载完成后为 true；登录/加载前取消退出时不停止设备（尚未启动）</summary>
        private bool _shouldStopDevicesOnShutdown;

        #endregion

        #region ===================== 构造 =====================

        /// <summary>注入依赖并订阅 DataLoadCompletedEvent、初始化菜单</summary>
        public MainViewModel(IRegionManager regionManager, IDialogService dialogService,
            IEventAggregator eventAggregator, IDeviceManagementService deviceManagement,
            ExternalWebStationHost externalWebStationHost)
        {
            _deviceManagement = deviceManagement;
            _externalWebStationHost = externalWebStationHost;
            _regionManager = regionManager;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            // 订阅数据加载完成事件（后台线程回调，避免阻塞 UI）
            _eventAggregator.GetEvent<DataLoadCompletedEvent>()
                .Subscribe(OnDataLoadCompleted, ThreadOption.BackgroundThread);

            OpenViewCommand = new DelegateCommand<SubItemModel>(DoOpenView);
            TestExceptionCommand = new DelegateCommand(DoTestException);
            InitializeData(); // 初始化左侧菜单树
        }

        #endregion

        #region ===================== 属性 =====================

        /// <summary>菜单集合</summary>
        public ObservableCollection<MenuItemModel> MenuList { get; set; } = new ObservableCollection<MenuItemModel>();

        /// <summary>打开视图指令</summary>
        public DelegateCommand<SubItemModel> OpenViewCommand { get; set; }

        /// <summary>临时：触发 UI 线程异常，测试全局捕获</summary>
        public DelegateCommand TestExceptionCommand { get; set; }

        /// <summary>当前登录用户名，登录成功后写入，供主界面显示</summary>
        private string _loginUserName = string.Empty;
        /// <summary>当前登录用户名</summary>
        public string LoginUserName
        {
            get { return _loginUserName; }
            set
            {
                SetProperty(ref _loginUserName, value);
            }
        }

        /// <summary>
        /// 退出时是否需要停止设备。登录取消 / 数据加载前失败为 false。
        /// </summary>
        public bool ShouldStopDevicesOnShutdown => _shouldStopDevicesOnShutdown;

        #endregion

        #region ===================== 启动流程入口 =====================

        /// <summary>
        /// 请求退出应用。MainView 订阅此事件执行 PerformShutdownAsync。
        /// skipConfirmation=true 时跳过主窗口「是否退出」确认（登录/数据加载取消）。
        /// </summary>
        public event Action<bool>? ShutdownRequested;

        /// <summary>
        /// 启动流程入口（由 MainView.Loaded 调用）。
        /// 当前仅打开登录弹窗；登录成功后在回调中再打开数据加载弹窗。
        /// </summary>
        public void RunStartupFlow()
        {
            // 防止 Loaded 重复触发
            if (_startupFlowStarted)
                return;

            _startupFlowStarted = true;
            // 第 1 步：登录（ShowDialog 立即返回，结果在回调中处理）
            ShowLoginDialog();
        }

        #endregion

        #region ===================== 登录 =====================

        /// <summary>
        /// 启动第 1 步：打开登录弹窗（LoginView）。
        /// Prism ShowDialog 非阻塞，关闭后在 callback 中分支。
        /// </summary>
        private void ShowLoginDialog()
        {
            // 可传入初始参数（当前为空）
            var parameters = new DialogParameters();
            // 显示登录对话框；callback 在用户关闭弹窗后执行
            _dialogService.ShowDialog("LoginView", parameters, result =>
            {
                // ── 分支 A：登录成功 ──
                if (result.Result == ButtonResult.OK)
                {
                    // 保存用户名到主界面
                    LoginUserName = result.Parameters.GetValue<string>("UserName");
                    // 第 2 步：打开数据加载弹窗
                    ShowDataLoadDialog();
                    return;
                }

                // ── 分支 B：用户取消（关闭按钮 / 取消）──
                if (result.Result == ButtonResult.Cancel || result.Result == ButtonResult.None)
                {
                    // 须先停 Kestrel 再退出；skipConfirmation 跳过二次确认
                    RequestShutdown(skipConfirmation: true);
                }
            });
        }

        #endregion

        #region ===================== 数据加载 =====================

        /// <summary>
        /// 启动第 2 步：打开数据加载弹窗（DataLoadView）。
        /// 弹窗内自动调用 DataLoadService.LoadAllConfigurationsAsync。
        /// </summary>
        private void ShowDataLoadDialog()
        {
            _dialogService.ShowDialog("DataLoadView", result =>
            {
                // ── 分支 A：加载失败或用户中断 ──
                if (result.Result != ButtonResult.OK)
                {
                    HandyControl.Controls.MessageBox.Show(
                        $"数据加载未完成（{result.Result}），准备退出主程序",
                        "提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    RequestShutdown(skipConfirmation: true);
                    return;
                }

                // ── 分支 B：加载成功 ──
                // 发布完成事件 → OnDataLoadCompleted → StartAllDevicesAsync
                _eventAggregator.GetEvent<DataLoadCompletedEvent>().Publish();
            });
        }

        #endregion

        #region ===================== 设备启动 =====================

        /// <summary>
        /// DataLoadCompletedEvent 订阅回调：配置已全部进缓存，主界面已显示。
        /// 此后才启动 SOAP 与 PLC，登录/加载阶段不占 8090。
        /// </summary>
        private async void OnDataLoadCompleted()
        {
            _shouldStopDevicesOnShutdown = true; // 标记退出时需要停止设备与 SOAP
            var soapStart = _externalWebStationHost.StartAsync(); // 主界面出来后再听 8090
            await _deviceManagement.StartAllDevicesAsync(); // 按配置连接所有工位 PLC
            await soapStart;
        }

        #endregion

        #region ===================== 退出与清理 =====================

        /// <summary>
        /// 向 MainView 发起退出请求。
        /// 不能直接 Application.Shutdown()，否则 MainView_Closing 会 Cancel 且 OnExit 不执行。
        /// </summary>
        /// <param name="skipConfirmation">true=跳过「是否退出」确认</param>
        private void RequestShutdown(bool skipConfirmation)
        {
            // 优先走 MainView 的优雅关闭（先停 Web 再 Shutdown）
            if (ShutdownRequested != null)
            {
                ShutdownRequested.Invoke(skipConfirmation);
                return;
            }

            // 主窗口尚未订阅时的兜底（理论上不应发生）
            Application.Current.Shutdown();
        }

        /// <summary>退出前停止设备与 Win7 SOAP Web 服务（由 MainView.PerformShutdownAsync 调用）</summary>
        /// <param name="stopDevices">false=登录/加载前取消，仅停 Web</param>
        public async Task StopServicesAsync(CancellationToken cancellationToken = default, bool stopDevices = true)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(stopDevices ? TimeSpan.FromSeconds(25) : TimeSpan.FromSeconds(6));

            if (stopDevices)
                await _deviceManagement.StopAllDevicesAsync(); // 断开所有 PLC 连接

            await _externalWebStationHost.StopAsync(linked.Token); // 停止 Kestrel SOAP 服务
        }

        #endregion

        #region ===================== 导航 =====================

        /// <summary>加载菜单栏对应的窗体</summary>
        /// <param name="model">子菜单项，含 TargetView 目标视图名</param>
        private void DoOpenView(SubItemModel model)
        {
            _regionManager.RequestNavigate("MainRegion", model.TargetView); // 在 MainRegion 区域切换子页面
        }

        /// <summary>临时：测试全局异常处理与右下角抽屉提示</summary>
        private void DoTestException()
        {
            throw new InvalidOperationException("临时异常测试：验证 GlobalExceptionHandler 与 ExceptionToast。");
        }

        /// <summary>初始化左侧菜单树（工艺 / 电机码 / 排程 / 设备 / 报表 / 生产 / 物料）</summary>
        void InitializeData()
        {

            MenuList.Add(new MenuItemModel()
            {
                Icon = "\ue668",
                Header = "工艺",
                Children = new ObservableCollection<SubItemModel>()
                {
                    new SubItemModel()
                    {
                        Header  = "型号管理", TargetView="ProductTypeView", Level = 2 // 产品型号 客户添加 
                    },
                    new SubItemModel()
                    {
                        Header = "产线配置" ,TargetView = "ProductionLineView" , Level = 2// 产线配置
                    },
                    new SubItemModel()
                    {
                        Header = "工位配置" ,TargetView = "StationConfigView" , Level = 2 // 添加工位  
                    },
                    new SubItemModel()
                    {
                        Header="工艺流程" , TargetView="ProcessFlowView" , Level = 2//工位流流转、工位重复工作、工位所包含其他工位零部件
                    },
                    new SubItemModel()
                    {
                        Header="数据采集配置" , TargetView="DataCollectConfigView", Level = 2
                    },
                    new SubItemModel()
                    {
                        Header="工位传值" , TargetView="StationDataTransferView", Level = 2
                    },
                }
            });
            MenuList.Add(new MenuItemModel()
            {
                Icon = "\ue8cb",
                Header = "电机码",
                Children = new ObservableCollection<SubItemModel>()
                {
                    new SubItemModel()
                    {
                        Header = "日期代号对照", TargetView = "MotorCodeDateMapView", Level = 2
                    },
                    new SubItemModel()
                    {
                        Header = "固定信息设置", TargetView = "MotorCodeFixedSegmentView", Level = 2
                    },
                    new SubItemModel()
                    {
                        Header = "生成规则", TargetView = "MotorCodeRuleView", Level = 2
                    },
                    new SubItemModel()
                    {
                        Header = "序列号设置", TargetView = "MotorCodeSequenceView", Level = 2
                    },
                }
            });
            MenuList.Add(new MenuItemModel()
            {
                Icon = "\uec35",
                Header = "排程",
                Children = new ObservableCollection<SubItemModel>()
                {
                    new SubItemModel()
                    {
                        Header  = "工单设置", TargetView="WorkOrderSettingsView", Level = 3
                    },
                    new SubItemModel()
                    {
                        Header  = "工单下发", TargetView="WorkOrderIssuanceView", Level = 3
                    }
                }
            });
            MenuList.Add(new MenuItemModel()
            {
                Icon = "\ue612",
                Header = "设备",
                Children = new ObservableCollection<SubItemModel>()
                {
                     new SubItemModel()
                    {
                        Header  = "工位通讯配置", TargetView="DeviceConnectInfoView" , Level = 2
                    },
                    new SubItemModel()
                    {
                        Header  = "设备交互地址配置", TargetView="AddressMappingView", Level = 2
                    },
                    new SubItemModel()
                    {
                        Header  = "设备看板", TargetView="DeviceDashboardView", Level = 3
                    },
                    new SubItemModel()
                    {
                        Header  = "事件日志", TargetView="InteractiveEventsView", Level = 3
                    },

                }
            });
            MenuList.Add(new MenuItemModel()
            {
                Icon = "\ue63f",
                Header = "报表",
                Children = new ObservableCollection<SubItemModel>()
                {
                    new SubItemModel()
                    {
                        Header  = "产品数据", TargetView="ProductDataView", Level = 3
                    },
                    new SubItemModel()
                    {
                        Header  = "绑定物料", TargetView="MaterialBindView", Level = 3
                    },
                    new SubItemModel()
                    {
                        Header  = "过站明细", TargetView="StationPassRecordView", Level = 3
                    },
                      new SubItemModel()
                    {
                        Header  = "返修查询", TargetView="", Level = 3
                    },
                }
            });
            MenuList.Add(new MenuItemModel()
            {
                Icon = "\ue632",
                Header = "生产",
                Children = new ObservableCollection<SubItemModel>()
                {
                    new SubItemModel()
                    {
                        Header  = "生产看板", TargetView="ProductionDashboardView", Level = 3
                    },
                     new SubItemModel()
                    {
                        Header  = "型号下发", TargetView="IssueProductModelView", Level = 3
                    },
                    new SubItemModel()
                    {
                        Header  = "产线管理", TargetView="", Level = 3
                    },
                    new SubItemModel()
                    {
                        Header  = "产品工位状态", TargetView="CrossStationInquiryView", Level = 3
                    },
                    new SubItemModel()
                    {
                        Header  = "产品返修", TargetView="ProductRepairView", Level = 3
                    },
                }
            });
            MenuList.Add(new MenuItemModel()
            {
                Icon = "\ue64e",
                Header = "物料",
                Children = new ObservableCollection<SubItemModel>()
                {
                    new SubItemModel()
                    {
                        Header  = "物料管理", TargetView="MaterialView", Level = 2
                    },
                    new SubItemModel()
                    {
                        Header  = "物料条码规则", TargetView="CodeRulesView", Level = 2
                    },
                    new SubItemModel()
                    {
                        Header  = "工位物料", TargetView="StationMaterialView", Level = 2
                    },

                }
            });

        }

        #endregion
    }

    #region ===================== 菜单模型 =====================

    /// <summary>主菜单项：图标 + 标题 + 子菜单集合</summary>
    public class MenuItemModel
    {
        /// <summary>图标,必须初始化</summary>
        public required string Icon { get; set; }
        /// <summary>标题,必须初始化</summary>
        public required string Header { get; set; }
        /// <summary>子菜单集合</summary>
        public ObservableCollection<SubItemModel> Children { get; set; } = [];
        /// <summary>权限等级</summary>
        public int Level { get; set; }
    }

    /// <summary>子菜单项：标题 + 目标视图名 + 权限等级</summary>
    public class SubItemModel
    {
        /// <summary>标题,必须初始化</summary>
        public required string Header { get; set; }
        /// <summary>视图名称,必须初始化</summary>
        public required string TargetView { get; set; }
        /// <summary>权限等级</summary>
        public int Level { get; set; }
    }

    #endregion
}
