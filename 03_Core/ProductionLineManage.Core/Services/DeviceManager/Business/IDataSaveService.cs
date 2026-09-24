using ProductionLineManage.Core.Models.DataBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Services.DeviceManager.Business
{
    /// <summary> 保存数据服务接口：8000 保存生产数据与过站记录 </summary>
    public interface IDataSaveService
    {
        #region ===================== 生产数据保存 =====================

        /// <summary>
        /// 保存生产数据（PLC 发送 SaveData 信号时调用）
        /// </summary>
        /// <param name="flowCode">流水码</param>
        /// <param name="stationId">工位 Id</param>
        /// <param name="trayCode"> 托盘号 </param>
        /// <param name="productTypeId">产品型号 Id</param>
        /// <param name="lineId">产线 Id</param>
        /// <param name="datas">需要保存的工艺数据</param>
        /// <param name="status">过站状态（1=合格, 2=不合格）</param>
        /// <param name="productStationStatusId">工位状态 Id（production_ProductStationStatus.Id）</param>
        /// <param name="passRecordId">未结束的过站明细 Id（report_StationPassRecord.Id，8000 时关闭周期）</param>
        /// <param name="repairTargetStationId">返修目标工位 Id（仅返修保存时使用）</param>
        Task<(bool result, string mes)> SaveProductionDataAsync(string flowCode, int stationId, string trayCode,
            int productTypeId, int lineId, List<report_ProcessHistory> datas, int status, int productStationStatusId,
            int passRecordId,
            List<(string materialCode, string materialName)>? materialCodes = null,
            bool isRepair = false,
            int repairCount = 0,
            int repairTargetStationId = 0,
            bool isRepairProcess = false,
            bool isQualified = true);

        #endregion
    }
}
