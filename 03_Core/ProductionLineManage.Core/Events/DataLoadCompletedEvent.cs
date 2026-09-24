// ProductionLineManage.Core/Events/DataLoadCompletedEvent.cs
using Prism.Events;

namespace ProductionLineManage.Core.Events
{
    /// <summary>
    /// 数据加载完成事件（无载荷）。
    /// 由 MainViewModel.ShowDataLoadDialog 在加载弹窗 OK 后 Publish；
    /// MainViewModel.OnDataLoadCompleted 订阅后调用 StartAllDevicesAsync。
    /// </summary>
    public class DataLoadCompletedEvent : PubSubEvent
    {
    }
}
