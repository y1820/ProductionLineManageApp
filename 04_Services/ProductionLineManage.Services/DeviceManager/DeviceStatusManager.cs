using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DeviceManager;
using System.Collections.Concurrent;

namespace ProductionLineManage.Services.DeviceManager
{
    /// <summary> 设备状态管理器：线程安全维护各工位面向 UI 的展示状态 </summary>
    public class DeviceStatusManager : IDeviceStatusManager
    {
        #region ===================== 私有字段 =====================

        private readonly ConcurrentDictionary<int, DeviceStatus> _statuses = new();

        #endregion

        #region ===================== 状态订阅 =====================

        /// <summary> 状态变更通知（仅 UI 相关字段变化时触发） </summary>
        public event EventHandler<int>? StatusChanged;

        #endregion

        #region ===================== 状态读写 =====================

        /// <summary> 原子更新指定工位状态（由任何线程调用，线程安全） </summary>
        public void UpdateStatus(int stationId, Action<DeviceStatus> updateAction)
        {
            var status = _statuses.GetOrAdd(stationId, _ => new DeviceStatus { StationId = stationId });
            var notify = false;

            lock (status)
            {
                var before = CaptureUiSnapshot(status); // 更新前快照
                updateAction(status);
                status.UpdateTime = DateTime.Now;
                notify = !UiSnapshotEquals(before, CaptureUiSnapshot(status)); // 仅 UI 字段变化时通知
            }

            if (notify)
                StatusChanged?.Invoke(this, stationId);
        }

        /// <summary> 读取单个工位状态 </summary>
        public DeviceStatus? GetStatus(int stationId)
        {
            return _statuses.TryGetValue(stationId, out var status) ? status : null;
        }

        /// <summary> 读取所有工位状态（用于 UI 初始化） </summary>
        public IReadOnlyDictionary<int, DeviceStatus> GetAllStatus()
        {
            return _statuses;
        }

        /// <summary> 快捷更新连接状态 </summary>
        public void UpdateConnectionStatus(int stationId, ConnectionState state)
        {
            UpdateStatus(stationId, status => { status.ConnectionState = state; });
        }

        /// <summary> 快捷更新心跳时间 </summary>
        public void UpdateHeartbeat(int stationId, DateTime time)
        {
            UpdateStatus(stationId, status => status.LastHeartbeat = time);
        }

        /// <summary> 快捷更新错误信息 </summary>
        public void UpdateError(int stationId, string error)
        {
            UpdateStatus(stationId, status => status.LastError = error);
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 捕获 UI 相关字段快照 </summary>
        private static UiSnapshot CaptureUiSnapshot(DeviceStatus status) => new()
        {
            ConnectionState = status.ConnectionState,
            RunState = status.RunState,
            DeviceCode = status.DeviceCode,
            CurrentFlowCode = status.CurrentFlowCode,
            TodayProcessedCount = status.TodayProcessedCount,
            ProductTypeName = status.ProductTypeName,
            WorkOrder = status.WorkOrder,
            DeviceLastCommand = status.DeviceLastCommand,
            DeviceLastCommandTime = status.DeviceLastCommandTime,
            LastCommand = status.LastCommand,
            LastCommandTime = status.LastCommandTime,
            LastHeartbeat = status.LastHeartbeat,
            LastError = status.LastError,
            ReconnectAttempts = status.ReconnectAttempts
        };

        /// <summary> 比较两个 UI 快照是否相等 </summary>
        private static bool UiSnapshotEquals(UiSnapshot a, UiSnapshot b) =>
            a.ConnectionState == b.ConnectionState &&
            a.RunState == b.RunState &&
            a.DeviceCode == b.DeviceCode &&
            a.CurrentFlowCode == b.CurrentFlowCode &&
            a.TodayProcessedCount == b.TodayProcessedCount &&
            a.ProductTypeName == b.ProductTypeName &&
            a.WorkOrder == b.WorkOrder &&
            a.DeviceLastCommand == b.DeviceLastCommand &&
            a.DeviceLastCommandTime == b.DeviceLastCommandTime &&
            a.LastCommand == b.LastCommand &&
            a.LastCommandTime == b.LastCommandTime &&
            a.LastHeartbeat == b.LastHeartbeat &&
            a.LastError == b.LastError &&
            a.ReconnectAttempts == b.ReconnectAttempts;

        /// <summary> UI 展示字段快照（用于变更检测，避免无效通知） </summary>
        private sealed class UiSnapshot
        {
            public ConnectionState ConnectionState { get; init; }
            public RunState RunState { get; init; }
            public string DeviceCode { get; init; } = string.Empty;
            public string CurrentFlowCode { get; init; } = string.Empty;
            public int TodayProcessedCount { get; init; }
            public string ProductTypeName { get; init; } = string.Empty;
            public string WorkOrder { get; init; } = string.Empty;
            public string DeviceLastCommand { get; init; } = string.Empty;
            public DateTime? DeviceLastCommandTime { get; init; }
            public string LastCommand { get; init; } = string.Empty;
            public DateTime? LastCommandTime { get; init; }
            public DateTime? LastHeartbeat { get; init; }
            public string LastError { get; init; } = string.Empty;
            public int ReconnectAttempts { get; init; }
        }

        #endregion
    }
}
