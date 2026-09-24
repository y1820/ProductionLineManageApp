// ProductionLineManage.Core/Services/IDeviceConnectionManager.cs
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Services.DeviceManager.Connection
{
    /// <summary> 设备连接管理器接口：管理各工位连接任务的启停 </summary>
    public interface IDeviceConnectionManager
    {
        #region ===================== 单工位控制 =====================

        /// <summary> 启动单个工位连接 </summary>
        Task<bool> StartStationAsync(device_ConnectInfo config);

        /// <summary> 停止单个工位连接 </summary>
        Task StopStationAsync(int stationId);

        #endregion

        #region ===================== 批量控制 =====================

        /// <summary> 启动所有已配置工位 </summary>
        Task StartAllAsync();

        /// <summary> 停止所有工位连接 </summary>
        Task StopAllAsync();

        #endregion
    }
}
