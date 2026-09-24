using Prism.Events;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProductionLineManage.Infrastructure.Data.Repository;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Services.MotorCode;


namespace ProductionLineManage.Services.DataLoadGroup
{
    /// <summary>
    /// 数据加载服务：启动时从数据库读取全部配置表，
    /// 通过 EventAggregator 发布 *UpdatedEvent，并写入 IDataCacheService 缓存。
    /// 由 DataLoadViewModel.LoadDataAsync 调用 LoadAllConfigurationsAsync。
    /// </summary>
    public class DataLoadService : IDataLoadService
    {
        #region ===================== 字段 =====================

        /// <summary> 发布各配置表的 Updated 事件 </summary>
        private readonly IEventAggregator _eventAggregator;
        /// <summary> 全局内存缓存，供各模块读取 </summary>
        private readonly IDataCacheService _cacheService;
        /// <summary> 数据库访问 </summary>
        private readonly SQLHelper _sqlHelper;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入事件聚合器、缓存服务与数据库助手 </summary>
        public DataLoadService(IEventAggregator eventAggregator, IDataCacheService cacheService, SQLHelper sqlHelper)
        {
            _eventAggregator = eventAggregator; // 用于 PublishData 通知订阅方
            _cacheService = cacheService; // 用于 PublishData 写入缓存
            _sqlHelper = sqlHelper; // 用于各表 QueryAsync
        }

        #endregion

        #region ===================== 异步加载所有配置信息 =====================

        /// <summary>
        /// 异步加载所有配置信息（启动流程核心）。
        /// 按 tasks 列表顺序执行：查库 → PublishData（事件+缓存）→ 报告进度。
        /// 全部完成后延迟 1.5 秒再返回，由 DataLoadViewModel 关闭弹窗。
        /// </summary>
        /// <param name="progress">进度报告：(百分比, 提示文字)</param>
        public async Task LoadAllConfigurationsAsync(IProgress<(int percent, string message)> progress)
        {
            // ========== 第 1 部分：定义加载任务列表 ==========
            // 每项：(SQL, 显示名称, 异步加载动作)
            var tasks = new List<(string sql, string tableName, Func<Task> action)>();

            // 0：型号信息 → ProductTypeInfoUpdatedEvent
            tasks.Add(("SELECT * FROM craft_TypeInfo", "型号信息", async () =>
            {
                var data = await _sqlHelper.QueryAsync<craft_TypeInfo>("SELECT * FROM craft_TypeInfo");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<ProductTypeInfoUpdatedEvent>().Publish(list));
            }
            ));

            // 1：产线信息 → ProductLineInfoUpdatedEvent
            tasks.Add(("SELECT * FROM craft_LineInfo", "产线信息", async () =>
            {
                var data = await _sqlHelper.QueryAsync<craft_LineInfo>("SELECT * FROM craft_LineInfo");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<ProductLineInfoUpdatedEvent>().Publish(list));
            }
            ));

            // 2：工位信息 → WorkStationInfoUpdatedEvent
            tasks.Add(("SELECT * FROM craft_StationInfo", "工位信息", async () =>
            {
                var data = await _sqlHelper.QueryAsync<craft_StationInfo>("SELECT * FROM craft_StationInfo");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<WorkStationInfoUpdatedEvent>().Publish(list));
            }
            ));

            // 3：工艺流程 → ProcessFlowInfoUpdatedEvent
            tasks.Add(("SELECT * FROM craft_ProcessInfo", "工艺流程信息", async () =>
            {
                var data = await _sqlHelper.QueryAsync<craft_ProcessInfo>("SELECT * FROM craft_ProcessInfo");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<ProcessFlowInfoUpdatedEvent>().Publish(list));
            }
            ));

            // 4：设备连接 → DeviceConnectUpdatedEvent
            tasks.Add(("SELECT * FROM device_ConnectInfo", "设备连接信息", async () =>
            {
                var data = await _sqlHelper.QueryAsync<device_ConnectInfo>("SELECT * FROM device_ConnectInfo");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<DeviceConnectUpdatedEvent>().Publish(list));
            }
            ));

            // 5：数据采集地址 → DataCollectConfigUpdatedEvent
            tasks.Add(("SELECT * FROM craft_DataCollectConfig", "数据地址信息", async () =>
            {
                var data = await _sqlHelper.QueryAsync<craft_DataCollectConfig>("SELECT * FROM craft_DataCollectConfig");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<DataCollectConfigUpdatedEvent>().Publish(list));
            }
            ));

            // 6：用户 → UserInfoUpdatedEvent
            tasks.Add(("SELECT * FROM UsersInfo", "用户信息", async () =>
            {
                var data = await _sqlHelper.QueryAsync<UserInfo>("SELECT * FROM UsersInfo");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<UserInfoUpdatedEvent>().Publish(list));
            }
            ));

            // 7：物料 → MaterialInfoUpdatedEvent
            tasks.Add(("SELECT * FROM material_Info", "物料信息", async () =>
            {
                var data = await _sqlHelper.QueryAsync<material_Info>("SELECT * FROM material_Info");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<MaterialInfoUpdatedEvent>().Publish(list));
            }
            ));

            // 8：物料条码规则 → CodeRulesUpdatedEvent
            tasks.Add(("SELECT * FROM material_CodeRules", "物料条码规则", async () =>
            {
                var data = await _sqlHelper.QueryAsync<material_CodeRules>("SELECT * FROM material_CodeRules");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<CodeRulesUpdatedEvent>().Publish(list));
            }
            ));

            // 9：工位物料 → StationMaterialUpdatedEvent
            tasks.Add(("SELECT * FROM material_Station", "工位物料", async () =>
            {
                var data = await _sqlHelper.QueryAsync<material_Station>("SELECT * FROM material_Station");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<StationMaterialUpdatedEvent>().Publish(list));
            }
            ));

            // 10：中央 PLC 地址映射 → AddressMappingUpdatedEvent
            tasks.Add(("SELECT * FROM device_AddressMapping", "中央PLC映射", async () =>
            {
                var data = await _sqlHelper.QueryAsync<device_AddressMapping>("SELECT * FROM device_AddressMapping");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<AddressMappingUpdatedEvent>().Publish(list));
            }
            ));

            // 11：工位传值 → DataTransferUpdatedEvent
            tasks.Add(("SELECT * FROM craft_StationDataTransfer", "工位传值", async () =>
            {
                var data = await _sqlHelper.QueryAsync<craft_StationDataTransfer>("SELECT * FROM craft_StationDataTransfer");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<DataTransferUpdatedEvent>().Publish(list));
            }
            ));

            // 12：流水码规则 → FlowCodeRulesUpdatedEvent
            tasks.Add(("SELECT * FROM craft_FlowCodeRules", "工位流水码编码规则", async () =>
            {
                var data = await _sqlHelper.QueryAsync<craft_FlowCodeRules>("SELECT * FROM craft_FlowCodeRules");
                PublishData(data.ToList(), list => _eventAggregator.GetEvent<FlowCodeRulesUpdatedEvent>().Publish(list));
            }
            ));

            // 13：电机码配置 → MotorCodeConfigUpdatedEvent（多表聚合加载）
            Func<Task> loadMotorCodeCache = async () =>
            {
                var snapshot = await MotorCodeCacheService.LoadFromDatabaseAsync(_sqlHelper);
                _cacheService.SetData(snapshot);
                _eventAggregator.GetEvent<MotorCodeConfigUpdatedEvent>().Publish(snapshot);
            };
            tasks.Add(("SELECT 1", "电机码配置", loadMotorCodeCache));

            // ========== 第 2 部分：顺序执行所有任务并报告进度 ==========
            int total = tasks.Count;
            for (int i = 0; i < total; i++)
            {
                var task = tasks[i];
                // 进度：第 1 项 = 1/total*100%，最后一项 = 100%
                int percent = (i + 1) * 100 / total;
                progress?.Report((percent, $"正在加载：{task.tableName}"));

                // 执行查库 + 发布事件 + 写缓存
                await task.action();
            }

            // ========== 第 3 部分：完成提示并短暂停留 ==========
            progress?.Report((100, "加载完成,准备跳转至主窗体"));
            // 让用户看到 100% 与完成文字后再关闭弹窗
            await Task.Delay(1500);
        }

        #endregion

        #region ===================== 通用发布方法 =====================

        /// <summary>
        /// 将单表数据同时发布到 EventAggregator 并写入 IDataCacheService。
        /// 各业务模块可订阅 *UpdatedEvent 或从缓存读取。
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="data">查库结果</param>
        /// <param name="publishEvent">对应表的 Updated 事件发布委托</param>
        private void PublishData<T>(List<T> data, Action<List<T>> publishEvent)
        {
            // 1. 通知已订阅该事件的 ViewModel / 服务
            publishEvent?.Invoke(data);
            // 2. 写入全局缓存供后续读取
            _cacheService.SetData(data);
        }

        #endregion
    }
}
