using System.Windows.Controls;

namespace WorkmanshipModule.Views
{
    /// <summary>
    /// 数据采集配置视图：展示工位采集地址与数据项，绑定 DataCollectConfigViewModel。
    /// </summary>
    public partial class DataCollectConfigView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化数据采集配置视图 </summary>
        public DataCollectConfigView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
