namespace ProductionLineManage.Core.Configuration
{
    /// <summary>
    /// Win7 SOAP 外部工位配置（appsettings.json → ExternalWebStation 节点）。
    /// 所有 Id/名称类字段均为可选：未配置时服务仍可运行，仅影响 Id 解析与界面日志筛选。
    /// </summary>
    public class ExternalWebStationOptions
    {
        #region ===================== 服务开关与端点 =====================

        /// <summary> 是否启用 Host 内嵌 Kestrel SOAP 服务 </summary>
        public bool Enabled { get; set; }

        /// <summary> Kestrel 监听地址，需与 Win7 客户端访问的 IP:端口一致，如 http://*:8090 </summary>
        public string Urls { get; set; } = "http://*:8090";

        /// <summary> SOAP 端点路径，必须与现场 WSDL 一致 </summary>
        public string SoapPath { get; set; } = "/RLD19145WebInterface.asmx";

        #endregion

        #region ===================== 工位与产线解析 =====================

        /// <summary> 可选：固定工位 Id（&gt;0 时优先于 SOAP 报文 StationName 解析） </summary>
        public int StationId { get; set; }

        /// <summary>
        /// 可选：工位代号/名称提示（如 OP090）。
        /// 当 StationId=0 且 SOAP 报文未带工位名时，用此值在 craft_StationInfo 缓存中解析 Id（日志、落库）。
        /// </summary>
        public string? StationNameHint { get; set; }

        /// <summary> 可选：固定产线 Id（0 表示尝试从工位或报文推断） </summary>
        public int LineId { get; set; }

        /// <summary> 可选：固定型号 Id（0 表示尝试从 SOAP ProductType 解析） </summary>
        public int ProductTypeId { get; set; }

        #endregion

        #region ===================== 默认值与字段映射 =====================

        /// <summary> 兼容接口 GetProductTypeByCustomerName 等的默认型号代号 </summary>
        public string DefaultProductTypeCode { get; set; } = "DT01";

        /// <summary>
        /// 电机码绑定物料名称默认值。
        /// 若工位物料表已配置同名物料则优先使用配置名称，否则用此默认值。
        /// </summary>
        public string DefaultMotorMaterialName { get; set; } = "电机码";

        /// <summary> UploadDataWithInfo 的 DataContent JSON 中，电机码所在字段名 </summary>
        public string MotorNumberFieldName { get; set; } = "MotorNumber";

        #endregion
    }
}
