// ProductionLineManage.Services/DeviceLogService.cs
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProductionLineManage.Services.DeviceManager
{
    /// <summary> 设备日志服务：记录与查询工位交互日志，供 UI 设备历史事件展示 </summary>
    public class DeviceLogService : IDeviceLogService
    {
        #region ===================== 私有字段 =====================

        private readonly ILogger _logger;
        private readonly string _deviceLogRoot;

        // 日志行解析正则：[时间][级别][工位:Id][设备:Code] 消息
        private static readonly Regex LogRegex = new Regex(
            @"\[(\d{2}:\d{2}:\d{2}\.\d{3})\]\s*\[(\w+)\]\s*\[工位:(\d+)\]\s*\[设备:([^\]]+)\]\s*(.*)",
            RegexOptions.Compiled);

        // 指令响应解析正则：指令: xxx | 响应: yyy
        private static readonly Regex CommandRegex = new Regex(
            @"指令:\s*([^\|]+)\|\s*响应:\s*(.*)",
            RegexOptions.Compiled);

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 初始化设备日志根目录 </summary>
        public DeviceLogService(ILogger logger)
        {
            _logger = logger;
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            _deviceLogRoot = Path.Combine(basePath, "Logs", "Device");
        }

        #endregion

        #region ===================== 日志写入 =====================

        /// <summary> 记录设备日志 </summary>
        public void Log(int stationId, string deviceCode, string message, string level = "Info")
        {
            _logger.DeviceLog(stationId, deviceCode, message,
                level.ToLower() switch
                {
                    "error" => LogLevel.Error,
                    "warning" => LogLevel.Warning,
                    _ => LogLevel.Info
                });
        }

        /// <summary> 记录指令交互日志 </summary>
        public void LogCommand(int stationId, string deviceCode, string command, string response)
        {
            var message = $"指令: {command} | 响应: {response}";
            _logger.DeviceLog(stationId, deviceCode, message, LogLevel.Info);
        }

        #endregion

        #region ===================== 日志查询 =====================

        /// <summary> 获取指定日期的日志 </summary>
        public async Task<List<DeviceLogEntry>> GetLogsAsync(int stationId, DateTime date)
        {
            var result = new List<DeviceLogEntry>();

            // 构建日志文件路径: Logs/Device/{StationId}/{yyyy}/{MM}/{dd}.txt
            var logFilePath = Path.Combine(
                _deviceLogRoot,
                stationId.ToString(),
                date.Year.ToString(),
                date.Month.ToString(),
                $"{date.Day:00}.txt");

            if (!File.Exists(logFilePath))
                return result;

            var lines = await Task.Run(() => File.ReadAllLines(logFilePath));

            foreach (var line in lines)
            {
                var entry = ParseLogLine(line, stationId);
                if (entry != null)
                {
                    result.Add(entry);
                }
            }

            return result.OrderByDescending(e => e.Timestamp).ToList(); // 按时间倒序
        }

        /// <summary> 获取最新 N 条日志 </summary>
        public async Task<List<DeviceLogEntry>> GetLatestLogsAsync(int stationId, int count)
        {
            var result = new List<DeviceLogEntry>();
            var stationDir = Path.Combine(_deviceLogRoot, stationId.ToString());

            if (!Directory.Exists(stationDir))
                return result;

            // 获取所有日志文件（按时间倒序）
            var allFiles = new List<string>();
            foreach (var yearDir in Directory.GetDirectories(stationDir).OrderByDescending(d => d))
            {
                foreach (var monthDir in Directory.GetDirectories(yearDir).OrderByDescending(d => d))
                {
                    var files = Directory.GetFiles(monthDir, "*.txt").OrderByDescending(f => f);
                    allFiles.AddRange(files);
                }
            }

            // 从最新文件开始读取，直到达到指定数量
            foreach (var file in allFiles)
            {
                if (result.Count >= count) break;

                var lines = await Task.Run(() => File.ReadAllLines(file));
                for (int i = lines.Length - 1; i >= 0 && result.Count < count; i--)
                {
                    var entry = ParseLogLine(lines[i], stationId);
                    if (entry != null)
                    {
                        result.Add(entry);
                    }
                }
            }

            return result;
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 解析单行日志为 DeviceLogEntry </summary>
        private DeviceLogEntry ParseLogLine(string line, int stationId)
        {
            var match = LogRegex.Match(line);
            if (!match.Success) return null;

            var timeStr = match.Groups[1].Value;
            var level = match.Groups[2].Value;
            var logStationId = int.Parse(match.Groups[3].Value);
            var deviceCode = match.Groups[4].Value;
            var message = match.Groups[5].Value;

            if (logStationId != stationId) return null; // 过滤非目标工位

            var entry = new DeviceLogEntry
            {
                Timestamp = DateTime.Today.Add(TimeSpan.Parse(timeStr)),
                StationId = stationId,
                Level = level,
                DeviceCode = deviceCode,
                Message = message
            };

            // 尝试解析指令和响应
            var cmdMatch = CommandRegex.Match(message);
            if (cmdMatch.Success)
            {
                entry.Command = cmdMatch.Groups[1].Value.Trim();
                entry.Response = cmdMatch.Groups[2].Value.Trim();
            }

            return entry;
        }

        #endregion
    }
}
