namespace ProductionLineManage.Core.CommandCodes
{
    /// <summary>
    /// 1/2 系列码表。成功响应与请求同值；失败码避开 1–5，避免和握手/流水码请求冲突。
    /// </summary>
    public sealed class Code12CommandTemplate : ICommandCodeTemplate
    {
        public string Key => CommandCodeTemplateKeys.Code12;
        public string DisplayName => "Code12（1/2/3/4）";

        public int Handshake => 1;
        public int FlowCodeVerify => 2;
        public int MaterialVerify => 3;
        public int SaveData => 4;
        public int MotorCodeFetch => 5;

        public int HandshakeSuccess => 1;
        public int FlowCodeValid => 2;
        public int FlowCodeInvalid => 12;
        public int FlowCodeAlreadyUsed => 13;
        public int FlowCodeNotMatch => 14;
        public int MaterialValid => 3;
        public int MaterialInvalid => 32;
        public int MaterialRuleNotMatch => 33;
        public int SaveSuccess => 4;
        public int SaveWithReportSuccess => 41;
        public int GeneralError => 90;
        public int NeedRetry => 91;
        public int Timeout => 92;
        public int DatabaseError => 93;
        public int SequenceNotAllowed => 94;
        public int MotorCodeFetchSuccess => 5;
        public int MotorCodeNotFound => 51;
        public int PlatformCodeNotConfigured => 52;
    }
}
