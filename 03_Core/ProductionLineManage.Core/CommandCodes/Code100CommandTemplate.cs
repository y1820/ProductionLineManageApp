using ProductionLineManage.Core.Enums;

namespace ProductionLineManage.Core.CommandCodes
{
    /// <summary> 现有 100 系列码表，数值与 PLCRequestCode / SCADAResponseCode 一致 </summary>
    public sealed class Code100CommandTemplate : ICommandCodeTemplate
    {
        public string Key => CommandCodeTemplateKeys.Code100;
        public string DisplayName => "Code100（100/200/500/8000）";

        public int Handshake => (int)PLCRequestCode.Handshake;
        public int FlowCodeVerify => (int)PLCRequestCode.FlowCodeVerify;
        public int MaterialVerify => (int)PLCRequestCode.MaterialVerify;
        public int SaveData => (int)PLCRequestCode.SaveDataExtended;
        public int MotorCodeFetch => (int)PLCRequestCode.MotorCodeFetch;

        public int HandshakeSuccess => (int)SCADAResponseCode.HandshakeSuccess;
        public int FlowCodeValid => (int)SCADAResponseCode.FlowCodeValid;
        public int FlowCodeInvalid => (int)SCADAResponseCode.FlowCodeInvalid;
        public int FlowCodeAlreadyUsed => (int)SCADAResponseCode.FlowCodeAlreadyUsed;
        public int FlowCodeNotMatch => (int)SCADAResponseCode.FlowCodeNotMatch;
        public int MaterialValid => (int)SCADAResponseCode.MaterialValid;
        public int MaterialInvalid => (int)SCADAResponseCode.MaterialInvalid;
        public int MaterialRuleNotMatch => (int)SCADAResponseCode.MaterialRuleNotMatch;
        public int SaveSuccess => (int)SCADAResponseCode.SaveSuccess;
        public int SaveWithReportSuccess => (int)SCADAResponseCode.SaveWithReportSuccess;
        public int GeneralError => (int)SCADAResponseCode.GeneralError;
        public int NeedRetry => (int)SCADAResponseCode.NeedRetry;
        public int Timeout => (int)SCADAResponseCode.Timeout;
        public int DatabaseError => (int)SCADAResponseCode.DatabaseError;
        public int SequenceNotAllowed => (int)SCADAResponseCode.SequenceNotAllowed;
        public int MotorCodeFetchSuccess => (int)SCADAResponseCode.MotorCodeFetchSuccess;
        public int MotorCodeNotFound => (int)SCADAResponseCode.MotorCodeNotFound;
        public int PlatformCodeNotConfigured => (int)SCADAResponseCode.PlatformCodeNotConfigured;
    }
}
