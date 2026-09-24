using ProductionLineManage.Core.Models.DataBase;
using Prism.Mvvm;

namespace DeviceModule.ViewModels
{
    /// <summary>
    /// 地址映射列表展示行：将 device_AddressMapping 与产线/工位名称组合供 DataGrid 绑定（编辑/新增走弹窗）。
    /// </summary>
    public class MappingDisplayItem : BindableBase
    {
        #region ===================== 数据属性 =====================

        /// <summary> 原始数据库映射记录 </summary>
        public device_AddressMapping RawData { get; set; } = new();

        /// <summary> 映射主键 </summary>
        public int Id => RawData.Id;

        /// <summary> 产线名称（由 ViewModel 填充） </summary>
        public string LineName { get; set; } = string.Empty;

        /// <summary> 工位名称（由 ViewModel 填充） </summary>
        public string StationName { get; set; } = string.Empty;

        /// <summary> 数据名称 </summary>
        public string DataName => RawData.DataName;

        /// <summary> PLC 数据地址 </summary>
        public string DataAddress => RawData.DataAddress;

        /// <summary> 数据类型 </summary>
        public string DataType => RawData.DataType;

        /// <summary> 数据长度 </summary>
        public int DataLen => RawData.DataLen;

        /// <summary> 数据方向（读/写） </summary>
        public string DataDirection => RawData.DataDirection;

        /// <summary> 是否启用 </summary>
        public bool IsEnabled => RawData.IsEnabled;

        /// <summary> 备注 </summary>
        public string Remarks => RawData.Remarks ?? string.Empty;

        #endregion
    }
}
