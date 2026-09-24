namespace ProductionLineManage.Core.Models.Device
{
    /// <summary> 保存数据的格式（工艺数据采集项，8000 指令落库用） </summary>
    public class DataSaveFormat
    {
        #region ===================== 采集项字段 =====================

        /// <summary> 数据名称 </summary>
        public string DataName { get; set; } = "null";

        /// <summary> 数据地址 </summary>
        public string DataAddress { get; set; } = "null";

        /// <summary> 数据值 </summary>
        public string DataValue { get; set; } = "null";

        /// <summary> 数据单位 </summary>
        public string DataUnit { get; set; } = "null";

        /// <summary> 上限 </summary>
        public string UpperLimit { get; set; } = "null";

        /// <summary> 下限 </summary>
        public string LowerLimit { get; set; } = "null";

        #endregion
    }
}
