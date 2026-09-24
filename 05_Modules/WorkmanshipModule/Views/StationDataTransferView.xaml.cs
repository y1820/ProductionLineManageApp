using System.Windows.Controls;

namespace WorkmanshipModule.Views
{
    /// <summary>
    /// 工位传值配置视图：展示工位间数据传递规则，绑定 StationDataTransferViewModel。
    /// </summary>
    public partial class StationDataTransferView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化工位传值配置视图 </summary>
        public StationDataTransferView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
