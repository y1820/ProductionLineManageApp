using System.Windows.Controls;

namespace WorkmanshipModule.Views
{
    /// <summary>
    /// 工艺流程视图：展示工艺流程列表，绑定 ProcessFlowViewModel。
    /// </summary>
    public partial class ProcessFlowView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化工艺流程视图 </summary>
        public ProcessFlowView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
