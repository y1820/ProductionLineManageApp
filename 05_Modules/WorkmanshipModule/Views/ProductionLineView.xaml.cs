using System.Windows.Controls;

namespace WorkmanshipModule.Views
{
    /// <summary>
    /// 产线配置视图：展示产线列表，绑定 ProductionLineViewModel。
    /// </summary>
    public partial class ProductionLineView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化产线配置视图 </summary>
        public ProductionLineView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
