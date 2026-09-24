namespace ProductionLineManage.Core.Abstractions
{
    /// <summary>
    /// 产线身份。以后每条线一个实现，Host 用 LineKey 和配置里的 ActiveLine 对应。
    /// </summary>
    public interface ILineProfile
    {
        /// <summary> 配置代号 </summary>
        string LineKey { get; }
        /// <summary> 给人看的名称 </summary>
        string DisplayName {  get; }
    }
}
