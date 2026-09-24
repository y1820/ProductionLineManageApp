namespace ProductionLineManage.Core.Models.Device
{
    /// <summary> 本交互周期内已通过 500 验证、待 800 绑定的物料项 </summary>
    public sealed class ValidatedMaterialItem
    {
        #region ===================== 物料标识 =====================

        /// <summary> 物料 Id </summary>
        public int MaterialId { get; init; }

        /// <summary> 物料编码 </summary>
        public string MaterialCode { get; init; } = string.Empty;

        /// <summary> 物料名称 </summary>
        public string MaterialName { get; init; } = string.Empty;

        /// <summary> 录入顺序 </summary>
        public int Sequence { get; init; }

        #endregion
    }
}
