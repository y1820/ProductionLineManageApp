
namespace ProductionLineManage.Core.Models.Device
{
    /// <summary>
    /// 工位交互状态（每个 IInteractionType 实例私有持有，不对外暴露）。
    /// 用途：编排层判断当前消息是否允许触发业务处理器。
    /// 与 IDeviceStatusManager 的区别：StationWorkState 为交互内部状态；DeviceStatus 为 UI 展示状态。
    /// </summary>
    public class StationWorkState
    {
        #region ===================== 工位基本信息 =====================

        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        /// <summary> 产线 Id </summary>
        public int LineId { get; set; }

        /// <summary> 产品型号名称 </summary>
        public string ProductTypeName { get; set; } = string.Empty;

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 工位状态 Id（production_ProductStationStatus.Id，200 暂存、8000 写入工艺历史） </summary>
        public int StationRecordId { get; set; }

        /// <summary> 返修 200 计算出的允许返修顺序（Sequence 第几位，0 表示不可选） </summary>
        public int AllowRepairSequence { get; set; }

        /// <summary> 当前流水码历史返修次数（200 查询缓存，8000 写报表/过站时使用） </summary>
        public int RepairCount { get; set; }

        /// <summary> 当前周期是否为返修工位流程 </summary>
        public bool IsCurrentRepairStation { get; set; }

        /// <summary> 8000 返修确认后的目标工位 Id（待过站记录所在工位） </summary>
        public int RepairTargetStationId { get; set; }

        /// <summary> 本周期是否为返修目标工位的返修加工（正常工位 200 识别） </summary>
        public bool IsRepairProcess { get; set; }

        /// <summary> 托盘号 </summary>
        public string TrayCode { get; set; } = "--";

        #endregion

        #region ===================== 指令型：业务生命周期 =====================

        /// <summary>
        /// 是否已开始交互（收到 100 握手成功后为 true）。
        /// 未握手时除 100 外其他指令均不允许执行。
        /// </summary>
        public bool IsInteractionStarted { get; set; }

        /// <summary>
        /// 流水码是否合格（200 验证通过后为 true）。
        /// 500 物料验证、800 保存数据的前置条件。
        /// </summary>
        public bool IsFlowCodeQualified { get; set; }

        /// <summary>
        /// 业务周期是否已结束（800 保存成功后为 true，同时复位其他周期标志）。
        /// 为 true 时软件除 100 外不再处理任何业务，需 PLC 重新发起 100 握手。
        /// </summary>
        public bool IsBusinessCycleEnded { get; set; }

        /// <summary> 是否处于返修模式（700 触发） </summary>
        public bool IsRepairMode { get; set; }

        #endregion

        #region ===================== 信号型：信号到达标记 =====================

        /// <summary> 物料码完成信号是否已到 </summary>
        public bool IsMaterialCodeDone { get; set; }

        /// <summary> 等待配对的物料码（信号型流水码/物料码分步到达时使用） </summary>
        public string? PendingMaterialCode { get; set; }

        /// <summary> 本周期已通过物料验证、待 800 绑定的物料列表 </summary>
        public List<ValidatedMaterialItem> ValidatedMaterials { get; } = new();

        #endregion

        #region ===================== 处理中标记（防重入） =====================

        /// <summary> 是否正在执行业务处理器 </summary>
        public bool IsProcessing { get; set; }

        /// <summary> 最后一次触发业务的时间 </summary>
        public DateTime? LastProcessTime { get; set; }

        #endregion

        #region ===================== 防重复（信号型一个工作周期内） =====================

        /// <summary> 当前周期内该信号是否已处理过 </summary>
        public bool IsMaterialCodeHandled { get; set; }

        #endregion
    }
}
