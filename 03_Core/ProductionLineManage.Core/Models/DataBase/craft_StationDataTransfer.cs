namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 工艺 - 工位间数据传递配置 </summary>
    public class craft_StationDataTransfer : BaseEntity
    {
        #region ===================== 请求与源 =====================

        /// <summary> 请求的工位 Id </summary>
        public int RequestStationId { get; set; }

        /// <summary> 需要的数据名称 </summary>
        public string RequestDataName { get; set; } = string.Empty;

        /// <summary> 数据源的工位 ID </summary>
        public int SourceStationId { get; set; }

        /// <summary> 写入请求工位的 PLC 地址（落库字段名 SourceAddress） </summary>
        public string SourceAddress { get; set; } = string.Empty;

        #endregion

        #region ===================== 数据定义 =====================

        /// <summary> 数据类型 </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary> 数据长度 </summary>
        public int DataLength { get; set; }

        #endregion

        #region ===================== 关联与开关 =====================

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 ID </summary>
        public int LineId { get; set; }

        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; }

        #endregion
    }
}
