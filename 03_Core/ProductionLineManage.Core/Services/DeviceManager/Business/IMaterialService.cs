using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;

namespace ProductionLineManage.Core.Services.DeviceManager.Business
{
    /// <summary> 物料服务接口：物料验证与查询 </summary>
    public interface IMaterialService
    {
        #region ===================== 物料验证 =====================

        /// <summary> 完整物料验证（编码规则 → 工位物料 → 加工工位/顺序/父子） </summary>
        Task<MaterialVerifyResult> ValidateMaterialAsync(
            string materialType,
            string materialCode,
            int stationId,
            int typeId,
            int lineId,
            IReadOnlyList<int> validatedMaterialIds);

        /// <summary> 返修工位 500：仅验证物料编码规则 </summary>
        Task<MaterialVerifyResult> ValidateMaterialRulesOnlyAsync(
            string materialType,
            string materialCode,
            int stationId,
            int typeId);

        #endregion

        #region ===================== 物料查询 =====================

        /// <summary> 根据物料代号获取物料信息 </summary>
        Task<material_Info?> GetMaterialInfoByCodeAsync(string materialCode, int typeId);

        /// <summary> 获取工位需要录入的物料列表 </summary>
        Task<List<material_Station>> GetStationMaterialsAsync(int stationId, int typeId, int lineId);

        /// <summary> 获取流水码绑定的物料列表 </summary>
        Task<List<report_MaterialBind>> GetBoundMaterialsAsync(string flowCode);

        #endregion
    }
}
