namespace ProductionLineManage.Core.Constants
{
    /// <summary> 设备数据类型常量与按协议筛选的集合 </summary>
    public static class DeviceDataTypeConstants
    {
        #region ===================== 模拟器 C# 数据类型 =====================

        // 增删改常量后需同步更新下方 SimulatorDataTypeConstants 集合
        public const string Cbool = "bool";
        public const string Cint = "int";
        public const string Cfloat = "float";
        public const string Cdouble = "double";
        public const string Cbyte = "byte";
        public const string Cshort = "short";
        public const string CDateTime = "DateTime";
        public const string Cstring = "string";

        #endregion

        #region ===================== S7 协议数据类型 =====================

        public const string SBool = "Bool";
        public const string SByte = "Byte";
        public const string SWord = "Word";
        public const string SInt = "Int";
        public const string SDInt = "DInt";
        public const string SReal = "Real";
        public const string SDateTime = "DateTime";
        public const string SS7String = "S7String";
        public const string STimer = "Timer";

        #endregion

        #region ===================== OPC UA 数据类型 =====================

        public const string OBoolean = "Boolean";
        public const string OByte = "Byte";
        public const string OSByte = "SByte";
        public const string OInt16 = "Int16";
        public const string OUInt16 = "UInt16";
        public const string OInt32 = "Int32";
        public const string OUInt32 = "UInt32";
        public const string OInt64 = "Int64";
        public const string OUInt64 = "UInt64";
        public const string OFloat = "Float";
        public const string ODouble = "Double";
        public const string OString = "String";
        public const string ODateTime = "DateTime";
        public const string OGuid = "Guid";

        #endregion

        #region ===================== 数据类型集合 =====================

        /// <summary> 模拟器可选数据类型 </summary>
        public static IReadOnlyList<string> SimulatorDataTypeConstants = new[]
        {
            "bool",
            "byte",
            "short",
            "int",
            "float",
            "double",
            "string",
            "DateTime",
        };

        /// <summary> 默认数据类型（S7 + OPC UA 合并） </summary>
        public static IReadOnlyList<string> DataTypeConstants = new[]
        {
            SBool,
            SByte,
            SWord,
            SInt,
            SDInt,
            SReal,
            SDateTime,
            SS7String,
            STimer,

            OBoolean,
            OByte,
            OSByte,
            OInt16,
            OUInt16,
            OInt32,
            OUInt32,
            OInt64,
            OUInt64,
            OFloat,
            ODouble,
            OString,
            ODateTime,
            OGuid,
        };

        /// <summary> OPC UA 协议可选数据类型 </summary>
        public static IReadOnlyList<string> OPCUADataTypeConstants = new[]
        {
            OBoolean,
            OByte,
            OSByte,
            OInt16,
            OUInt16,
            OInt32,
            OUInt32,
            OInt64,
            OUInt64,
            OFloat,
            ODouble,
            OString,
            ODateTime,
            OGuid,
        };

        /// <summary> S7 协议可选数据类型 </summary>
        public static IReadOnlyList<string> S7DataTypeConstants = new[]
        {
            SBool,
            SByte,
            SWord,
            SInt,
            SDInt,
            SReal,
            SDateTime,
            SS7String,
            STimer,
        };

        /// <summary> 数据方向：读取、写入、读写 </summary>
        public static IReadOnlyList<string> DataDirection = new[]
        {
            "Read",
            "Write",
            "Read/Write"
        };

        #endregion

        #region ===================== 按协议筛选 =====================

        /// <summary> 按通讯协议返回可选数据类型；无配置或未识别协议时返回空列表 </summary>
        public static IReadOnlyList<string> GetDataTypesForProtocol(string? protocolType)
        {
            if (string.IsNullOrWhiteSpace(protocolType))
                return Array.Empty<string>(); // 协议未指定，不提供选项

            return protocolType switch
            {
                DeviceProtocolTypeConstants.S7 => S7DataTypeConstants,
                DeviceProtocolTypeConstants.OPCUA => OPCUADataTypeConstants,
                DeviceProtocolTypeConstants.Simulator => SimulatorDataTypeConstants,
                _ => Array.Empty<string>() // 未支持的协议
            };
        }

        #endregion
    }
}
