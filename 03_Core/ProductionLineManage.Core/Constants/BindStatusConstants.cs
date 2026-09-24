namespace ProductionLineManage.Core.Constants
{
    /// <summary> report_MaterialBind.BindStatus 物料绑定状态常量 </summary>
    public static class BindStatusConstants
    {
        /// <summary> 未绑定 </summary>
        public const int Unbound = 0;

        /// <summary> 已绑定 </summary>
        public const int Bound = 1;

        /// <summary> 历史解绑 </summary>
        public const int UnboundHistory = 2;

        /// <summary> 已报废 </summary>
        public const int Scrapped = 3;
    }
}
