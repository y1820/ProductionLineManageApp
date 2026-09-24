// ProductionLineManage.Infrastructure/Logging/FileLogger.cs
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProductionLineManage.Infrastructure.Logging
{
    /// <summary> 文件日志实现：应用程序日志与按工位组织的设备交互日志 </summary>
    public class FileLogger : ILogger
    {
        #region ===================== 私有字段 =====================

        private readonly string _appLogPath;      // 应用程序日志根路径
        private readonly string _deviceLogRoot;   // 设备日志根路径
        private readonly object _lockObj = new object();
        private readonly int _maxRetentionDays = 30; // 日志保留天数

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 初始化日志路径并启动过期日志清理 </summary>
        public FileLogger(IConfiguration configuration)
        {
            var baseLogPath = configuration["Logging:LogPath"] ??
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            _appLogPath = Path.Combine(baseLogPath, "Application");
            _deviceLogRoot = Path.Combine(baseLogPath, "Device");

            EnsureDirectoryExists(_appLogPath);   // 确保应用日志目录存在
            EnsureDirectoryExists(_deviceLogRoot); // 确保设备日志目录存在

            Task.Run(() => CleanOldLogs()); // 异步清理过期日志
        }

        #endregion

        #region ===================== 应用程序日志 =====================

        /// <summary> 调试日志 </summary>
        public void Debug(string message, string? source = null)
        {
            WriteAppLog(LogLevel.Debug, message, source);
        }

        /// <summary> 信息日志 </summary>
        public void Info(string message, string? source = null)
        {
            WriteAppLog(LogLevel.Info, message, source);
        }

        /// <summary> 警告日志 </summary>
        public void Warning(string message, string? source = null)
        {
            WriteAppLog(LogLevel.Warning, message, source);
        }

        /// <summary> 错误日志 </summary>
        public void Error(string message, string? source = null)
        {
            WriteAppLog(LogLevel.Error, message, source);
        }

        /// <summary> 错误日志（含异常） </summary>
        public void Error(Exception ex, string? message = null, string? source = null)
        {
            var fullMessage = string.IsNullOrEmpty(message) ? ex.Message : $"{message} | {ex.Message}";
            WriteAppLog(LogLevel.Error, fullMessage, source, ex.ToString());
        }

        /// <summary> 致命错误日志 </summary>
        public void Fatal(string message, string? source = null)
        {
            WriteAppLog(LogLevel.Fatal, message, source);
        }

        /// <summary> 致命错误日志（含异常） </summary>
        public void Fatal(Exception ex, string? message = null, string? source = null)
        {
            var fullMessage = string.IsNullOrEmpty(message) ? ex.Message : $"{message} | {ex.Message}";
            WriteAppLog(LogLevel.Fatal, fullMessage, source, ex.ToString());
        }

        #endregion

        #region ===================== 设备日志 =====================

        /// <summary> 记录设备交互日志（目录：Device/{StationId}/{yyyy}/{MM}/{dd}.txt） </summary>
        public void DeviceLog(int stationId, string deviceCode, string message, LogLevel level = LogLevel.Info)
        {
            DeviceLog(stationId, deviceCode, message, level, null);
        }

        /// <summary> 记录设备交互日志（带异常） </summary>
        public void DeviceLog(int stationId, string deviceCode, string message, LogLevel level, Exception? ex)
        {
            var now = DateTime.Now;

            // 构建目录路径：Device/{StationId}/{yyyy}/{MM}/
            var stationDir = Path.Combine(_deviceLogRoot, stationId.ToString());
            var yearDir = Path.Combine(stationDir, now.Year.ToString());
            var monthDir = Path.Combine(yearDir, now.Month.ToString());

            EnsureDirectoryExists(monthDir);

            var logFilePath = Path.Combine(monthDir, $"{now.Day:00}.txt"); // 日志文件名：dd.txt
            var logLine = FormatDeviceLogEntry(now, stationId, deviceCode, message, level, ex);
            Task.Run(() => WriteToFileAsync(logFilePath, logLine)); // 异步写入
        }

        /// <summary> 格式化设备日志条目 </summary>
        private string FormatDeviceLogEntry(DateTime timestamp, int stationId, string deviceCode,
            string message, LogLevel level, Exception? ex)
        {
            var sb = new StringBuilder();
            sb.Append($"[{timestamp:HH:mm:ss.fff}]");      // 时间（精确到毫秒）
            sb.Append($"[{level}]");                        // 日志级别
            sb.Append($"[工位:{stationId}]");               // 工位 Id
            sb.Append($"[设备:{deviceCode}]");              // 设备编码
            sb.Append($" {message}");                       // 消息内容

            if (ex != null)
            {
                sb.Append($" | 异常: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sb.Append($" | 堆栈: {ex.StackTrace}");
                }
            }

            return sb.ToString();
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 写入应用程序日志 </summary>
        private void WriteAppLog(LogLevel level, string message, string? source = null, string? exception = null)
        {
            var now = DateTime.Now;
            var logFilePath = Path.Combine(_appLogPath, $"{now:yyyy-MM-dd}.log");

            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var sourceInfo = string.IsNullOrEmpty(source) ? "" : $"[{source}]";
            var exceptionInfo = string.IsNullOrEmpty(exception) ? "" : $" | 异常详情: {exception}";
            var logLine = $"[{timestamp}] [{level}]{sourceInfo} {message}{exceptionInfo}";

            Task.Run(() => WriteToFileAsync(logFilePath, logLine));
        }

        /// <summary> 写入文件（带重试机制） </summary>
        private async Task WriteToFileAsync(string filePath, string content, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    lock (_lockObj)
                    {
                        File.AppendAllText(filePath, content + Environment.NewLine, Encoding.UTF8);
                    }
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    await Task.Delay(50 * (i + 1)); // 文件被占用时退避重试
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"日志写入失败: {ex.Message}");
                    return;
                }
            }
        }

        /// <summary> 清理过期日志 </summary>
        private void CleanOldLogs()
        {
            try
            {
                CleanDirectoryByDate(_appLogPath);  // 清理应用程序日志
                CleanDeviceLogs(_deviceLogRoot);    // 清理设备日志
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理日志失败: {ex.Message}");
            }
        }

        /// <summary> 按文件修改时间清理应用日志目录 </summary>
        private void CleanDirectoryByDate(string path)
        {
            if (!Directory.Exists(path)) return;

            var cutoffDate = DateTime.Now.AddDays(-_maxRetentionDays);
            var oldFiles = Directory.GetFiles(path, "*.log")
                .Where(f => File.GetLastWriteTime(f) < cutoffDate);

            foreach (var file in oldFiles)
            {
                try { File.Delete(file); }
                catch { }
            }
        }

        /// <summary> 清理过期的设备日志（按文件夹日期结构） </summary>
        private void CleanDeviceLogs(string rootPath)
        {
            if (!Directory.Exists(rootPath)) return;

            var cutoffDate = DateTime.Now.AddDays(-_maxRetentionDays);

            foreach (var stationDir in Directory.GetDirectories(rootPath)) // 遍历工位文件夹
            {
                foreach (var yearDir in Directory.GetDirectories(stationDir)) // 遍历年份文件夹
                {
                    if (!int.TryParse(Path.GetFileName(yearDir), out int year)) continue;

                    foreach (var monthDir in Directory.GetDirectories(yearDir)) // 遍历月份文件夹
                    {
                        if (!int.TryParse(Path.GetFileName(monthDir), out int month)) continue;

                        var folderDate = new DateTime(year, month, 1);

                        if (folderDate.AddMonths(1) < cutoffDate) // 整月过期则删除文件夹
                        {
                            try { Directory.Delete(monthDir, true); }
                            catch { }
                        }
                        else
                        {
                            foreach (var logFile in Directory.GetFiles(monthDir, "*.txt"))
                            {
                                var fileName = Path.GetFileNameWithoutExtension(logFile);
                                if (int.TryParse(fileName, out int day))
                                {
                                    var fileDate = new DateTime(year, month, day);
                                    if (fileDate < cutoffDate)
                                    {
                                        try { File.Delete(logFile); }
                                        catch { }
                                    }
                                }
                            }

                            if (Directory.GetFileSystemEntries(monthDir).Length == 0) // 空月份文件夹
                            {
                                try { Directory.Delete(monthDir); }
                                catch { }
                            }
                        }
                    }

                    if (Directory.GetDirectories(yearDir).Length == 0) // 空年份文件夹
                    {
                        try { Directory.Delete(yearDir); }
                        catch { }
                    }
                }

                if (Directory.GetDirectories(stationDir).Length == 0 &&
                    Directory.GetFiles(stationDir).Length == 0) // 空工位文件夹
                {
                    try { Directory.Delete(stationDir); }
                    catch { }
                }
            }
        }

        /// <summary> 确保目录存在 </summary>
        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        #endregion
    }
}
