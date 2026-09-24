using ProductionLineManage.Core.Models.DataBase;
using Prism.Mvvm;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>
    /// 工位传值配置列表展示行：组合 craft_StationDataTransfer 与工位/型号/产线名称。
    /// </summary>
    public class StationDataTransferDisplayItem : BindableBase
    {
        #region ===================== 数据属性 =====================

        /// <summary> 原始传值配置记录 </summary>
        public craft_StationDataTransfer RawData { get; set; } = new();

        /// <summary> 配置主键 </summary>
        public int Id => RawData.Id;

        /// <summary> 产线名称 </summary>
        public string LineName { get; set; } = string.Empty;

        /// <summary> 产品型号名称 </summary>
        public string ProductTypeName { get; set; } = string.Empty;

        /// <summary> 请求工位名称 </summary>
        public string RequestStationName { get; set; } = string.Empty;

        /// <summary> 源工位名称 </summary>
        public string SourceStationName { get; set; } = string.Empty;

        /// <summary> 请求数据名称 </summary>
        public string RequestDataName => RawData.RequestDataName;

        /// <summary> 目标 PLC 地址 </summary>
        public string TargetAddress => RawData.SourceAddress;

        /// <summary> 数据类型 </summary>
        public string DataType => RawData.DataType;

        /// <summary> 数据长度 </summary>
        public int DataLength => RawData.DataLength;

        /// <summary> 是否启用 </summary>
        public bool IsEnabled => RawData.IsEnabled;

        /// <summary> 备注 </summary>
        public string Remarks => RawData.Remarks ?? string.Empty;

        #endregion
    }
}
