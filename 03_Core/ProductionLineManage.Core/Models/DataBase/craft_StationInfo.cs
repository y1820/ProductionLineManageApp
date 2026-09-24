namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 工艺 - 工位基础信息 </summary>
    public class craft_StationInfo : BaseEntity
    {
        #region ===================== 工位标识 =====================

        /// <summary> 工位代号 </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary> 工位名称 </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary> 所属产线 ID </summary>
        public int LineId { get; set; }

        #endregion

        #region ===================== UI 辅助 =====================

        /// <summary> 代号与名称组合（下拉显示用） </summary>
        public string DisplayText => $"{Code} - {Name}";

        #endregion
    }
}
