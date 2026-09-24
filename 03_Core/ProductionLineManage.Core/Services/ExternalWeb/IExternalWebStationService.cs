namespace ProductionLineManage.Core.Services.ExternalWeb
{
    /// <summary>
    /// Win7 SOAP 外部工位业务接口（与 RLD19145WebInterface.asmx WSDL 方法一一对应）。
    /// 实现类负责：电机码生成、JSON 解析落库、物料绑定、日志；不过站、不写过站状态表。
    /// </summary>
    public interface IExternalWebStationService
    {
        #region ===================== 连通性与兼容接口 =====================

        /// <summary> 连通性测试 </summary>
        string HelloWorld();

        /// <summary> 兼容接口：返回客户名列表（实验版返回固定值） </summary>
        string GetAllCustomerName();

        /// <summary> 兼容接口：按客户名返回型号代号 </summary>
        string GetProductTypeByCustomerName(string customerName);

        #endregion

        #region ===================== 电机码生成 =====================

        /// <summary> 按型号生成并返回电机码（Win7 加工前调用） </summary>
        string GetMotorNumberByProductType(string productType);

        /// <summary> 按型号 + 标识码生成电机码 </summary>
        string GetMotorNumberByProductTypeAndFlagCode(string productType, string flagCode);

        #endregion

        #region ===================== 数据上传 =====================

        /// <summary> 整段 JSON 上传（兼容接口，写入 report_ProcessHistory） </summary>
        string UploadDataWithJSONCode(string jsonCode);

        /// <summary>
        /// 上传加工数据：解析 DataContent JSON → report_ProcessHistory；
        /// 提取 MotorNumber → report_MaterialBind（物料名默认「电机码」）。
        /// </summary>
        string UploadDataWithInfo(
            string stationName,
            string productType,
            string traySn,
            string productSn,
            string productResult,
            string dataContent,
            string insertTime,
            string operter);

        #endregion

        #region ===================== 加工结果查询 =====================

        /// <summary> 查询指定流水码最近一次的 Final_Result / 加工结果 </summary>
        string GetLastProductResult(string productType, string stationName, string productSn);

        /// <summary> 兼容接口：根据上次结果判断是否允许加工（OK/NG） </summary>
        string CheckIsAllowProductionByProductSN(string productType, string stationName, string productSn);

        #endregion
    }
}
