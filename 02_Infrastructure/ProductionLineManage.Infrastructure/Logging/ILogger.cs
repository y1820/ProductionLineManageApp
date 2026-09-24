// ProductionLineManage.Infrastructure/Logging/ILogger.cs
using System;

namespace ProductionLineManage.Infrastructure.Logging
{
    /// <summary> 日志接口：应用程序日志与设备交互日志 </summary>
    public interface ILogger
    {
        #region ===================== 应用程序日志 =====================

        /// <summary> 调试日志 </summary>
        void Debug(string message, string? source = null);

        /// <summary> 信息日志 </summary>
        void Info(string message, string? source = null);

        /// <summary> 警告日志 </summary>
        void Warning(string message, string? source = null);

        /// <summary> 错误日志 </summary>
        void Error(string message, string? source = null);

        /// <summary> 错误日志（含异常） </summary>
        void Error(Exception ex, string? message = null, string? source = null);

        /// <summary> 致命错误日志 </summary>
        void Fatal(string message, string? source = null);

        /// <summary> 致命错误日志（含异常） </summary>
        void Fatal(Exception ex, string? message = null, string? source = null);

        #endregion

        #region ===================== 设备交互日志 =====================

        /// <summary> 记录设备交互日志 </summary>
        void DeviceLog(int stationId, string deviceCode, string message, LogLevel level = LogLevel.Info);

        /// <summary> 记录设备交互日志（带异常） </summary>
        void DeviceLog(int stationId, string deviceCode, string message, LogLevel level, Exception? ex);

        #endregion
    }
}
