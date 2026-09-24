// ProductionLineManage.Core/Services/IDeviceLogService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Services.DeviceManager
{
    #region ===================== 日志条目模型 =====================

    /// <summary> 设备日志条目（供 UI 历史事件查询展示） </summary>
    public class DeviceLogEntry
    {
        /// <summary> 时间戳 </summary>
        public DateTime Timestamp { get; set; }

        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        /// <summary> 工位名称 </summary>
        public string StationName { get; set; } = string.Empty;

        /// <summary> 设备编码 </summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary> 日志级别 </summary>
        public string Level { get; set; } = "Info";

        /// <summary> 日志消息 </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary> 指令内容 </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary> 响应内容 </summary>
        public string Response { get; set; } = string.Empty;

        /// <summary> 异常信息 </summary>
        public string Exception { get; set; } = string.Empty;
    }

    #endregion

    /// <summary> 设备日志服务接口：记录与查询工位交互日志 </summary>
    public interface IDeviceLogService
    {
        #region ===================== 日志写入 =====================

        /// <summary> 记录设备日志 </summary>
        void Log(int stationId, string deviceCode, string message, string level = "Info");

        /// <summary> 记录指令交互日志 </summary>
        void LogCommand(int stationId, string deviceCode, string command, string response);

        #endregion

        #region ===================== 日志查询 =====================

        /// <summary> 获取指定日期的日志 </summary>
        Task<List<DeviceLogEntry>> GetLogsAsync(int stationId, DateTime date);

        /// <summary> 获取最新 N 条日志 </summary>
        Task<List<DeviceLogEntry>> GetLatestLogsAsync(int stationId, int count);

        #endregion
    }
}
