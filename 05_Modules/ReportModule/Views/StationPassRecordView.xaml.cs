using System.Windows.Controls;

namespace ReportModule.Views
{
    /// <summary>
    /// 工位过站记录查询视图：按条件查询 report_StationPassRecord，由 StationPassRecordViewModel 驱动。
    /// </summary>
    public partial class StationPassRecordView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化工位过站记录查询 XAML 布局 </summary>
        public StationPassRecordView()
        {
            InitializeComponent(); // 加载 XAML 并绑定 ViewModel
        }

        #endregion
    }
}
