namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 登录用户信息数据实体 </summary>
    public class UserInfo : BaseEntity
    {
        #region ===================== 账号信息 =====================

        /// <summary> 用户名 </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary> 哈希密码 </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary> 盐值 </summary>
        public string Salt { get; set; } = string.Empty;

        #endregion

        #region ===================== 权限 =====================

        /// <summary> 用户等级/权限 </summary>
        public int Level { get; set; }

        #endregion
    }
}
