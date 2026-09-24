namespace ProductionLineManage.Core.Models.MotorCode
{
    #region ===================== 生成结果 =====================

    /// <summary> 电机码生成结果 </summary>
    public sealed class MotorCodeGenerateResult
    {
        /// <summary> 是否成功 </summary>
        public bool Success { get; init; }

        /// <summary> 生成的电机码 </summary>
        public string MotorCode { get; init; } = string.Empty;

        /// <summary> 本次使用的序列号 </summary>
        public int SequenceValue { get; init; }

        /// <summary> 错误信息 </summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary> 构造成功结果 </summary>
        public static MotorCodeGenerateResult Ok(string code, int sequenceValue) =>
            new() { Success = true, MotorCode = code, SequenceValue = sequenceValue };

        /// <summary> 构造失败结果 </summary>
        public static MotorCodeGenerateResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };
    }

    #endregion

    #region ===================== 生成选项 =====================

    /// <summary> 生成选项：模拟取号不写库 </summary>
    public sealed class MotorCodeGenerateOptions
    {
        /// <summary> 模拟模式（SOAP/工位应 false） </summary>
        public bool Simulate { get; set; }

        /// <summary> 模拟时指定序号；为 null 则内部按 DB 当前值+Step 计算 </summary>
        public int? SimulateSequenceValue { get; set; }

        /// <summary> 参考时间（默认 DateTime.Now） </summary>
        public DateTime? ReferenceTime { get; set; }
    }

    #endregion

    #region ===================== 序列运行时状态 =====================

    /// <summary> 序列运行时信息（UI 展示） </summary>
    public sealed class MotorCodeSequenceStatus
    {
        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 序号位数 </summary>
        public int DigitLength { get; set; }

        /// <summary> 起始值 </summary>
        public int StartValue { get; set; }

        /// <summary> 自增步长 </summary>
        public int Step { get; set; }

        /// <summary> 复位周期 </summary>
        public int ResetCycle { get; set; }

        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; }

        /// <summary> 当前桶键（复位周期标识） </summary>
        public string BucketKey { get; set; } = string.Empty;

        /// <summary> 当前已发出的最大序号 </summary>
        public int CurrentValue { get; set; }

        /// <summary> 下一待发序号 </summary>
        public int NextValue => CurrentValue + Step;

        /// <summary> 上次复位时间 </summary>
        public DateTime? LastResetTime { get; set; }

        /// <summary> 最后更新时间 </summary>
        public DateTime? UpdateTime { get; set; }
    }

    #endregion
}
