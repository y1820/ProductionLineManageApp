// ProductionLineManage.Core/Models/DeviceStatus.cs
using ProductionLineManage.Core.Enums;
using System;

namespace ProductionLineManage.Core.Models.Device
{
    /// <summary> 设备实时状态（面向看板/监控 UI 展示） </summary>
    public class DeviceStatus
    {
        #region ===================== 工位标识 =====================

        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        /// <summary> 工位名称 </summary>
        public string StationName { get; set; } = string.Empty;

        /// <summary> 设备编码 </summary>
        public string DeviceCode { get; set; } = string.Empty;

        #endregion

        #region ===================== 运行状态 =====================

        /// <summary> 连接状态 </summary>
        public ConnectionState ConnectionState { get; set; } = ConnectionState.Disconnected;

        /// <summary> 运行状态 </summary>
        public RunState RunState { get; set; } = RunState.Stopped;

        /// <summary> 当前流水码 </summary>
        public string CurrentFlowCode { get; set; } = "--";

        /// <summary> 今日产量 </summary>
        public int TodayProcessedCount { get; set; }

        /// <summary> 产品型号名称 </summary>
        public string ProductTypeName { get; set; } = "--";

        /// <summary> 工单号 </summary>
        public string WorkOrder { get; set; } = "--";
        /// <summary> 托盘号 </summary>
        public string TrayCode {  get; set; } = "--";

        #endregion

        #region ===================== 指令与心跳 =====================

        /// <summary> 设备最后指令 </summary>
        public string DeviceLastCommand { get; set; } = "--";

        /// <summary> 设备最后指令时间 </summary>
        public DateTime? DeviceLastCommandTime { get; set; }

        /// <summary> 软件侧最后指令 </summary>
        public string LastCommand { get; set; } = "--";

        /// <summary> 软件侧最后指令时间 </summary>
        public DateTime? LastCommandTime { get; set; }

        /// <summary> 最后心跳时间 </summary>
        public DateTime? LastHeartbeat { get; set; }

        #endregion

        #region ===================== 异常与元数据 =====================

        /// <summary> 最后错误信息 </summary>
        public string LastError { get; set; } = string.Empty;

        /// <summary> 重连次数 </summary>
        public int ReconnectAttempts { get; set; }

        /// <summary> 状态更新时间 </summary>
        public DateTime UpdateTime { get; set; }

        #endregion
    }
}
