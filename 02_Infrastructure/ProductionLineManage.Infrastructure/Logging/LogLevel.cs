using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Infrastructure.Logging
{
    /// <summary> 日志级别枚举，用于区分输出严重程度 </summary>
    public enum LogLevel
    {
        #region ===================== 枚举值 =====================

        /// <summary> 调试信息，开发诊断用 </summary>
        Debug,

        /// <summary> 普通信息，常规运行记录 </summary>
        Info,

        /// <summary> 警告，潜在问题但不影响主流程 </summary>
        Warning,

        /// <summary> 错误，操作失败或异常 </summary>
        Error,

        /// <summary> 致命错误，可能导致进程终止 </summary>
        Fatal

        #endregion
    }
}
