namespace ProductionLineManage.Core.Services.DeviceManager.Connection
{
    #region ===================== 连接尝试状态 =====================

    /// <summary> 连接尝试结果状态 </summary>
    public enum ConnectionAttemptStatus
    {
        /// <summary> 连接成功 </summary>
        Success,

        /// <summary> 连接失败 </summary>
        Failed,

        /// <summary> 已取消 </summary>
        Cancelled,

        /// <summary> 发生异常 </summary>
        Error
    }

    #endregion

    /// <summary> 连接策略执行结果 </summary>
    public sealed class ConnectionAcquireResult
    {
        #region ===================== 结果属性 =====================

        /// <summary> 尝试状态 </summary>
        public ConnectionAttemptStatus Status { get; init; }

        /// <summary> 驱动实例（成功时非空） </summary>
        public IDeviceCommunication? Driver { get; init; }

        /// <summary> 异常信息（Error 状态时非空） </summary>
        public Exception? Error { get; init; }

        /// <summary> 是否连接成功 </summary>
        public bool IsSuccess => Status == ConnectionAttemptStatus.Success;

        #endregion

        #region ===================== 工厂方法 =====================

        /// <summary> 创建成功结果 </summary>
        public static ConnectionAcquireResult Succeeded(IDeviceCommunication driver) =>
            new() { Status = ConnectionAttemptStatus.Success, Driver = driver };

        /// <summary> 创建失败结果 </summary>
        public static ConnectionAcquireResult Failed() =>
            new() { Status = ConnectionAttemptStatus.Failed };

        /// <summary> 创建取消结果 </summary>
        public static ConnectionAcquireResult Cancelled() =>
            new() { Status = ConnectionAttemptStatus.Cancelled };

        /// <summary> 创建异常结果 </summary>
        public static ConnectionAcquireResult Errored(Exception error) =>
            new() { Status = ConnectionAttemptStatus.Error, Error = error };

        #endregion
    }
}
