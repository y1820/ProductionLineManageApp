using System.Windows.Controls;
using Prism.Regions;

namespace ReportModule.Views
{
    /// <summary>
    /// 物料绑定记录查询视图：查询产品物料绑定/解绑历史，由 MaterialBindViewModel 驱动。
    /// </summary>
    public partial class MaterialBindView : UserControl, IRegionMemberLifetime
    {
        #region ===================== 区域生命周期 =====================

        /// <summary> 切换菜单后保留当前查询结果，不重建视图 </summary>
        public bool KeepAlive => true;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 初始化物料绑定记录查询 XAML 布局 </summary>
        public MaterialBindView()
        {
            InitializeComponent(); // 加载 XAML 并绑定 MaterialBindViewModel
        }

        #endregion
    }
}
