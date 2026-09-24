using System.Data;
using Dapper;
using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.MotorCode;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Infrastructure.Data.Repository;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.MotorCode
{
    /// <summary>
    /// 电机码生成服务：规则解析、日期映射、按型号独立序列安全取号。
    /// </summary>
    public sealed class MotorCodeService : IMotorCodeService
    {
        #region ===================== 常量与字段 =====================

        private const string LogSource = "MotorCode";

        /// <summary> 数据库访问 </summary>
        private readonly SQLHelper _sql;
        /// <summary> 全局配置缓存（型号信息等） </summary>
        private readonly IDataCacheService _cache;
        /// <summary> 电机码专用配置缓存 </summary>
        private readonly IMotorCodeCacheService _motorCodeCache;
        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入 SQL、缓存与日志 </summary>
        public MotorCodeService(
            SQLHelper sql,
            IDataCacheService cache,
            IMotorCodeCacheService motorCodeCache,
            ILogger logger)
        {
            _sql = sql;
            _cache = cache;
            _motorCodeCache = motorCodeCache;
            _logger = logger;
        }

        #endregion

        #region ===================== 配置快照 =====================

        /// <summary> 确保电机码配置已加载并返回快照 </summary>
        private async Task<MotorCodeCacheSnapshot> GetConfigSnapshotAsync()
        {
            if (!_motorCodeCache.IsLoaded)
                await _motorCodeCache.RefreshAsync(); // 首次使用时从库加载

            return _motorCodeCache.GetSnapshot();
        }

        #endregion

        #region ===================== 电机码生成 =====================

        /// <inheritdoc />
        public async Task<MotorCodeGenerateResult> GenerateByProductTypeNameAsync(
            string productTypeName,
            MotorCodeGenerateOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(productTypeName))
                return MotorCodeGenerateResult.Fail("ProductType 为空");

            var types = _cache.GetData<List<craft_TypeInfo>>(); // 从全局缓存取型号列表
            var type = types?.FirstOrDefault(t =>
                t.Name.Equals(productTypeName.Trim(), StringComparison.OrdinalIgnoreCase)); // 按名称匹配

            if (type == null)
                return MotorCodeGenerateResult.Fail($"未找到型号: {productTypeName}");

            return await GenerateAsync(type.Id, options); // 委托按 Id 生成
        }

        /// <inheritdoc />//按型号Id生成电机码(真取号或模拟)
        public async Task<MotorCodeGenerateResult> GenerateAsync(
            int productTypeId,
            MotorCodeGenerateOptions? options = null)
        {
            options ??= new MotorCodeGenerateOptions();
            var now = options.ReferenceTime ?? DateTime.Now; // 参考时间用于日期片段与桶 Key

            try
            {
                var snapshot = await GetConfigSnapshotAsync(); // 取规则、映射、绑定等配置

                var rule = snapshot.Rules.FirstOrDefault(r => r.ProductTypeId == productTypeId); // 该型号规则

                if (rule == null)
                    return MotorCodeGenerateResult.Fail($"型号 Id={productTypeId} 未配置电机码规则");

                if (!rule.IsEnabled)
                    return MotorCodeGenerateResult.Fail($"型号 Id={productTypeId} 电机码规则未启用");

                if (string.IsNullOrWhiteSpace(rule.Formula))
                    return MotorCodeGenerateResult.Fail("电机码规则公式为空");

                var config = snapshot.GetSequenceConfig(productTypeId);
                if (config == null || !config.IsEnabled)
                    return MotorCodeGenerateResult.Fail($"型号 Id={productTypeId} 未配置序列号或已禁用,序列号配置{config == null},是否启用{config?.IsEnabled}");
                //该productTypeId型号的片段绑定配置
                var binds = snapshot.SegmentBinds
                    .Where(b => b.ProductTypeId == productTypeId)
                    .ToList();
                //该productTypeId型号的日期对照配置
                var dateMaps = snapshot.DateMaps
                    .Where(m => m.ProductTypeId == productTypeId)
                    .ToList();
                //该productTypeId型号的固定信息
                var fixedSegments = snapshot.FixedSegments
                    .Where(f => f.ProductTypeId == productTypeId)
                    .ToList();

                var tokens = ParseFormula(rule.Formula); // 按 + 拆分片段代号
                if (tokens.Count == 0)
                    return MotorCodeGenerateResult.Fail("规则公式未包含有效片段代号");

                int sequenceValue;
                if (options.Simulate)
                {
                    // 模拟模式：不占用真实序列号
                    if (options.SimulateSequenceValue.HasValue)
                    {
                        sequenceValue = options.SimulateSequenceValue.Value; // 使用指定模拟值
                    }
                    else
                    {
                        var status = await GetSequenceStatusAsync(productTypeId);
                        if (status == null || !status.IsEnabled)
                            return MotorCodeGenerateResult.Fail("序列号配置未启用或不存在");

                        sequenceValue = status.CurrentValue + status.Step; // 预测下一号
                    }
                }
                else
                {
                    var alloc = await AllocateSequenceAsync(productTypeId, now); // 事务内安全取号
                    if (!alloc.Success)
                        return MotorCodeGenerateResult.Fail(alloc.ErrorMessage);

                    sequenceValue = alloc.SequenceValue;
                }

                var parts = new List<string>();
                foreach (var token in tokens)
                {
                    var bind = binds.FirstOrDefault(b =>
                        b.SegmentCode.Equals(token, StringComparison.OrdinalIgnoreCase));

                    if (bind == null)
                        return MotorCodeGenerateResult.Fail($"规则公式含未定义代号: {token}");

                    var segmentType = (MotorCodeSegmentType)bind.SegmentType;
                    var part = segmentType switch
                    {
                        MotorCodeSegmentType.Year => ResolveDateMap(dateMaps, MotorCodeMapType.Year, now.Year,
                            $"年代号映射缺失: {now.Year}"),
                        MotorCodeSegmentType.Month => ResolveDateMap(dateMaps, MotorCodeMapType.Month, now.Month,
                            $"月代号映射缺失: {now.Month}"),
                        MotorCodeSegmentType.Day => ResolveDateMap(dateMaps, MotorCodeMapType.Day, now.Day,
                            $"日代号映射缺失: {now.Day}"),
                        MotorCodeSegmentType.Sequence => FormatSequence(sequenceValue, config.DigitLength),
                        MotorCodeSegmentType.Fixed => ResolveFixedByBind(fixedSegments, bind),
                        _ => throw new InvalidOperationException($"未知片段类型: {bind.SegmentType}")
                    };

                    if (part.StartsWith("NG:", StringComparison.Ordinal))
                        return MotorCodeGenerateResult.Fail(part[3..].Trim());

                    parts.Add(part);
                }

                var code = string.Concat(parts);
                _logger.Info(
                    $"生成电机码 ProductTypeId={productTypeId}, Simulate={options.Simulate}, Seq={sequenceValue} → {code}",
                    LogSource);

                return MotorCodeGenerateResult.Ok(code, sequenceValue);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"生成电机码失败 ProductTypeId={productTypeId}", LogSource);
                return MotorCodeGenerateResult.Fail(ex.Message);
            }
        }

        #endregion

        #region ===================== 序列号管理 =====================

        /// <inheritdoc />
        public async Task<MotorCodeSequenceStatus?> GetSequenceStatusAsync(int productTypeId)//读取指定型号的序列号状态
        {
            if (productTypeId <= 0)
                return null;

            var snapshot = await GetConfigSnapshotAsync();//拿电机码配置快照
            var config = snapshot.GetSequenceConfig(productTypeId);//该型号的序列号配置
            if (config == null)
                return null;
            //获取对应型号的电机码序列号状态
            var state = await _sql.QuerySingleAsync<craft_MotorCodeSequenceState>(@"
            SELECT TOP 1 * FROM craft_MotorCodeSequenceState WHERE ProductTypeId = @ProductTypeId",
                new { ProductTypeId = productTypeId });

            if (state == null)//如果不存在
            {
                return new MotorCodeSequenceStatus
                {
                    ProductTypeId = productTypeId,
                    DigitLength = config.DigitLength,
                    StartValue = config.StartValue,
                    Step = config.Step,
                    ResetCycle = config.ResetCycle,
                    IsEnabled = config.IsEnabled,
                    BucketKey = MotorCodeBucketHelper.BuildBucketKey((MotorCodeResetCycle)config.ResetCycle, DateTime.Now),//根据复位周期组成今日桶键
                    CurrentValue = config.StartValue - config.Step,//是为了让「下一号」等于起始值
                    LastResetTime = null,
                    UpdateTime = null
                };
            }

            //判断桶键是否需要更新
            var bucketKey = MotorCodeBucketHelper.BuildBucketKey((MotorCodeResetCycle)config.ResetCycle, DateTime.Now);
            var current = state.CurrentValue;
            if(!string.Equals(state.BucketKey, bucketKey,StringComparison.OrdinalIgnoreCase))
                current = config.StartValue - config.Step;
            return new MotorCodeSequenceStatus
            {
                ProductTypeId = productTypeId,//型号
                DigitLength = config.DigitLength,//序列号长度
                StartValue = config.StartValue,//起始值
                Step = config.Step,//自增系数
                ResetCycle = config.ResetCycle,//复位周期
                IsEnabled = config.IsEnabled,//是否启用
                BucketKey = bucketKey,//当前桶键
                CurrentValue = current,//当前序列号
                LastResetTime = state.LastResetTime,//上次复位时间
                UpdateTime = state.UpdateTime,//最后更新时间
                
            };
        }

        /// <inheritdoc />
        public async Task SaveSequenceConfigAsync(craft_MotorCodeSequenceConfig config)
        {
            if (config.ProductTypeId <= 0)
                throw new ArgumentException("ProductTypeId 无效");

            config.UpdateTime = DateTime.Now;

            var existing = await _sql.QuerySingleAsync<craft_MotorCodeSequenceConfig>(@"
            SELECT TOP 1 * FROM craft_MotorCodeSequenceConfig WHERE ProductTypeId = @ProductTypeId",
                new { config.ProductTypeId });

            if (existing == null)
            {
                config.Id = await _sql.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(Id), 0) + 1 FROM craft_MotorCodeSequenceConfig");

                const string insertSql = @"
                INSERT INTO craft_MotorCodeSequenceConfig
                    (Id, ProductTypeId, DigitLength, StartValue, Step, ResetCycle, IsEnabled, UpdateTime, Remarks)
                VALUES
                    (@Id, @ProductTypeId, @DigitLength, @StartValue, @Step, @ResetCycle, @IsEnabled, @UpdateTime, @Remarks)";

                await _sql.ExecuteAsync(insertSql, config);
            }
            else
            {
                config.Id = existing.Id;
                const string updateSql = @"
                UPDATE craft_MotorCodeSequenceConfig
                SET DigitLength = @DigitLength, StartValue = @StartValue, Step = @Step,
                    ResetCycle = @ResetCycle, IsEnabled = @IsEnabled, UpdateTime = @UpdateTime, Remarks = @Remarks
                WHERE ProductTypeId = @ProductTypeId";

                var rows = await _sql.ExecuteAsync(updateSql, config);
                if (rows == 0)
                    throw new InvalidOperationException("序列号配置保存失败");
            }

            await EnsureSequenceStateAsync(config.ProductTypeId);
            await _motorCodeCache.RefreshAsync();
        }

        /// <inheritdoc />
        public async Task AdjustSequenceAsync(int productTypeId, int newValue, string operatorName, string reason)
        {
            if (productTypeId <= 0)
                throw new ArgumentException("ProductTypeId 无效");

            await EnsureSequenceStateAsync(productTypeId);

            var state = await _sql.QuerySingleAsync<craft_MotorCodeSequenceState>(@"
            SELECT TOP 1 * FROM craft_MotorCodeSequenceState WHERE ProductTypeId = @ProductTypeId",
                new { ProductTypeId = productTypeId })
                ?? throw new InvalidOperationException("序列号状态不存在");

            var oldValue = state.CurrentValue;
            var now = DateTime.Now;

            await _sql.ExecuteInTransactionAsync(async (conn, tran) =>
            {
                const string updateSql = @"
            UPDATE craft_MotorCodeSequenceState
            SET CurrentValue = @NewValue, UpdateTime = @Now
            WHERE ProductTypeId = @ProductTypeId";

                await conn.ExecuteAsync(updateSql, new
                {
                    NewValue = newValue,
                    Now = now,
                    ProductTypeId = productTypeId
                }, tran);

                const string auditSql = @"
                INSERT INTO craft_MotorCodeSequenceAudit
                (ProductTypeId, OldValue, NewValue, OldBucketKey, NewBucketKey, Operator, Reason, OperateTime)
                VALUES (@ProductTypeId, @OldValue, @NewValue, @OldBucketKey, @NewBucketKey, @Operator, @Reason, @Now)";

                await conn.ExecuteAsync(auditSql, new
                {
                    ProductTypeId = productTypeId,
                    OldValue = oldValue,
                    NewValue = newValue,
                    OldBucketKey = state.BucketKey,
                    NewBucketKey = state.BucketKey,
                    Operator = operatorName ?? string.Empty,
                    Reason = reason ?? string.Empty,
                    Now = now
                }, tran);
            });
        }

        /// <inheritdoc />
        public async Task ResetSequenceToStartAsync(int productTypeId, string operatorName, string reason)
        {
            if (productTypeId <= 0)
                throw new ArgumentException("ProductTypeId 无效");

            var config = await _sql.QuerySingleAsync<craft_MotorCodeSequenceConfig>(@"
            SELECT TOP 1 * FROM craft_MotorCodeSequenceConfig WHERE ProductTypeId = @ProductTypeId",
                new { ProductTypeId = productTypeId })
                ?? throw new InvalidOperationException("序列号配置不存在");

            await EnsureSequenceStateAsync(productTypeId);

            var now = DateTime.Now;
            var bucket = MotorCodeBucketHelper.BuildBucketKey((MotorCodeResetCycle)config.ResetCycle, now);
            var resetValue = config.StartValue - config.Step;

            await AdjustSequenceAsync(productTypeId, resetValue, operatorName, reason);

            const string sql = @"
            UPDATE craft_MotorCodeSequenceState
            SET BucketKey = @BucketKey, LastResetTime = @Now, UpdateTime = @Now
            WHERE ProductTypeId = @ProductTypeId";

            await _sql.ExecuteAsync(sql, new { BucketKey = bucket, Now = now, ProductTypeId = productTypeId });
        }

        #endregion

        #region ===================== 内部方法 =====================

        /// <summary> 序列号分配结果 </summary>
        private sealed class SequenceAllocResult
        {
            public bool Success { get; init; }
            public int SequenceValue { get; init; }
            public string ErrorMessage { get; init; } = string.Empty;
        }

        /// <summary> 确保 craft_MotorCodeSequenceState 存在（首次生成前初始化） </summary>
        private async Task EnsureSequenceStateAsync(int productTypeId)
        {
            var existing = await _sql.QuerySingleAsync<craft_MotorCodeSequenceState>(@"
SELECT TOP 1 Id FROM craft_MotorCodeSequenceState WHERE ProductTypeId = @ProductTypeId",
                new { ProductTypeId = productTypeId });

            if (existing != null)
                return;

            var snapshot = _motorCodeCache.GetSnapshot();
            var config = snapshot.GetSequenceConfig(productTypeId);
            var now = DateTime.Now;
            var bucket = config == null
                ? "GLOBAL"
                : MotorCodeBucketHelper.BuildBucketKey((MotorCodeResetCycle)config.ResetCycle, now);
            var initialValue = config == null ? 0 : config.StartValue - config.Step;

            var nextId = await _sql.QuerySingleAsync<int>(
                "SELECT ISNULL(MAX(Id), 0) + 1 FROM craft_MotorCodeSequenceState");

            const string insertSql = @"
INSERT INTO craft_MotorCodeSequenceState (Id, ProductTypeId, BucketKey, CurrentValue, LastResetTime, UpdateTime)
VALUES (@Id, @ProductTypeId, @BucketKey, @CurrentValue, @Now, @Now)";

            await _sql.ExecuteAsync(insertSql, new
            {
                Id = nextId,
                ProductTypeId = productTypeId,
                BucketKey = bucket,
                CurrentValue = initialValue,
                Now = now
            });
        }

        /// <summary>事务内 UPDLOCK 按型号安全取号</summary>
        private async Task<SequenceAllocResult> AllocateSequenceAsync(int productTypeId, DateTime now)
        {
            int issuedValue = 0;
            string? error = null;

            await _sql.ExecuteInTransactionAsync(async (conn, tran) =>
            {
                var snapshot = _motorCodeCache.GetSnapshot();
                var config = snapshot.GetSequenceConfig(productTypeId);

                if (config == null || !config.IsEnabled)
                {
                    error = $"型号 Id={productTypeId} 序列号配置未启用或不存在";
                    return;
                }
                //锁表获取
                const string stateSql = @"
                SELECT TOP 1 * FROM craft_MotorCodeSequenceState WITH (UPDLOCK, HOLDLOCK)
                WHERE ProductTypeId = @ProductTypeId";

                var state = await conn.QueryFirstOrDefaultAsync<craft_MotorCodeSequenceState>(
                    stateSql,
                    new { ProductTypeId = productTypeId },
                    transaction: tran);

                if (state == null)
                {
                    var bucket = MotorCodeBucketHelper.BuildBucketKey((MotorCodeResetCycle)config.ResetCycle, now);
                    var initial = config.StartValue - config.Step;
                    var nextId = await conn.QuerySingleAsync<int>(
                        "SELECT ISNULL(MAX(Id), 0) + 1 FROM craft_MotorCodeSequenceState WITH (UPDLOCK, HOLDLOCK)",
                        transaction: tran);

                    const string insertSql = @"
                    INSERT INTO craft_MotorCodeSequenceState (Id, ProductTypeId, BucketKey, CurrentValue, LastResetTime, UpdateTime)
                    VALUES (@Id, @ProductTypeId, @BucketKey, @CurrentValue, @Now, @Now)";

                    await conn.ExecuteAsync(insertSql, new
                    {
                        Id = nextId,
                        ProductTypeId = productTypeId,
                        BucketKey = bucket,
                        CurrentValue = initial,
                        Now = now
                    }, tran);

                    state = new craft_MotorCodeSequenceState
                    {
                        ProductTypeId = productTypeId,
                        BucketKey = bucket,
                        CurrentValue = initial
                    };
                }

                var bucketKey = MotorCodeBucketHelper.BuildBucketKey((MotorCodeResetCycle)config.ResetCycle, now);
                var current = state.CurrentValue;

                if (!string.Equals(state.BucketKey, bucketKey, StringComparison.OrdinalIgnoreCase))
                    current = config.StartValue - config.Step;

                var next = current + config.Step;
                var max = (int)Math.Pow(10, config.DigitLength) - 1;
                if (next > max)
                {
                    error = $"序列号已超出位数上限（最大 {max}）";
                    return;
                }

                const string updateSql = @"
                UPDATE craft_MotorCodeSequenceState
                SET CurrentValue = @CurrentValue, BucketKey = @BucketKey,
                    LastResetTime = CASE WHEN BucketKey <> @BucketKey THEN @Now ELSE LastResetTime END,
                    UpdateTime = @Now
                WHERE ProductTypeId = @ProductTypeId";

                await conn.ExecuteAsync(updateSql, new
                {
                    CurrentValue = next,
                    BucketKey = bucketKey,
                    Now = now,
                    ProductTypeId = productTypeId
                }, tran);

                issuedValue = next;
            });

            if (!string.IsNullOrEmpty(error))
                return new SequenceAllocResult { Success = false, ErrorMessage = error ?? "" };

            return new SequenceAllocResult { Success = true, SequenceValue = issuedValue };
        }

        /// <summary> 解析规则公式，按 + 拆分为片段代号列表 </summary>
        private static List<string> ParseFormula(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return [];

            return formula.Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();
        }

        /// <summary> 解析年月日映射片段，缺失时返回 NG: 前缀错误 </summary>
        private static string ResolveDateMap(
            List<craft_MotorCodeDateMap> maps,
            MotorCodeMapType mapType,
            int key,
            string missingMessage)
        {
            var item = maps.FirstOrDefault(m => m.MapType == (int)mapType && m.MapKey == key);
            if (item == null || string.IsNullOrWhiteSpace(item.MapCode))
                return "NG:" + missingMessage; // 上层识别为失败

            return item.MapCode;
        }

        /// <summary> 解析固定信息片段 </summary>
        private static string ResolveFixedByBind(
            List<craft_MotorCodeFixedSegment> segments,
            craft_MotorCodeSegmentBind bind)
        {
            if (bind.FixedSegmentId <= 0)
                return "NG:未选择固定信息";

            var seg = segments.FirstOrDefault(s => s.Id == bind.FixedSegmentId);
            if (seg == null || string.IsNullOrWhiteSpace(seg.FixedValue))
                return $"NG:固定信息未配置(Id={bind.FixedSegmentId})";

            return seg.FixedValue;
        }

        /// <summary> 格式化序列号为固定位数，超出范围返回 NG: 错误 </summary>
        private static string FormatSequence(int value, int digitLength)
        {
            if (digitLength <= 0)
                digitLength = 4; // 默认 4 位

            var max = (int)Math.Pow(10, digitLength) - 1; // 如 4 位最大 9999
            if (value < 0 || value > max)
                return $"NG:序列号 {value} 超出 {digitLength} 位范围";

            return value.ToString($"D{digitLength}");
        }

        #endregion
    }
}
