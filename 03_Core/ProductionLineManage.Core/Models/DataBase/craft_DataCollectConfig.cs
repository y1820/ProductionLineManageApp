namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 工位加工数据存储地址配置 </summary>
    public class craft_DataCollectConfig : BaseEntity
    {
        #region ===================== 关联标识 =====================

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 ID </summary>
        public int LineId { get; set; }

        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        #endregion

        #region ===================== 数据项定义 =====================

        /// <summary> 数据名称 </summary>
        public string DataName { get; set; } = string.Empty;

        /// <summary> 数据单位 </summary>
        public string DataUnit { get; set; } = string.Empty;

        /// <summary> 数据地址 </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary> 数据类型 </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary> 数据长度 </summary>
        public int DataLength { get; set; }

        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; }

        #endregion
    }
}
