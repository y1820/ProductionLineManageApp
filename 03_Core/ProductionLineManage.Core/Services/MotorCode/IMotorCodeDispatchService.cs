using ProductionLineManage.Core.Models.MotorCode;

namespace ProductionLineManage.Core.Services.MotorCode
{
    /// <summary> 下游工位（如激光标刻）按流水码获取已绑定电机码，并按型号解析平台代号 </summary>
    public interface IMotorCodeDispatchService
    {
        #region ===================== 900 指令下发 =====================

        /// <summary> 查询流水码最新绑定的电机码，以及该型号标记为「平台代号」的固定信息 </summary>
        Task<MotorCodeDispatchResult> GetDispatchDataAsync(
            string flowCode,
            int productTypeId,
            string? motorMaterialName = null);

        #endregion
    }
}
