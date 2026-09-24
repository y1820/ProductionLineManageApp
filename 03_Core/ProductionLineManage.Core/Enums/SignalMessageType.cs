namespace ProductionLineManage.Core.Enums
{
    #region ===================== 信号消息类型 =====================

    /// <summary> 信号线交互 - 消息类型 </summary>
    public enum SignalMessageType
    {
        /// <summary> 流水码录入完成信号 </summary>
        FlowCodeDone,

        /// <summary> 流水码值 </summary>
        FlowCodeValue,

        /// <summary> 物料码录入完成 </summary>
        MaterialCodeDone,

        /// <summary> 物料码值 </summary>
        MaterialCodeValue,

        /// <summary> 物料类型 </summary>
        MaterialType,

        /// <summary> 保存数据 </summary>
        SaveData,
    }

    #endregion

    #region ===================== 信号消息载体 =====================

    /// <summary> 信号线交互 - 消息载体 </summary>
    public class SignalMessage
    {
        /// <summary> 消息类型 </summary>
        public SignalMessageType Type { get; set; }

        /// <summary> 消息值（类型取决于 Type） </summary>
        public object? Value { get; set; }

        /// <summary> 时间戳 </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary> 重试次数 </summary>
        public int RetryCount { get; set; } = 0;
    }

    #endregion
}
