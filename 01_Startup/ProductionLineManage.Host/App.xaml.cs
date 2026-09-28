using DeviceModule.ViewModels.Dialog;
using DeviceModule.Views.Dialog;
using MaterialModule.ViewModels.Dialog;
using MaterialModule.Views.Dialog;
using Microsoft.Extensions.Configuration;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using ProductionLineManage.Host.SetupModule.ViewModels;
using ProductionLineManage.Host.SetupModule.Views;
using ProductionLineManage.Infrastructure.Data.Repository;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.Common;
using ProductionLineManage.Services.DataLoadGroup;
using ProductionLineManage.Services.DeviceManager;
using ProductionLineManage.Services.DeviceManager.Business;
using ProductionLineManage.Services.DeviceManager.Business.BusinessLogic;
using ProductionLineManage.Services.DeviceManager.Connection;
using ProductionLineManage.Services.DeviceManager.InteractionType;
using ProductionLineManage.Core.Abstractions;
using ProductionLineManage.Services.Repositories;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Unity;
using System.IO;
using System.Windows;
using Unity;
using WorkmanshipModule.ViewModels.Dialog;
using WorkmanshipModule.Views.Dialog;
using ProductionLineManage.Core.Configuration;
using ProductionLineManage.Core.Services.ExternalWeb;
using ProductionLineManage.Host.ExternalWeb;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Services.MotorCode;
using ProductionLineManage.Services.ExternalWeb;
using ProductionLineManage.Shared.Infrastructure;

namespace ProductionLineManage.Host
{
    /// <summary>
    /// WPF 应用程序入口（PrismApplication）。
    /// 启动顺序：单实例检查 → OnStartup → RegisterTypes → CreateShell → OnInitialized
    /// → MainView.Loaded → 登录 → 数据加载 → 主界面显示后启动设备与 SOAP。
    /// </summary>
    public partial class App : PrismApplication
    {
        #region ===================== 字段 =====================

        /// <summary>单实例互斥；仅主实例持有</summary>
        private SingleInstanceGuard? _singleInstance;

        /// <summary>是否为本进程主实例（二次启动为 false，跳过退出时的服务停止）</summary>
        private bool _isPrimaryInstance;

        #endregion

        #region ===================== 应用生命周期 =====================

        /// <summary>
        /// 静态构造尽可能早注册 AppDomain / Task 全局异常（不依赖 WPF Application）。
        /// </summary>
        static App()
        {
            GlobalExceptionHandler.Initialize(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Unhandled"));
        }

        /// <summary>应用进程启动：先占单实例，再注册异常与 Prism 初始化</summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            _singleInstance = new SingleInstanceGuard();
            if (!_singleInstance.TryAcquire(ActivateExistingInstance))
            {
                _singleInstance.Dispose();
                _singleInstance = null;
                Shutdown();
                return;
            }

            _isPrimaryInstance = true;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit; // 进程即将退出时的兜底
            base.OnStartup(e);
            // Application.Current 在 base.OnStartup 之后才就绪，此处注册 UI 线程处理器
            GlobalExceptionHandler.EnsureDispatcherHandler();
        }

        /// <summary>进程即将退出时的兜底：强制停止 Kestrel，防止后台线程残留</summary>
        private void OnProcessExit(object? sender, EventArgs e) => ForceStopExternalWebStation();

        /// <summary>WPF 应用退出：停止设备连接、停止 Web 服务、关闭异常提示</summary>
        protected override void OnExit(ExitEventArgs e)
        {
            if (_isPrimaryInstance)
            {
                try
                {
                    Container.Resolve<IDeviceManagementService>()
                        .StopAllDevicesAsync()
                        .Wait(TimeSpan.FromSeconds(3)); // 退出阶段最多等待 3 秒
                }
                catch
                {
                    // 退出阶段尽量不再抛出
                }

                ForceStopExternalWebStation();
                ExceptionToastManager.Shutdown(); // 关闭右下角异常抽屉
            }

            _singleInstance?.Dispose();
            _singleInstance = null;
            base.OnExit(e);
        }

        /// <summary>兜底释放 SOAP；StopAsync 内部已幂等，未启动或已停止时立即返回</summary>
        private void ForceStopExternalWebStation()
        {
            try
            {
                if (Container == null)
                    return;

                Container.Resolve<ExternalWebStationHost>()
                    .StopAsync(CancellationToken.None)
                    .Wait(TimeSpan.FromSeconds(5)); // 退出阶段最多等待 5 秒
            }
            catch
            {
                // 退出阶段尽量不再抛出
            }
        }

        /// <summary>二次启动时把已有主窗口（及登录/加载弹窗）还原到前台</summary>
        private void ActivateExistingInstance()
        {
            var dispatcher = Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            dispatcher.BeginInvoke(new Action(() =>
            {
                var main = MainWindow;
                if (main != null)
                {
                    RestoreAndActivate(main);
                    foreach (Window owned in main.OwnedWindows)
                    {
                        if (owned.IsVisible)
                            RestoreAndActivate(owned);
                    }
                    return;
                }

                foreach (Window window in Windows)
                {
                    if (window.IsVisible)
                        RestoreAndActivate(window);
                }
            }));
        }

        /// <summary>还原最小化并抢到前台（Topmost 翻转用于绕过 Windows 焦点限制）</summary>
        private static void RestoreAndActivate(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            if (!window.IsVisible)
                window.Show();

            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        }

        #endregion

        #region ===================== Prism 壳窗口 =====================

        /// <summary>Prism 壳窗口：返回主窗口 MainView（此时尚未弹登录框）</summary>
        protected override Window CreateShell()
        {
            // 从 DI 解析 MainView，WPF 将其设为 Application.MainWindow 并显示
            return Container.Resolve<MainView>();
        }

        #endregion

        #region ===================== DI 注册 =====================

        /// <summary>Prism 依赖注入：启动流程、弹窗、数据库、设备服务、外部 Web 等</summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // ── 启动流程相关注册（登录 → 数据加载 → 事件/缓存）──
            containerRegistry.Register<MainViewModel>();
            containerRegistry.RegisterDialogWindow<DialogWindow>();
            containerRegistry.RegisterDialog<LoginView, LoginViewModel>();       // 启动第 1 步：登录弹窗
            containerRegistry.RegisterDialog<DataLoadView, DataLoadViewModel>(); // 启动第 2 步：数据加载弹窗
            containerRegistry.RegisterSingleton<IDataLoadService, DataLoadService>();
            containerRegistry.RegisterSingleton<IEventAggregator, EventAggregator>(); // DataLoadCompletedEvent 等
            containerRegistry.RegisterSingleton<IDataCacheService, DataCacheService>();

            #region --------------------- 模块弹窗注册 ---------------------

            #region 工艺模块弹窗注册
            //新增型号
            containerRegistry.RegisterDialog<NewAddTypeView, NewAddTypeViewModel>();
            //编辑型号
            containerRegistry.RegisterDialog<EditTypeView, EditTypeViewModel>();
            //新增产线
            containerRegistry.RegisterDialog<NewAddLineView, NewAddLineViewModel>();
            //编辑产线
            containerRegistry.RegisterDialog<EditLineView, EditLineViewModel>();
            //新增工位
            containerRegistry.RegisterDialog<NewAddWorkStationView, NewAddWorkStationViewModel>();
            //编辑工位
            containerRegistry.RegisterDialog<EditWorkStationView, EditWorkStationViewModel>();
            //新增加工数据采集配置
            containerRegistry.RegisterDialog<AddDataCollectConfigView, AddDataCollectConfigViewModel>();
            //编辑加工数据采集配置
            containerRegistry.RegisterDialog<EditDataCollectConfigView, EditDataCollectConfigViewModel>();
            containerRegistry.RegisterDialog<AddStationDataTransferView, AddStationDataTransferViewModel>();
            containerRegistry.RegisterDialog<EditStationDataTransferView, EditStationDataTransferViewModel>();
            //新增工艺流程
            containerRegistry.RegisterDialog<NewAddProcessView, NewAddProcessViewModel>();
            //编辑工艺流程
            containerRegistry.RegisterDialog<EditProcessView, EditProcessViewModel>();
            #endregion

            #region 物料模块弹窗注册
            //新增物料
            containerRegistry.RegisterDialog<NewAddMaterialView, NewAddMaterialViewModel>();
            //编辑物料
            containerRegistry.RegisterDialog<EditMaterialView, EditMaterialViewModel>();
            //新增物料编码规则
            containerRegistry.RegisterDialog<AddCodeRuleView, AddCodeRuleViewModel>();
            //编辑物料编码规则
            containerRegistry.RegisterDialog<EditCodeRuleView, EditCodeRuleViewModel>();
            //新增工位物料
            containerRegistry.RegisterDialog<AddStationMaterialView, AddStationMaterialViewModel>();
            //编辑工位物料
            containerRegistry.RegisterDialog<EditStationMaterialView, EditStationMaterialViewModel>();
            #endregion

            #region 设备模块弹窗注册
            containerRegistry.RegisterDialog<AddDeviceConnectView, AddDeviceConnectViewModel>();
            containerRegistry.RegisterDialog<EditDeviceConnectView, EditDeviceConnectViewModel>();
            containerRegistry.RegisterDialog<AddAddressMappingView, AddAddressMappingViewModel>();
            containerRegistry.RegisterDialog<EditAddressMappingView, EditAddressMappingViewModel>();
            containerRegistry.RegisterDialog<LogDetailView, LogDetailViewModel>();
            #endregion

            #endregion

            #region --------------------- 数据库相关 ---------------------

            // 1. 加载配置文件 连接字符串
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 2. 注册 IConfiguration（使用 RegisterInstance）
            containerRegistry.RegisterInstance<IConfiguration>(configuration);
            // 注册 SQLHelper（单例，整个应用共享一个数据库连接）
            containerRegistry.RegisterSingleton<SQLHelper>();
            // 注册通用仓储（泛型注册）
            containerRegistry.Register(typeof(IRepository<>), typeof(Repository<>));

            #endregion

            //注册产线协议
            var activeLine = configuration["ActiveLine"] ?? string.Empty;
            ILineProfile lineProfile = LineProfileFactory.Create(activeLine);
            containerRegistry.RegisterInstance<ILineProfile>(lineProfile);
            //注册指令交互类型工厂
            containerRegistry.RegisterSingleton<ICommandInteractionFactory, Rld19145CommandInteractionFactory>();

            // 注册日志服务（单例）
            containerRegistry.RegisterSingleton<ILogger, FileLogger>();
            // 注册 LoadingService加载中动画（单例）
            containerRegistry.RegisterSingleton<ILoadingService, LoadingService>();

            #region --------------------- 设备模块服务 ---------------------

            // 设备模块服务
            containerRegistry.RegisterSingleton<IDeviceStatusManager, DeviceStatusManager>();
            containerRegistry.RegisterSingleton<IDeviceBusinessMediator, DeviceBusinessMediator>();
            containerRegistry.RegisterSingleton<IInteractionTypeFactory, InteractionTypeFactory>();
            containerRegistry.RegisterSingleton<IDeviceConnectionManager, DeviceConnectionManager>();
            containerRegistry.RegisterSingleton<IDeviceManagementService, DeviceManagementService>();
            containerRegistry.RegisterSingleton<IDeviceLogService, DeviceLogService>();
            containerRegistry.RegisterSingleton<ISharedDriverPool, SharedDriverPool>();

            // ========== 业务服务（Scoped - 每个工位独立）or Singleton==========
            containerRegistry.RegisterSingleton<IFlowCodeService, FlowCodeService>();//流水码验证
            containerRegistry.RegisterSingleton<IMaterialService, MaterialService>();//物料码验证
            containerRegistry.RegisterSingleton<IRepairService, RepairService>();//返修
            containerRegistry.RegisterSingleton<IDataSaveService, DataSaveService>();//保存数据服务单例
            //containerRegistry.RegisterScoped<IStationRecordService, StationRecordService>();
            containerRegistry.RegisterSingleton<IStationDataTransferService, StationDataTransferService>();//注册工位传值服务单例
            containerRegistry.RegisterSingleton<IRuleValidation, RuleValidation>();//验证规则

            containerRegistry.RegisterSingleton<IMotorCodeCacheService, MotorCodeCacheService>();
            containerRegistry.RegisterSingleton<IMotorCodeService, MotorCodeService>();
            containerRegistry.RegisterSingleton<IMotorCodeDispatchService, MotorCodeDispatchService>();

            // Win7 SOAP 外部工位
            var externalWebOptions = new ExternalWebStationOptions();
            configuration.GetSection("ExternalWebStation").Bind(externalWebOptions);
            containerRegistry.RegisterInstance(externalWebOptions);
            containerRegistry.RegisterSingleton<IExternalWebStationService, ExternalWebStationService>();
            containerRegistry.RegisterSingleton<ExternalWebStationHost>();

            #endregion
        }

        #endregion

        #region ===================== 初始化完成 =====================

        /// <summary>
        /// Prism 初始化完成（主窗口已创建）：注册设备 Handler。
        /// SOAP 改到数据加载完成、主界面显示后再启动（见 MainViewModel.OnDataLoadCompleted）。
        /// </summary>
        protected override void OnInitialized()
        {
            base.OnInitialized();
            RegisterDeviceHandlers();

            var logger = Container.Resolve<ILogger>();
            var profile = Container.Resolve<ILineProfile>();
            var activeLine = Container.Resolve<IConfiguration>()["ActiveLine"] ?? string.Empty;
            logger.Info($"协议包：{profile.DisplayName} ({profile.LineKey}),配置ActiveLine = {activeLine}","LineProfile");
            if (!string.Equals(profile.LineKey,activeLine,StringComparison.OrdinalIgnoreCase))
            {
                logger.Warning($"ActiveLine 与协议包不一致，仍按{profile.LineKey}运行。请检查 appsettings.json.","LineProfile");
            }

        }

        #endregion

        #region ===================== 设备业务 Handler =====================

        /// <summary>
        /// 应用启动完成后，将业务服务注册为 DeviceBusinessMediator 的 Handler。
        ///
        /// 【Resolve 与 Register 的区别】
        /// - RegisterTypes（RegisterSingleton 等）：把「接口 → 实现类」写入 DI 容器（像一本字典），此时还不创建对象。
        /// - Container.Resolve&lt;T&gt;()：从容器里「取出」已注册类型的实例（单例则返回同一个对象）。
        ///
        /// 因此这里不是重复注册 DI，而是：
        /// 1. Resolve 取出 Mediator 和业务服务实例
        /// 2. 若业务服务实现了 IDeviceDataHandler，则调用 mediator.RegisterHandler 写入 Mediator 内部字典
        /// </summary>
        private void RegisterDeviceHandlers()
        {
            // 从 DI 容器取出业务中介（在 RegisterTypes 中已 RegisterSingleton）
            var mediator = Container.Resolve<IDeviceBusinessMediator>();

            // 逐个 Resolve 业务服务 → 实现了 IDeviceDataHandler 的会自动 RegisterHandler 到 Mediator
            RegisterHandlerIfSupported(mediator, Container.Resolve<IFlowCodeService>());
            RegisterHandlerIfSupported(mediator, Container.Resolve<IMaterialService>());
            RegisterHandlerIfSupported(mediator, Container.Resolve<IRepairService>());
            RegisterHandlerIfSupported(mediator, Container.Resolve<IDataSaveService>());
            RegisterHandlerIfSupported(mediator, Container.Resolve<IStationDataTransferService>());
        }

        /// <summary>
        /// 若 service 实现了 IDeviceDataHandler，则以其 DataType 为键注册到 Mediator。
        /// 例如 FlowCodeService.DataType = "FlowCode" → Mediator._handlers["FlowCode"] = flowCodeService
        /// </summary>
        private static void RegisterHandlerIfSupported(IDeviceBusinessMediator mediator, object service)
        {
            if (service is IDeviceDataHandler handler)
            {
                mediator.RegisterHandler(handler.DataType, handler); // 按 DataType 写入 Mediator 内部字典
            }
        }

        #endregion

        #region ===================== 模块目录 =====================

        /// <summary>注册 Prism 功能模块（各业务子页面的 Module 入口）</summary>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            ///设备
            //设备看板画面
            moduleCatalog.AddModule<DeviceModule.ModuleDeviceDashboard>();
            ////设备事件日志画面
            moduleCatalog.AddModule<DeviceModule.ModuleInteractiveEvents>();
            //设备连接配置
            moduleCatalog.AddModule<DeviceModule.ModuleDeviceConnectInfo>();
            //设备地址映射
            moduleCatalog.AddModule<DeviceModule.ModuleAddressMapping>();

            /////生产
            ////生产看板
            //moduleCatalog.AddModule<ModuleProductionDashboard>();
            //型号下发
            moduleCatalog.AddModule<ProductionModule.ModuleIssueProductModel>();
            moduleCatalog.AddModule<ProductionModule.ModuleCrossStationInquiry>();
            ////产品返修
            //moduleCatalog.AddModule<ModuleProductRepair>();

            /////排程
            // //工单下发
            //moduleCatalog.AddModule<ModuleWorkOrderIssuance>();
            ////工单设置
            //moduleCatalog.AddModule<ModuleWorkOrderSettings>();

            /////工艺
            ////电机码（4 个子页）
            moduleCatalog.AddModule<WorkmanshipModule.ModuleMotorCode>();
            ////产品型号
            moduleCatalog.AddModule<WorkmanshipModule.ModuleProductType>();
            //产线配置
            moduleCatalog.AddModule<WorkmanshipModule.ModuleProductionLine>();
            //工位站点配置
            moduleCatalog.AddModule<WorkmanshipModule.ModuleStationConfig>();
            //工艺流程
            moduleCatalog.AddModule<WorkmanshipModule.ModuleProcessFlow>();
            //加工数据采集地址配置
            moduleCatalog.AddModule<WorkmanshipModule.ModuleDataCollectConfig>();
            moduleCatalog.AddModule<WorkmanshipModule.ModuleStationDataTransfer>();

            /////报表
            //产品加工数据查询
            moduleCatalog.AddModule<ReportModule.ModuleProductData>();

            ///物料
            //物料
            moduleCatalog.AddModule<MaterialModule.ModuleMaterial>();
            //物料编码规则
            moduleCatalog.AddModule<MaterialModule.ModuleCodeRules>();
            //工位物料
            moduleCatalog.AddModule<MaterialModule.ModuleStationMaterial>();
        }

        #endregion
    }
}
