namespace ProductionLineManage.Core.CommandCodes
{
    /// <summary>
    /// 指令型工位的请求/响应码表。含义固定，数字由已登记模板提供，工位在连接配置里选择。
    /// </summary>
    public interface ICommandCodeTemplate
    {
        /// <summary> 登记键，写入 device_ConnectInfo.CommandCodeTemplate </summary>
        string Key { get; }

        /// <summary> 界面下拉显示名 </summary>
        string DisplayName { get; }

        #region ===================== 请求码 =====================

        int Handshake { get; }
        int FlowCodeVerify { get; }
        int MaterialVerify { get; }
        int SaveData { get; }
        int MotorCodeFetch { get; }

        #endregion

        #region ===================== 响应码 =====================

        int HandshakeSuccess { get; }
        int FlowCodeValid { get; }
        int FlowCodeInvalid { get; }
        int FlowCodeAlreadyUsed { get; }
        int FlowCodeNotMatch { get; }
        int MaterialValid { get; }
        int MaterialInvalid { get; }
        int MaterialRuleNotMatch { get; }
        int SaveSuccess { get; }
        int SaveWithReportSuccess { get; }
        int GeneralError { get; }
        int NeedRetry { get; }
        int Timeout { get; }
        int DatabaseError { get; }
        int SequenceNotAllowed { get; }
        int MotorCodeFetchSuccess { get; }
        int MotorCodeNotFound { get; }
        int PlatformCodeNotConfigured { get; }

        #endregion
    }
}
