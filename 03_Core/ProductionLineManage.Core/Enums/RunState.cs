// ProductionLineManage.Core/Enums/RunState.cs
namespace ProductionLineManage.Core.Enums
{
    /// <summary>设备运行状态</summary>
    public enum RunState
    {
        /// <summary>已停止</summary>
        Stopped = 0,

        /// <summary>运行中</summary>
        Running = 1,

        /// <summary>空闲</summary>
        Idle = 2,

        /// <summary>报警</summary>
        Alarm = 3,

        /// <summary>维护中</summary>
        Maintenance = 4
    }
}
