using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Linq;
using System.Windows;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 编辑型号弹窗视图模型：修改型号名称与下发代号，更新 craft_TypeInfo。
    /// </summary>
    public class EditTypeViewModel : BindableBase, IDialogAware
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化命令并注入型号仓储 </summary>
        public EditTypeViewModel(IRepository<craft_TypeInfo> repo)
        {
            ClosebtnCommand = new DelegateCommand(OnDialogClosed); // 关闭弹窗
            EditCommand = new DelegateCommand(OnEdite); // 确认修改
            _repo = repo;
        }

        #endregion

        #region ===================== 私有字段 =====================

        /// <summary> 型号数据仓储 </summary>
        private IRepository<craft_TypeInfo> _repo;

        private craft_TypeInfo _typeInfo = new craft_TypeInfo(); // 待编辑的型号实体

        #endregion

        #region ===================== 属性 =====================

        /// <summary> 待编辑的型号信息（双向绑定表单） </summary>
        public craft_TypeInfo TypeInfo
        {
            get { return _typeInfo; }
            set { SetProperty(ref _typeInfo, value); }
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
        public string Title => "编辑型号";

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

        /// <summary> 弹窗打开时从参数加载待编辑型号 </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            var temp = parameters.GetValue<craft_TypeInfo>("Type"); // 读取传入实体
            TypeInfo = new craft_TypeInfo() // 拷贝字段，避免直接修改列表引用
            {
                Id = temp.Id,
                CreateTime = temp.CreateTime,
                Name = temp.Name,
                IssueCode = temp.IssueCode,
                Remarks = temp.Remarks,
                UpdateTime = temp.UpdateTime
            };
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 校验输入并更新数据库，成功后返回 OK 结果 </summary>
        private void OnEdite()
        {
            if (string.IsNullOrWhiteSpace(TypeInfo.Name)) // 名称必填
            {
                HandyControl.Controls.MessageBox.Show("型号名称不能为空", "提示");
                return;
            }
            if (TypeInfo.IssueCode <= 0) // 下发代号必须为正整数
            {
                HandyControl.Controls.MessageBox.Show("下发型号代号必须大于 0", "提示");
                return;
            }
            if (_repo.GetAll().Any(t => t.IssueCode == TypeInfo.IssueCode && t.Id != TypeInfo.Id)) // 代号唯一性（排除自身）
            {
                HandyControl.Controls.MessageBox.Show($"下发型号代号 {TypeInfo.IssueCode} 已存在，请更换", "提示");
                return;
            }
            if (HandyControl.Controls.MessageBox.Show($"确认修改：{TypeInfo.Name}（代号 {TypeInfo.IssueCode}）",
                "提示", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    if (_repo.Update(TypeInfo) == 1) // 更新成功
                    {
                        HandyControl.Controls.MessageBox.Show("修改成功");
                        var parameters = new DialogParameters();
                        parameters.Add("Type", TypeInfo); // 回传修改后实体供列表刷新
                        RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                    }
                    else
                    {
                        HandyControl.Controls.MessageBox.Show("数据库更新型号失败");
                    }
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show("数据库更新型号异常：" + ex.Message);
                }
            }
        }

        #endregion
    }
}
