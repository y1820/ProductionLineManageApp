using System.Windows.Controls;

namespace WorkmanshipModule.Views
{
    /// <summary>
    /// 工位配置视图：展示工位列表，绑定 StationConfigViewModel。
    /// </summary>
    public partial class StationConfigView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化工位配置视图 </summary>
        public StationConfigView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
