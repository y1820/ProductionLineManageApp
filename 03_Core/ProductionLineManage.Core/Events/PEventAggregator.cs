using System.Data;
using Prism.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.MotorCode;

namespace ProductionLineManage.Core.Events
{
    #region ===================== 通用消息事件容器 =====================

    /// <summary> Prism 共享事件容器（嵌套简单载荷事件） </summary>
    public class PEventAggregator
    {
        /// <summary> 字符串消息广播 </summary>
        public class MessageSentEvent : PubSubEvent<string> { }

        /// <summary> 全量配置表更新（DataTable 列表） </summary>
        public class AllConfigUpdatedEvent : PubSubEvent<List<DataTable>> { }
    }

    #endregion

    #region ===================== 导航事件 =====================

    /// <summary> 导航到设备配置页面 </summary>
    public class NavigateToDeviceConfigEvent : PubSubEvent<int> { }

    /// <summary> 下发型号事件 </summary>
    public class IssueModelEvent : PubSubEvent<craft_TypeInfo> { }

    #endregion

    #region ===================== 配置缓存更新事件 =====================

    /// <summary> 产线信息共享 </summary>
    public class ProductLineInfoUpdatedEvent : PubSubEvent<List<craft_LineInfo>> { }

    /// <summary> 型号信息共享 </summary>
    public class ProductTypeInfoUpdatedEvent : PubSubEvent<List<craft_TypeInfo>> { }

    /// <summary> 登录用户信息共享 </summary>
    public class UserInfoUpdatedEvent : PubSubEvent<List<UserInfo>> { }

    /// <summary> 工位信息共享 </summary>
    public class WorkStationInfoUpdatedEvent : PubSubEvent<List<craft_StationInfo>> { }

    /// <summary> 设备通讯连接方式共享 </summary>
    public class DeviceConnectUpdatedEvent : PubSubEvent<List<device_ConnectInfo>> { }

    /// <summary> 数据采集地址配置共享 </summary>
    public class DataCollectConfigUpdatedEvent : PubSubEvent<List<craft_DataCollectConfig>> { }

    /// <summary> 工艺流程共享 </summary>
    public class ProcessFlowInfoUpdatedEvent : PubSubEvent<List<craft_ProcessInfo>> { }

    /// <summary> 物料管理共享 </summary>
    public class MaterialInfoUpdatedEvent : PubSubEvent<List<material_Info>> { }

    /// <summary> 物料条码规则共享 </summary>
    public class CodeRulesUpdatedEvent : PubSubEvent<List<material_CodeRules>> { }

    /// <summary> 工位物料绑定共享 </summary>
    public class StationMaterialUpdatedEvent : PubSubEvent<List<material_Station>> { }

    /// <summary> 地址映射共享 </summary>
    public class AddressMappingUpdatedEvent : PubSubEvent<List<device_AddressMapping>> { }

    /// <summary> 工位传值配置变更 </summary>
    public class DataTransferUpdatedEvent : PubSubEvent<List<craft_StationDataTransfer>> { }

    /// <summary> 工位流水码编码规则变更 </summary>
    public class FlowCodeRulesUpdatedEvent : PubSubEvent<List<craft_FlowCodeRules>> { }

    /// <summary> 电机码配置缓存更新（规则/日期对照/固定段/序列配置） </summary>
    public class MotorCodeConfigUpdatedEvent : PubSubEvent<MotorCodeCacheSnapshot> { }

    #endregion
}
