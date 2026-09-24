namespace ProductionLineManage.Core.Models.DataBase
{
    #region ===================== 无审计字段基类 =====================

    /// <summary> 实体基类（不带审计字段） </summary>
    public class EntityBase
    {
        /// <summary> 主键 ID </summary>
        public int Id { get; set; }
    }

    #endregion

    #region ===================== 带审计字段基类 =====================

    /// <summary> 实体基类（带审计字段） </summary>
    public class BaseEntity : EntityBase
    {
        /// <summary> 创建时间 </summary>
        public DateTime? CreateTime { get; set; }

        /// <summary> 更新时间 </summary>
        public DateTime? UpdateTime { get; set; }

        /// <summary> 备注 </summary>
        public string Remarks { get; set; } = string.Empty;
    }

    #endregion
}
