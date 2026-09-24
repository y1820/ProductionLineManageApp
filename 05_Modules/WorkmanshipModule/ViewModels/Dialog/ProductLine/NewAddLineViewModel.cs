using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Windows;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 新增产线弹窗视图模型：录入产线名称与备注，写入 craft_LineInfo。
    /// </summary>
    public class NewAddLineViewModel : BindableBase, IDialogAware
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化命令并注入产线仓储 </summary>
        public NewAddLineViewModel(IRepository<craft_LineInfo> repo)
        {
            ClosebtnCommand = new DelegateCommand(OnDialogClosed); // 关闭弹窗
            NewAddbtnCommand = new DelegateCommand(OnNewAdd); // 确认新增
            _repo = repo;
        }

        #endregion

        #region ===================== 私有字段 =====================

        /// <summary> 产线数据仓储 </summary>
        private IRepository<craft_LineInfo> _repo;

        private craft_LineInfo _lineInfo = new craft_LineInfo(); // 待新增的产线实体

        #endregion

        #region ===================== 属性 =====================

        /// <summary> 待新增的产线信息（双向绑定表单） </summary>
        public craft_LineInfo LineInfo
        {
            get { return _lineInfo; }
            set { SetProperty(ref _lineInfo, value); }
        }

        #endregion

        #region ===================== 命令 =====================

        /// <summary> 关闭弹窗命令 </summary>
        public DelegateCommand ClosebtnCommand { get; set; }

        /// <summary> 确认新增命令 </summary>
        public DelegateCommand NewAddbtnCommand { get; set; }

        #endregion

        #region ===================== IDialogAware 实现 =====================

        /// <summary> 弹窗标题 </summary>
        public string Title => "新增产线";

        /// <summary> 请求关闭弹窗事件 </summary>
        public event Action<IDialogResult>? RequestClose;

        /// <summary> 是否允许关闭弹窗 </summary>
        public bool CanCloseDialog()
        {
            return true;
        }

        /// <summary> 取消并关闭弹窗 </summary>
        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)); // 返回 Cancel 结果
        }

        /// <summary> 弹窗打开回调（新增场景无需预填参数） </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 校验输入并写入数据库，成功后返回 OK 结果 </summary>
        private void OnNewAdd()
        {
            if (string.IsNullOrWhiteSpace(LineInfo.Name)) // 名称必填
            {
                HandyControl.Controls.MessageBox.Show("产线名称不能为空", "提示");
                return;
            }
            if (HandyControl.Controls.MessageBox.Show($"确认新增：{LineInfo.Name}",
                "提示", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    LineInfo.Id = _repo.Insert(LineInfo); // 写入数据库并获取自增 Id
                    HandyControl.Controls.MessageBox.Show("新增成功");
                    var parameters = new DialogParameters();
                    parameters.Add("Line", LineInfo); // 回传新增实体供列表刷新
                    RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("写入产线到数据库异常：" + ex.Message);
                }
            }
        }

        #endregion
    }
}
