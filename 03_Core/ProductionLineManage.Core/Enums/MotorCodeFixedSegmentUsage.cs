namespace ProductionLineManage.Core.Enums
{
    /// <summary>固定信息用途（用于激光等下游工位下发，与生成规则公式独立）</summary>
    public enum MotorCodeFixedSegmentUsage
    {
        /// <summary>仅参与电机码组合公式</summary>
        General = 0,

        /// <summary>平台代号：900 指令下发到 PLC「下发平台代号」</summary>
        Platform = 1
    }
}
