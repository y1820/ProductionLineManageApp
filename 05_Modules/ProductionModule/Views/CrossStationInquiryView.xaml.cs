using System.Windows.Controls;

namespace ProductionModule.Views
{
    /// <summary>
    /// 过站记录查询视图：按产线、工站、产品等条件查询产品过站历史记录。
    /// </summary>
    public partial class CrossStationInquiryView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化过站记录查询视图 </summary>
        public CrossStationInquiryView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
