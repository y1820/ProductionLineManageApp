namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 工艺流程 - 型号产线工位顺序配置 </summary>
    public class craft_ProcessInfo : BaseEntity
    {
        #region ===================== 关联标识 =====================

        /// <summary> 产品型号 ID </summary>
        public int TypeId { get; set; }

        /// <summary> 所属产线 ID </summary>
        public int LineId { get; set; }

        /// <summary> 工位 ID </summary>
        public int StationId { get; set; }

        #endregion

        #region ===================== 工序配置 =====================

        /// <summary> 是否允许重复过站 </summary>
        public bool IsRepeatWork { get; set; }

        /// <summary> 上工序工位 ID </summary>
        public int UpperWorkstationId { get; set; }

        /// <summary> 工序顺序号 </summary>
        public int Sequence { get; set; }

        /// <summary> 是否启用 </summary>
        public bool IsEnable { get; set; } = true;

        /// <summary> 是否为返修工位（指令型返修流程专用） </summary>
        public bool IsRepairStation { get; set; }

        #endregion
    }
}
