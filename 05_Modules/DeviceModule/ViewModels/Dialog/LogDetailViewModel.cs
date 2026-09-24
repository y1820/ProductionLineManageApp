using ProductionLineManage.Core.Services.DeviceManager;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;

namespace DeviceModule.ViewModels.Dialog
{
    /// <summary>
    /// 设备日志详情弹窗视图模型：展示单条 DeviceLogEntry 的命令、响应与异常信息。
    /// </summary>
    public class LogDetailViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================

        private DeviceLogEntry _logEntry = new DeviceLogEntry(); // 当前展示的日志条目

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 初始化关闭命令 </summary>
        public LogDetailViewModel()
        {
            CloseCommand = new DelegateCommand(OnClose); // 绑定关闭按钮
        }

        #endregion

        #region ===================== 公共属性 =====================

        /// <summary> 日志详情数据（由弹窗参数传入） </summary>
        public DeviceLogEntry LogEntry
        {
            get => _logEntry;
            set => SetProperty(ref _logEntry, value); // 更新并通知 UI
        }

        /// <summary> 是否包含命令内容 </summary>
        public bool HasCommand => LogEntry != null && !string.IsNullOrEmpty(LogEntry.Command);

        /// <summary> 是否包含响应内容 </summary>
        public bool HasResponse => LogEntry != null && !string.IsNullOrEmpty(LogEntry.Response);

        /// <summary> 是否包含异常信息 </summary>
        public bool HasException => LogEntry != null && !string.IsNullOrEmpty(LogEntry.Exception);

        /// <summary> 关闭弹窗命令 </summary>
        public DelegateCommand CloseCommand { get; set; }

        #endregion

        #region ===================== IDialogAware =====================

        /// <summary> 弹窗标题 </summary>
        public string Title => "日志详情";

        /// <summary> Prism 关闭弹窗事件 </summary>
        public event Action<IDialogResult>? RequestClose;

        /// <summary> 是否允许关闭弹窗 </summary>
        public bool CanCloseDialog() => true;

        /// <summary> 弹窗关闭后回调（当前直接返回 OK） </summary>
        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK)); // 通知 DialogService 关闭
        }

        /// <summary> 弹窗打开时从参数读取 LogEntry </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            LogEntry = parameters.GetValue<DeviceLogEntry>("LogEntry"); // 解析传入的日志对象
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 用户点击关闭按钮 </summary>
        private void OnClose()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK)); // 以 OK 结果关闭弹窗
        }

        #endregion
    }
}
