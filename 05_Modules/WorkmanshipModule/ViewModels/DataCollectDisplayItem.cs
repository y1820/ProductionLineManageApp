using ProductionLineManage.Core.Models.DataBase;
using Prism.Mvvm;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>
    /// 加工数据采集配置列表展示行：组合 craft_DataCollectConfig 与工位/型号/产线名称（编辑/新增走弹窗）。
    /// </summary>
    public class DataCollectDisplayItem : BindableBase
    {
        #region ===================== 数据属性 =====================

        /// <summary> 原始采集配置记录 </summary>
        public craft_DataCollectConfig RawData { get; set; } = new();

        /// <summary> 配置主键 </summary>
        public int Id => RawData.Id;

        /// <summary> 工位名称 </summary>
        public string StationName { get; set; } = string.Empty;

        /// <summary> 产品型号名称 </summary>
        public string ProductTypeName { get; set; } = string.Empty;

        /// <summary> 产线名称 </summary>
        public string LineName { get; set; } = string.Empty;

        /// <summary> 数据项名称 </summary>
        public string DataName => RawData.DataName;

        /// <summary> PLC 地址 </summary>
        public string Address => RawData.Address;

        /// <summary> 数据类型 </summary>
        public string DataType => RawData.DataType;

        /// <summary> 数据长度 </summary>
        public int DataLength => RawData.DataLength;

        /// <summary> 数据单位 </summary>
        public string DataUnit => RawData.DataUnit;

        /// <summary> 是否启用 </summary>
        public bool IsEnabled => RawData.IsEnabled;

        /// <summary> 备注 </summary>
        public string Remarks => RawData.Remarks ?? string.Empty;

        #endregion
    }
}
