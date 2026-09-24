namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 物料 - 工位绑定关系（物料加工顺序与合格校验） </summary>
    public class material_Station : BaseEntity
    {
        #region ===================== 关联标识 =====================

        /// <summary> 产品型号 ID </summary>
        public int TypeId { get; set; }

        /// <summary> 产线 ID </summary>
        public int LineId { get; set; }

        /// <summary> 工位 ID </summary>
        public int StationId { get; set; }

        /// <summary> 物料 ID </summary>
        public int MaterialId { get; set; }

        #endregion

        #region ===================== 校验与顺序 =====================

        /// <summary> 查询物料加工合格工位（0 = 不查询） </summary>
        public int CheckMaterialStationId { get; set; }

        /// <summary> 添加顺序（0 = 不判断顺序） </summary>
        public int Sequence { get; set; }

        /// <summary> 父物料 ID（0 = 根物料，用于追溯组合关系） </summary>
        public int ParentMaterialId { get; set; }

        #endregion
    }
}
