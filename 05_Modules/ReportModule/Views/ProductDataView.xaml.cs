using System.Windows.Controls;
using Prism.Regions;

namespace ReportModule.Views
{
    /// <summary>
    /// 产品加工数据查询视图：按条件查询 report_ProcessHistory 并支持导出，由 ProductDataViewModel 驱动。
    /// </summary>
    public partial class ProductDataView : UserControl, IRegionMemberLifetime
    {
        #region ===================== 区域生命周期 =====================

        /// <summary> 切换菜单后保留当前查询结果，不重建视图 </summary>
        public bool KeepAlive => true;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 初始化产品加工数据查询 XAML 布局 </summary>
        public ProductDataView()
        {
            InitializeComponent(); // 加载 XAML 并绑定 ProductDataViewModel
        }

        #endregion
    }
}
