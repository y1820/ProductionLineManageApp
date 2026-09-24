using ProductionLineManage.Core.Models.DataBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Services.DeviceManager.Connection
{
    /// <summary> 共享驱动池接口：集中式模式下多工位复用同一 PLC 连接 </summary>
    public interface ISharedDriverPool
    {
        #region ===================== 连接获取与释放 =====================

        /// <summary> 获取或创建连接 </summary>
        Task<IDeviceCommunication> GetOrCreateAsync(device_ConnectInfo config);

        /// <summary> 等待 Session 完成下一次或当前物理连接（集成式工位断线后调用，不触发 Open） </summary>
        Task<IDeviceCommunication?> WaitForConnectionAsync(device_ConnectInfo config, CancellationToken token);

        /// <summary> 释放连接 </summary>
        void Release(device_ConnectInfo config);

        #endregion
    }
}
