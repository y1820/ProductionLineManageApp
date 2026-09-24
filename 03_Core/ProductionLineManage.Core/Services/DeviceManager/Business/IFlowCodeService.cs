using ProductionLineManage.Core.Models;
using ProductionLineManage.Core.Models.DataBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Services.DeviceManager.Business
{
    /// <summary> 流水码验证服务接口 </summary>
    public interface IFlowCodeService
    {
        #region ===================== 流水码验证 =====================

        /// <summary> 验证流水码是否符合工位规则 </summary>
        /// <returns>是否有效及待过站记录 Id（失败时 Id=0）</returns>
        Task<(bool success, int stationRecordId)> ValidateFlowCodeAsync(
            string flowCode, int stationId, int productTypeId, int lineId, string trayCode);

        /// <summary> 判断上工位是否合格 </summary>
        Task<bool> IsPreviousStationQualifiedAsync(string flowCode, int currentStationId, int productType);

        /// <summary> 仅验证流水码编码规则（返修工位 200 使用，不创建过站记录） </summary>
        Task<bool> ValidateFlowCodeRulesOnlyAsync(
            string flowCode, int stationId, int productTypeId, int lineId);

        #endregion
    }
}
