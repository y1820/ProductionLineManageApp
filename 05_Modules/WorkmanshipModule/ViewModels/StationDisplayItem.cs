using ProductionLineManage.Core.Models.DataBase;
using Prism.Mvvm;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>
    /// 工位列表展示行：将 craft_StationInfo 与产线名称组合供 DataGrid 绑定（编辑/新增走弹窗）。
    /// </summary>
    public class StationDisplayItem : BindableBase
    {
        #region ===================== 数据属性 =====================

        /// <summary> 原始工位数据库记录 </summary>
        public craft_StationInfo RawData { get; set; } = new();

        /// <summary> 工位主键 </summary>
        public int Id => RawData.Id;

        /// <summary> 工位编码 </summary>
        public string Code => RawData.Code;

        /// <summary> 工位名称 </summary>
        public string Name => RawData.Name;

        /// <summary> 所属产线名称（由 ViewModel 填充） </summary>
        public string LineName { get; set; } = string.Empty;

        /// <summary> 备注 </summary>
        public string Remarks => RawData.Remarks ?? string.Empty;

        /// <summary> 创建时间 </summary>
        public DateTime? CreateTime => RawData.CreateTime;

        /// <summary> 更新时间 </summary>
        public DateTime? UpdateTime => RawData.UpdateTime;

        #endregion
    }
}
