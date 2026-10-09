using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Core.Models.Device
{
    #region ===================== 流水码验证 =====================

    /// <summary> 流水码验证 Handler 入参（由 Interaction 编排层从 StationWorkState 组装） </summary>
    public class FlowCodeVerifyPayload
    {
        /// <summary> 流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 Id </summary>
        public int LineId { get; set; }

        /// <summary> 托盘码 </summary>
        public string TrayCode { get; set; } = "--";

        /// <summary> 模式，用来区分不同业务 </summary>
        public FlowCodeVerifyMode Mode { get; set; } = 0;
    }
    public enum FlowCodeVerifyMode
    {
        Full = 0,       // 现在正常 200：规则 + 上工位 + 过站记录
        RulesOnly = 1   // 现在返修工位 200：只校编码规则
    }
    /// <summary> 流水码验证 Handler 返回结果 </summary>
    public class FlowCodeVerifyResult
    {
        /// <summary> 流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 工位状态 Id（production_ProductStationStatus.Id） </summary>
        public int StationRecordId { get; set; }

        /// <summary> 本工位当前是否为返修待加工（来自 production_ProductStationStatus） </summary>
        public bool IsRepair { get; set; }

        /// <summary> 返修次数 </summary>
        public int RepairCount { get; set; }

        /// <summary> 返修目标工位 Id </summary>
        public int RepairTargetStationId { get; set; }
    }

    #endregion

    #region ===================== 物料验证 =====================

    /// <summary> 物料验证 Handler 入参（由 Interaction 编排层组装后传入） </summary>
    public class MaterialVerifyPayload
    {
        /// <summary> 物料类型 </summary>
        public string MaterialType { get; set; } = string.Empty;

        /// <summary> 物料编码 </summary>
        public string MaterialCode { get; set; } = string.Empty;

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 Id </summary>
        public int LineId { get; set; }

        /// <summary> 本周期已验证通过的物料 Id（用于顺序/父子校验） </summary>
        public IReadOnlyList<int> ValidatedMaterialIds { get; set; } = Array.Empty<int>();

        /// <summary> 返修工位仅校验编码规则 </summary>
        public bool RulesOnly { get; set; }
    }

    /// <summary> 物料验证 Handler 返回结果 </summary>
    public sealed class MaterialVerifyResult
    {
        /// <summary> 是否成功 </summary>
        public bool Success { get; init; }

        /// <summary> 提示或错误消息 </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary> 是否为规则校验失败 </summary>
        public bool IsRuleFailure { get; init; }

        /// <summary> 物料 Id </summary>
        public int MaterialId { get; init; }

        /// <summary> 物料编码 </summary>
        public string MaterialCode { get; init; } = string.Empty;

        /// <summary> 物料名称 </summary>
        public string MaterialName { get; init; } = string.Empty;

        /// <summary> 录入顺序 </summary>
        public int Sequence { get; init; }
    }

    /// <summary> 8000 保存时需写入的物料绑定项 </summary>
    public sealed class SaveMaterialBindItem
    {
        /// <summary> 物料编码 </summary>
        public string MaterialCode { get; init; } = string.Empty;

        /// <summary> 物料名称 </summary>
        public string MaterialName { get; init; } = string.Empty;
    }

    #endregion

    #region ===================== 返修确认 =====================

    /// <summary> 返修确认 Handler 入参（8000 返修分支） </summary>
    public class RepairConfirmPayload
    {
        /// <summary> 流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 托盘码 </summary>
        public string TrayCode { get; set; } = string.Empty;

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 Id </summary>
        public int LineId { get; set; }

        /// <summary> PLC 请求返修顺序（Sequence 第几位） </summary>
        public int TargetRepairSequence { get; set; }

        /// <summary> 200 计算出的允许返修顺序 </summary>
        public int AllowRepairSequence { get; set; }

        /// <summary> 200 查询到的历史返修次数 </summary>
        public int RepairCount { get; set; }

        /// <summary> 旧版按工位 Id（兼容） </summary>
        public int TargetStationId { get; set; }
    }

    /// <summary> 返修确认 Handler 返回结果 </summary>
    public sealed class RepairConfirmResult
    {
        /// <summary> 目标工位状态 Id（production_ProductStationStatus.Id） </summary>
        public int StationRecordId { get; init; }

        /// <summary> 目标工位 Id </summary>
        public int TargetStationId { get; init; }

        /// <summary> 返修次数 </summary>
        public int RepairCount { get; init; }
    }

    #endregion

    #region ===================== 可返修工位查询 =====================
    /// <summary> 查询可返修的工位消息 </summary>
    public class RepairQueryPayload
    {
        /// <summary> 流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 Id </summary>
        public int LineId { get; set; }
    }

    /// <summary> 查询可返修的工位消息返回结果 </summary>
    public sealed class RepairQueryResult
    {
        public int AllowSequence {  get; set; }
        public int RepairCount { get; set; }
    }

    #endregion

    #region ===================== 保存数据 =====================

    /// <summary> 保存数据 Handler 入参（工艺数据由 DataSaveService 按 Mediator 采集配置从 PLC 读取） </summary>
    public class SaveDataPayload
    {
        /// <summary> 流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 过站状态：1=合格，2=不合格 </summary>
        public int Status { get; set; } = 1;

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 Id </summary>
        public int LineId { get; set; }

        /// <summary> 工位状态 Id（production_ProductStationStatus.Id，写入 report_ProcessHistory） </summary>
        public int StationRecordId { get; set; }

        /// <summary> 本周期已验证、需在保存时绑定的物料（可为空） </summary>
        public IReadOnlyList<SaveMaterialBindItem> Materials { get; set; } = Array.Empty<SaveMaterialBindItem>();

        /// <summary> 是否为返修保存（过站记录在目标工位，非当前返修工位） </summary>
        public bool IsRepairSave { get; set; }

        /// <summary> 返修目标工位 Id（待过站记录所在工位） </summary>
        public int RepairTargetStationId { get; set; }

        /// <summary> 返修次数（写入报表 IsRepair/RepairCount） </summary>
        public int RepairCount { get; set; }

        /// <summary> 返修目标工位正常 8000 保存时标记报表为返修（仍走正常保存分支） </summary>
        public bool IsRepairProcess { get; set; }
        /// <summary> 托盘号 </summary>
        public string TrayCode { get; set; } = string.Empty;
    }

    #endregion

    #region ===================== 工位传值 =====================

    /// <summary> 工位传值 Handler 入参 </summary>
    public class StationTransferPayload
    {
        /// <summary> 流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 Id </summary>
        public int LineId { get; set; }
    }

    /// <summary> 200 流水码验证通过后，写入请求工位 PLC 的单项传值数据 </summary>
    public class StationTransferWriteItem
    {
        /// <summary> 配置的数据名称（report_ProcessHistory.DataName） </summary>
        public string RequestDataName { get; set; } = string.Empty;

        /// <summary> 数据源工位 Id（如 OP090） </summary>
        public int SourceStationId { get; set; }

        /// <summary> 从历史表查到的值 </summary>
        public string DataValue { get; set; } = string.Empty;

        /// <summary> 写入请求工位的 PLC 地址 </summary>
        public string TargetAddress { get; set; } = string.Empty;

        /// <summary> PLC 数据类型 </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary> PLC 数据长度 </summary>
        public int DataLength { get; set; }
    }

    #endregion
}
