namespace ProductionLineManage.Core.Constants
{
    /// <summary> 与设备交互的类型；每个产线下各工位可配置不同交互方式，新增类型在此追加常量 </summary>
    public static class InteractionTypeConstants
    {
        /// <summary> 指令型交互（请求码/响应码） </summary>
        public const string Command = "指令类型";

        /// <summary> 信号型交互（Bool 信号触发） </summary>
        public const string Signal = "信号类型";

        /// <summary> 全部交互类型（UI 下拉选项） </summary>
        public static IReadOnlyList<string> AllInteractionTypeTypes { get; } = new[]
        {
            Command,
            Signal,
        };
    }
}
