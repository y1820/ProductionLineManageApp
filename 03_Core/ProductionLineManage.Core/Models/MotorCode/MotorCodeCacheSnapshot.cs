using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Core.Models.MotorCode
{
    /// <summary>
    /// 电机码配置缓存快照（启动加载 + UI 保存后刷新）。
    /// 不含 craft_MotorCodeSequenceState：运行时序号仍走数据库事务取号。
    /// </summary>
    public sealed class MotorCodeCacheSnapshot
    {
        #region ===================== 配置列表 =====================

        /// <summary> 序列号配置 </summary>
        public List<craft_MotorCodeSequenceConfig> SequenceConfigs { get; set; } = [];

        /// <summary> 日期代号对照 </summary>
        public List<craft_MotorCodeDateMap> DateMaps { get; set; } = [];

        /// <summary> 固定信息片段 </summary>
        public List<craft_MotorCodeFixedSegment> FixedSegments { get; set; } = [];

        /// <summary> 生成规则 </summary>
        public List<craft_MotorCodeRule> Rules { get; set; } = [];

        /// <summary> 片段绑定 </summary>
        public List<craft_MotorCodeSegmentBind> SegmentBinds { get; set; } = [];

        #endregion

        #region ===================== 查询辅助 =====================

        /// <summary> 按型号 Id 查找序列配置 </summary>
        public craft_MotorCodeSequenceConfig? GetSequenceConfig(int productTypeId) =>
            SequenceConfigs.FirstOrDefault(c => c.ProductTypeId == productTypeId); // 未配置时返回 null

        #endregion
    }
}
