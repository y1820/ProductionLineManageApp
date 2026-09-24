using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.MotorCode;

namespace ProductionLineManage.Core.Services.MotorCode
{
    /// <summary> 电机码生成与序列配置服务 </summary>
    public interface IMotorCodeService
    {
        #region ===================== 电机码生成 =====================

        /// <summary> 按型号 Id 生成电机码（真取号或模拟） </summary>
        Task<MotorCodeGenerateResult> GenerateAsync(int productTypeId, MotorCodeGenerateOptions? options = null);

        /// <summary> 按型号名称生成（SOAP 用） </summary>
        Task<MotorCodeGenerateResult> GenerateByProductTypeNameAsync(string productTypeName, MotorCodeGenerateOptions? options = null);

        #endregion

        #region ===================== 序列号管理 =====================

        /// <summary> 读取指定型号的序列状态 </summary>
        Task<MotorCodeSequenceStatus?> GetSequenceStatusAsync(int productTypeId);

        /// <summary> 保存指定型号的序列配置 </summary>
        Task SaveSequenceConfigAsync(craft_MotorCodeSequenceConfig config);

        /// <summary> 手动调整指定型号的当前序号 </summary>
        Task AdjustSequenceAsync(int productTypeId, int newValue, string operatorName, string reason);

        /// <summary> 强制按配置复位到当前桶起始前状态 </summary>
        Task ResetSequenceToStartAsync(int productTypeId, string operatorName, string reason);

        #endregion
    }
}
