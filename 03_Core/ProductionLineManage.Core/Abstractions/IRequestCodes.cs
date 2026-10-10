namespace ProductionLineManage.Core.Abstractions
{
    /// <summary>
    /// 指令型工位在门禁层需要的请求码。数字由协议包提供，共享连接层不引用具体枚举。
    /// </summary>
    public interface IRequestCodes
    {
        int Handshake { get; }
        int FlowCodeVerify { get; }
        int MaterialVerify { get; }
    }
}
