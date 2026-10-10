namespace ProductionLineManage.Line.RLD19145.Profile
{
    #region ===================== PLC 请求指令码 =====================

    /// <summary> PLC 请求指令码（指令型交互，数值可按项目配置调整） </summary>
    public enum PLCRequestCode
    {
        /// <summary> 无请求 </summary>
        None = 0,

        /// <summary> 握手 </summary>
        Handshake = 100,

        /// <summary> 流水码校验 </summary>
        FlowCodeVerify = 200,

        /// <summary> 物料码校验 </summary>
        MaterialVerify = 500,

        /// <summary> 保存数据（扩展） </summary>
        SaveDataExtended = 8000,

        /// <summary> 获取流水码最新绑定电机码 + 平台代号（激光标刻等） </summary>
        MotorCodeFetch = 900,
    }

    #endregion

    #region ===================== SCADA 响应指令码 =====================

    /// <summary> SCADA 响应指令码 </summary>
    public enum SCADAResponseCode
    {
        /// <summary> 无响应 </summary>
        None = 0,

        /// <summary> 握手成功 </summary>
        HandshakeSuccess = 100,

        /// <summary> 流水码有效 </summary>
        FlowCodeValid = 200,

        /// <summary> 流水码无效 </summary>
        FlowCodeInvalid = 201,

        /// <summary> 流水码已使用 </summary>
        FlowCodeAlreadyUsed = 202,

        /// <summary> 流水码不匹配 </summary>
        FlowCodeNotMatch = 203,

        /// <summary> 物料码有效 </summary>
        MaterialValid = 500,

        /// <summary> 物料码无效 </summary>
        MaterialInvalid = 501,

        /// <summary> 物料规则不匹配 </summary>
        MaterialRuleNotMatch = 502,

        /// <summary> 保存成功 </summary>
        SaveSuccess = 8000,

        /// <summary> 保存并上报成功 </summary>
        SaveWithReportSuccess = 8001,

        /// <summary> 通用错误 </summary>
        GeneralError = 1,

        /// <summary> 需要重试 </summary>
        NeedRetry = 2,

        /// <summary> 超时 </summary>
        Timeout = 3,

        /// <summary> 数据库错误 </summary>
        DatabaseError = 4,

        /// <summary> 工序顺序不允许 </summary>
        SequenceNotAllowed = 5,

        /// <summary> 电机码获取成功 </summary>
        MotorCodeFetchSuccess = 900,

        /// <summary> 电机码未找到 </summary>
        MotorCodeNotFound = 901,

        /// <summary> 平台代号未配置 </summary>
        PlatformCodeNotConfigured = 902,
    }

    #endregion
}
