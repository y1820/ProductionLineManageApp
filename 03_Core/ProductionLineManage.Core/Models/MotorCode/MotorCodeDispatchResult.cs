namespace ProductionLineManage.Core.Models.MotorCode
{
    /// <summary> 900 指令：按流水码取最新绑定电机码 + 型号平台代号 </summary>
    public sealed class MotorCodeDispatchResult
    {
        #region ===================== 结果字段 =====================

        /// <summary> 是否成功 </summary>
        public bool Success { get; init; }

        /// <summary> 电机码 </summary>
        public string MotorCode { get; init; } = string.Empty;

        /// <summary> 平台代号 </summary>
        public string PlatformCode { get; init; } = string.Empty;

        /// <summary> 错误信息 </summary>
        public string ErrorMessage { get; init; } = string.Empty;

        #endregion

        #region ===================== 工厂方法 =====================

        /// <summary> 构造成功结果 </summary>
        public static MotorCodeDispatchResult Ok(string motorCode, string platformCode) =>
            new() { Success = true, MotorCode = motorCode, PlatformCode = platformCode };

        /// <summary> 构造失败结果 </summary>
        public static MotorCodeDispatchResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        #endregion
    }
}
