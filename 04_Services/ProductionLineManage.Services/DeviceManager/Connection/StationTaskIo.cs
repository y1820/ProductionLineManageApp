using ProductionLineManage.Core.Services.DeviceManager.Connection;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 工位 Task I/O 门面：Interaction 通过 IDeviceTaskContext 读写 PLC 的实际执行层。
    /// 封装映射查找、驱动获取与 Stop 期间 IO 许可判断。
    /// </summary>
    internal sealed class StationTaskIo
    {
        #region ===================== 字段 =====================

        /// <summary> 地址映射 </summary>
        private readonly StationAcquisition _acquisition;

        /// <summary> 连接与驱动 </summary>
        private readonly StationConnection _connection;

        /// <summary> 是否允许 IO（含 Stop 时扫描周期内回写） </summary>
        private readonly Func<bool> _canUseDriverForIo;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建 Task I/O 门面 </summary>
        public StationTaskIo(
            StationAcquisition acquisition,
            StationConnection connection,
            Func<bool> canUseDriverForIo)
        {
            _acquisition = acquisition;
            _connection = connection;
            _canUseDriverForIo = canUseDriverForIo;
        }

        #endregion

        #region ===================== 按 DataName 读写 =====================

        /// <summary> 按地址映射 DataName 读取 </summary>
        public async Task<object?> ReadByDataNameAsync(string dataName)
        {
            if (!_acquisition.TryGetMapping(dataName, out var mapping))
                return null; // 未配置映射

            var driver = _connection.GetDriver();
            if (driver == null)
                return null; // 未连接或驱动已释放

            return await driver.ReadAsync(mapping.DataAddress, mapping.DataType, mapping.DataLen);
        }

        /// <summary> 按地址映射 DataName 写入 </summary>
        public async Task<bool> WriteByDataNameAsync(string dataName, object value)
        {
            if (!_canUseDriverForIo())
                return false; // Stop 期间且不在扫描周期内，禁止 IO

            if (!_acquisition.TryGetMapping(dataName, out var mapping))
                return false;

            var driver = _connection.GetDriver();
            if (driver == null)
                return false;

            return await driver.WriteAsync(
                mapping.DataAddress, mapping.DataType, value, mapping.DataLen);
        }

        #endregion

        #region ===================== 按原始地址读写 =====================

        /// <summary> 按原始 PLC 地址读取（数据采集等场景） </summary>
        public async Task<object?> ReadByAddressAsync(string address, string dataType, int dataLength)
        {
            if (!_canUseDriverForIo() || string.IsNullOrWhiteSpace(address))
                return null; // IO 不可用或地址为空

            var driver = _connection.GetDriver();
            if (driver == null)
                return null;

            return await driver.ReadAsync(address, dataType, dataLength);
        }

        /// <summary> 按原始 PLC 地址写入（工位传值等场景） </summary>
        public async Task<bool> WriteByAddressAsync(
            string address, string dataType, object value, int dataLength)
        {
            if (!_canUseDriverForIo() || string.IsNullOrWhiteSpace(address))
                return false;

            var driver = _connection.GetDriver();
            if (driver == null)
                return false; // 驱动不可用

            return await driver.WriteAsync(address, dataType, value, dataLength);
        }

        #endregion
    }
}
