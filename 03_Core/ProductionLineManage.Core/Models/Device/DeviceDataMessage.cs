using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Models.Device
{
    #region ===================== 连接层 → 业务层 =====================

    /// <summary> 设备数据消息（连接层采集/订阅后转发至业务层） </summary>
    public class DeviceDataMessage
    {
        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        /// <summary> 数据类型（如 FlowCode、Material、SaveData） </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary> 数据载荷 </summary>
        public object? Data { get; set; }

        /// <summary> 消息时间戳 </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    #endregion

    #region ===================== 业务层 → 连接层 =====================

    /// <summary> 业务响应（业务处理器执行结果，供连接层写回 PLC 或记日志） </summary>
    public class BusinessResponse
    {
        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        /// <summary> 是否成功 </summary>
        public bool Success { get; set; }

        /// <summary> 提示或错误消息 </summary>
        public string? Message { get; set; }

        /// <summary> 附加返回数据 </summary>
        public object? Data { get; set; }
    }

    #endregion
}
