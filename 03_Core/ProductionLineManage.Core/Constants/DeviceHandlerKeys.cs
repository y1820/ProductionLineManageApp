namespace ProductionLineManage.Core.Constants
{
    /// <summary>
    /// 业务处理器注册键（与 IDeviceDataHandler.DataType 对应）
    /// 在 App 启动时 RegisterHandler，Interaction 通过此键查找 Handler
    /// </summary>
    public static class DeviceHandlerKeys
    {
        /// <summary>流水码验证（PLC 请求码 200）</summary>
        public const string FlowCode = "FlowCode";

        /// <summary>物料验证（PLC 请求码 300）</summary>
        public const string Material = "Material";

        /// <summary>保存生产数据（PLC 请求码 800）</summary>
        public const string DataSave = "DataSave";

        /// <summary>返修确认</summary>
        public const string Repair = "Repair";

        /// <summary>工位间数据传递</summary>
        public const string StationDataTransfer = "StationDataTransfer";
    }
}
