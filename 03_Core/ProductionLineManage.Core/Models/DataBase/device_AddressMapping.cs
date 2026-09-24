namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 设备地址映射表 </summary>
    public class device_AddressMapping : BaseEntity
    {
        #region ===================== 工位与名称 =====================

        /// <summary> 工位 ID </summary>
        public int StationId { get; set; }

        /// <summary> 数据名称 </summary>
        public string DataName { get; set; } = string.Empty;

        #endregion

        #region ===================== 地址与类型 =====================

        /// <summary> 数据地址 </summary>
        public string DataAddress { get; set; } = string.Empty;

        /// <summary> 数据类型 </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary> 数据长度/数量 </summary>
        public int DataLen { get; set; }

        /// <summary> 数据方向（读取、写入、读写） </summary>
        public string DataDirection { get; set; } = string.Empty;

        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; } = true;

        #endregion
    }
}
