using System.ServiceModel;
using ProductionLineManage.Core.Services.ExternalWeb;

namespace ProductionLineManage.Host.ExternalWeb
{
    #region ===================== SOAP 契约 =====================

    /// <summary>
    /// Win7 客户端 SOAP 契约（命名空间、Action、参数名必须与现场 WSDL 完全一致）。
    /// SoapCore 使用 XmlSerializer 序列化，参数名大小写敏感（如 Operter 不可改）。
    /// </summary>
    [ServiceContract(Namespace = "http://tempuri.org/")]
    [XmlSerializerFormat]
    public interface IRLD19145WebInterfaceSoap
    {
        /// <summary>连通性测试</summary>
        [OperationContract(Action = "http://tempuri.org/HelloWorld")]
        string HelloWorld();

        /// <summary>获取全部客户名称</summary>
        [OperationContract(Action = "http://tempuri.org/GetAllCustomerName")]
        string GetAllCustomerName();

        /// <summary>按客户名称获取产品型号列表</summary>
        [OperationContract(Action = "http://tempuri.org/GetProductTypeByCustomerName")]
        string GetProductTypeByCustomerName(string CustomerName);

        /// <summary>按产品型号获取电机号</summary>
        [OperationContract(Action = "http://tempuri.org/GetMotorNumberByProductType")]
        string GetMotorNumberByProductType(string ProductType);

        /// <summary>按产品型号与标志码获取电机号</summary>
        [OperationContract(Action = "http://tempuri.org/GetMotorNumberByProductTypeAndFlagCode")]
        string GetMotorNumberByProductTypeAndFlagCode(string ProductType, string FlagCode);

        /// <summary>以 JSON 格式上传工位数据</summary>
        [OperationContract(Action = "http://tempuri.org/UploadDataWithJSONCode")]
        string UploadDataWithJSONCode(string jsonCode);

        /// <summary>以结构化字段上传工位数据（参数名 PascalCase，与 WSDL 一致）</summary>
        [OperationContract(Action = "http://tempuri.org/UploadDataWithInfo")]
        string UploadDataWithInfo(
            string StationName,
            string ProductType,
            string TraySN,
            string ProductSN,
            string ProductResult,
            string DataContent,
            string InsertTime,
            string Operter); // 注意：Operter 为 WSDL 原始拼写，不可修改

        /// <summary>查询指定产品最近一次加工结果</summary>
        [OperationContract(Action = "http://tempuri.org/GetLastProductResult")]
        string GetLastProductResult(string ProductType, string StationName, string ProductSN);

        /// <summary>校验产品序列号是否允许生产</summary>
        [OperationContract(Action = "http://tempuri.org/CheckIsAllowProductionByProductSN")]
        string CheckIsAllowProductionByProductSN(string ProductType, string StationName, string ProductSN);
    }

    #endregion

    #region ===================== SOAP 端点适配 =====================

    /// <summary>
    /// SOAP 适配层：仅做参数转发，业务逻辑在 <see cref="IExternalWebStationService"/>。
    /// 保持 WSDL 参数名（PascalCase）与 Win7 客户端一致。
    /// </summary>
    [ServiceBehavior(
        InstanceContextMode = InstanceContextMode.Single,
        ConcurrencyMode = ConcurrencyMode.Multiple,
        IncludeExceptionDetailInFaults = true,
        AddressFilterMode = AddressFilterMode.Any)]
    public sealed class RLD19145WebInterfaceSoapEndpoint : IRLD19145WebInterfaceSoap
    {
        /// <summary>外部工位业务服务，承载实际数据库读写</summary>
        private readonly IExternalWebStationService _service;

        /// <summary>注入外部工位业务服务</summary>
        public RLD19145WebInterfaceSoapEndpoint(IExternalWebStationService service)
        {
            _service = service;
        }

        /// <inheritdoc/>
        public string HelloWorld() => _service.HelloWorld();

        /// <inheritdoc/>
        public string GetAllCustomerName() => _service.GetAllCustomerName();

        /// <inheritdoc/>
        public string GetProductTypeByCustomerName(string CustomerName) =>
            _service.GetProductTypeByCustomerName(CustomerName);

        /// <inheritdoc/>
        public string GetMotorNumberByProductType(string ProductType) =>
            _service.GetMotorNumberByProductType(ProductType);

        /// <inheritdoc/>
        public string GetMotorNumberByProductTypeAndFlagCode(string ProductType, string FlagCode) =>
            _service.GetMotorNumberByProductTypeAndFlagCode(ProductType, FlagCode);

        /// <inheritdoc/>
        public string UploadDataWithJSONCode(string jsonCode) =>
            _service.UploadDataWithJSONCode(jsonCode);

        /// <inheritdoc/>
        public string UploadDataWithInfo(
            string StationName,
            string ProductType,
            string TraySN,
            string ProductSN,
            string ProductResult,
            string DataContent,
            string InsertTime,
            string Operter) =>
            _service.UploadDataWithInfo(
                StationName, ProductType, TraySN, ProductSN,
                ProductResult, DataContent, InsertTime, Operter); // 原样转发，保持 WSDL 参数名

        /// <inheritdoc/>
        public string GetLastProductResult(string ProductType, string StationName, string ProductSN) =>
            _service.GetLastProductResult(ProductType, StationName, ProductSN);

        /// <inheritdoc/>
        public string CheckIsAllowProductionByProductSN(string ProductType, string StationName, string ProductSN) =>
            _service.CheckIsAllowProductionByProductSN(ProductType, StationName, ProductSN);
    }

    #endregion
}
