using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Windows;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 编辑产线弹窗视图模型：修改产线名称与备注，更新 craft_LineInfo。
    /// </summary>
    public class EditLineViewModel : BindableBase, IDialogAware
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化命令并注入产线仓储 </summary>
        public EditLineViewModel(IRepository<craft_LineInfo> repo)
        {
            ClosebtnCommand = new DelegateCommand(OnDialogClosed); // 关闭弹窗
            EditCommand = new DelegateCommand(OnEdit); // 确认修改
            _repo = repo;
        }

        #endregion

        #region ===================== 私有字段 =====================

        /// <summary> 产线数据仓储 </summary>
        private IRepository<craft_LineInfo> _repo;

        private craft_LineInfo _lineInfo = new craft_LineInfo(); // 待编辑的产线实体

        #endregion

        #region ===================== 属性 =====================

        /// <summary> 待编辑的产线信息（双向绑定表单） </summary>
        public craft_LineInfo LineInfo
        {
            get { return _lineInfo; }
            set { SetProperty(ref _lineInfo, value); }
        }

        #endregion

        #region ===================== 命令 =====================

        /// <summary> 关闭弹窗命令 </summary>
        public DelegateCommand ClosebtnCommand { get; set; }

        /// <summary> 确认修改命令 </summary>
        public DelegateCommand EditCommand { get; set; }

        #endregion

        #region ===================== IDialogAware 实现 =====================

        /// <summary> 弹窗标题 </summary>
        public string Title => "编辑产线";

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

        /// <summary> 弹窗打开时从参数加载待编辑产线 </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            var temp = parameters.GetValue<craft_LineInfo>("Line"); // 读取传入实体
            LineInfo = new craft_LineInfo() // 拷贝字段，避免直接修改列表引用
            {
                Id = temp.Id,
                CreateTime = temp.CreateTime,
                Name = temp.Name,
                Remarks = temp.Remarks,
                UpdateTime = temp.UpdateTime
            };
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 校验输入并更新数据库，成功后返回 OK 结果 </summary>
        private void OnEdit()
        {
            if (string.IsNullOrWhiteSpace(LineInfo.Name)) // 名称必填
            {
                HandyControl.Controls.MessageBox.Show("产线名称不能为空", "提示");
                return;
            }
            if (HandyControl.Controls.MessageBox.Show($"确认修改：{LineInfo.Name}",
                "提示", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    if (_repo.Update(LineInfo) == 1) // 更新成功
                    {
                        HandyControl.Controls.MessageBox.Show("修改成功");
                        var parameters = new DialogParameters();
                        parameters.Add("Line", LineInfo); // 回传修改后实体供列表刷新
                        RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                    }
                    else
                    {
                        HandyControl.Controls.MessageBox.Show("数据库更新产线失败");
                    }
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show("数据库更新产线异常：" + ex.Message);
                }
            }
        }

        #endregion
    }
}
