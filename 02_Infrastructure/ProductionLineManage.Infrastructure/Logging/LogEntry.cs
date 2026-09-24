// ProductionLineManage.Infrastructure/Logging/LogEntry.cs
using System;

namespace ProductionLineManage.Infrastructure.Logging
{
    /// <summary> 日志条目模型 </summary>
    public class LogEntry
    {
        #region ===================== 基本信息 =====================

        /// <summary> 日志 Id </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary> 时间戳 </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary> 日志级别 </summary>
        public LogLevel Level { get; set; }

        /// <summary> 日志类型/分类 </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary> 消息内容 </summary>
        public string Message { get; set; } = string.Empty;

        #endregion

        #region ===================== 异常与来源 =====================

        /// <summary> 异常信息（如果有） </summary>
        public string? Exception { get; set; }

        /// <summary> 来源（类名/方法名） </summary>
        public string? Source { get; set; }

        #endregion

        #region ===================== 设备关联 =====================

        /// <summary> 关联的设备编码（设备日志用） </summary>
        public string? DeviceCode { get; set; }

        /// <summary> 关联的工位 Id（设备日志用） </summary>
        public int? StationId { get; set; }

        #endregion

        /// <summary> 格式化为可读字符串 </summary>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Exception))
            {
                return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] [{Category}] {Message} | 异常: {Exception}";
            }
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] [{Category}] {Message}";
        }
    }
}
