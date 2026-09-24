using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Host.SetupModule.ViewModels
{
    /// <summary>
    /// 登录弹窗 ViewModel（启动流程第 1 步）。
    /// 实现 IDialogAware，由 MainViewModel.ShowLoginDialog 通过 Prism 打开。
    /// 关闭结果：OK=进入数据加载，Cancel=请求退出应用。
    /// </summary>
    public class LoginViewModel : BindableBase, IDialogAware
    {
        #region ===================== 构造 =====================

        /// <summary> 绑定登录、关闭、创建用户等按钮命令 </summary>
        public LoginViewModel()
        {
            LoginCommand = new DelegateCommand(DoLogin);
            ClosebtnCommand = new DelegateCommand(CloseDialog);
            CreateUserCommand = new DelegateCommand(DoCreateUser);
        }

        #endregion

        #region ===================== IDialogAware =====================

        /// <summary>弹窗标题栏文字</summary>
        public string Title => "SCADA系统登录";

        /// <summary>Prism 关闭弹窗时触发，由 DialogService 监听</summary>
        public event Action<IDialogResult>? RequestClose;

        /// <summary>是否允许关闭弹窗（登录阶段始终允许）</summary>
        public bool CanCloseDialog()
        {
            return true;
        }

        /// <summary>弹窗完全关闭后的清理（当前无额外逻辑）</summary>
        public void OnDialogClosed()
        {
        }

        /// <summary>
        /// 弹窗打开时触发（Prism 在 ShowDialog 后调用）。
        /// 启动检查更新动画，模拟启动等待。
        /// </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            StartCheckUpdate();//检查更新
            //获取本地文件登录信息


        }

        #endregion

        #region ===================== 私有字段 =====================

        /// <summary>背景模糊度（检查更新时 10，完成后 0）</summary>
        private double _blur = 10;
        /// <summary>是否显示检查更新加载动画</summary>
        private bool _isShowCheck;
        /// <summary>是否自动登录（勾选后联动记住密码）</summary>
        private bool _isAutoLoad;
        /// <summary>是否记住密码</summary>
        private bool _isRememberPwd;

        #endregion

        #region ===================== 公共属性 =====================

        /// <summary>创建用户命令（待实现）</summary>
        public DelegateCommand CreateUserCommand { get; set; }
        /// <summary>右上角关闭 / 取消按钮</summary>
        public DelegateCommand ClosebtnCommand { get; set; }
        /// <summary>登录按钮</summary>
        public DelegateCommand LoginCommand { get; set; }

        /// <summary>背景模糊度，绑定到 UI 毛玻璃效果</summary>
        public double Blur
        {
            get { return _blur; }
            set { SetProperty(ref _blur, value); }
        }

        /// <summary>历史登录用户名列表（下拉选择）</summary>
        public List<string> UserNames { get; set; } = new List<string>();

        /// <summary>手动输入的用户名</summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>下拉框选中的历史用户</summary>
        public string UserNameSelectedItem { get; set; } = string.Empty;

        /// <summary>密码（绑定到 PasswordBox）</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>是否记住密码；取消时联动关闭自动登录</summary>
        public bool IsRememberPwd
        {
            get => _isRememberPwd;
            set
            {
                SetProperty(ref _isRememberPwd, value);
                if (!_isRememberPwd)
                {
                    IsAutoLogin = false;
                    RaisePropertyChanged(nameof(IsAutoLogin));
                }
            }
        }

        /// <summary>是否自动登录；开启时联动开启记住密码</summary>
        public bool IsAutoLogin
        {
            get => _isAutoLoad;
            set
            {
                SetProperty(ref _isAutoLoad, value);
                if (_isAutoLoad)
                {
                    IsRememberPwd = true;
                    RaisePropertyChanged(nameof(IsRememberPwd));
                }
            }
        }

        /// <summary>错误提示信息（如检查更新失败）</summary>
        public string ErrorInfo { get; set; } = string.Empty;

        /// <summary>检查更新加载动画可见性</summary>
        public bool IsShowCheck
        {
            get { return _isShowCheck; }
            set { SetProperty(ref _isShowCheck, value); }
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary>
        /// 弹窗打开时在后台模拟检查更新（约 1 秒），期间显示加载动画。
        /// </summary>
        private void StartCheckUpdate()
        {
            // 1. 显示检查更新遮罩
            IsShowCheck = true;
            // 2. 后台线程执行，避免阻塞 UI
            Task.Run(async () =>
            {
                try
                {
                    // 模拟网络请求耗时
                    await Task.Delay(1000);
                    // 预留：请求服务端获取最新文件列表
                    //var files_server = fileService.GetUpgradeFiles().ToList();
                }
                catch (Exception ex)
                {
                    // 更新检查失败时显示错误
                    ErrorInfo = ex.Message;
                }
                finally
                {
                    // 3. 恢复界面：去掉模糊、隐藏加载动画
                    Blur = 0;
                    IsShowCheck = false;
                }
            });
        }

        /// <summary>
        /// 登录按钮：取用户名，以 ButtonResult.OK 关闭弹窗。
        /// MainViewModel 回调收到 OK 后进入 ShowDataLoadDialog。
        /// </summary>
        private void DoLogin()
        {
            // 优先使用下拉选中的历史用户，否则用手动输入
            var userName = !string.IsNullOrWhiteSpace(UserNameSelectedItem)
                ? UserNameSelectedItem
                : UserName;
            // 将用户名带回 MainViewModel
            var parameters = new DialogParameters { { "UserName", userName } };
            // 通知 Prism 关闭弹窗，结果为 OK
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
        }

        /// <summary>
        /// 关闭按钮：以 ButtonResult.Cancel 关闭弹窗。
        /// MainViewModel 回调收到 Cancel 后 RequestShutdown(true)。
        /// </summary>
        private void CloseDialog()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        /// <summary>创建用户（待实现）</summary>
        private void DoCreateUser()
        {
        }

        #endregion

        #region ===================== 本地登录记录（预留） =====================

        // 预留：从本地文件读取历史用户名与记住密码

        #endregion
    }
}
