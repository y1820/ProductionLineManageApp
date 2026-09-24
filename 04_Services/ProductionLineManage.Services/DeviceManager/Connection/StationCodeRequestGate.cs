using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 流水码/物料码请求门控：在业务消费者之前配对「码」与「请求/信号」。
    /// PLC 先发 200/500 或扫码完成信号、码尚未就绪时暂存请求，码到达后再放行。
    /// </summary>
    internal sealed class StationCodeRequestGate : IDisposable
    {
        #region ===================== 常量 =====================

        /// <summary> 默认最长等待 30 秒（约为 500ms 扫描周期 × 60） </summary>
        public const int DefaultPendingTimeoutMs = 30000;

        /// <summary> 暂存期间主动读 PLC 的轮询间隔 </summary>
        public const int DefaultPollIntervalMs = 50;

        /// <summary> 主动读 PLC 单次超时 </summary>
        private const int PlcReadTimeoutMs = 3000;

        #endregion

        #region ===================== 字段 =====================

        /// <summary> 工位 Id </summary>
        private readonly int _stationId;

        /// <summary> 交互类型（信号型才暂存扫码完成信号） </summary>
        private readonly string _interactionType;

        /// <summary> Task 上下文（主动读码时使用） </summary>
        private readonly IDeviceTaskContext _context;

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        /// <summary> 放行后转发回调 </summary>
        private readonly Func<DeviceDataMessage, CancellationToken, ValueTask> _forwardAsync;

        /// <summary> 暂存请求最长等待毫秒 </summary>
        private readonly int _pendingTimeoutMs;

        /// <summary> 主动读码轮询间隔 </summary>
        private readonly int _pollIntervalMs;

        /// <summary> 保护快照与暂存状态 </summary>
        private readonly object _lock = new();

        /// <summary> 当前流水码快照 </summary>
        private string _flowCode = string.Empty;

        /// <summary> 当前物料码快照 </summary>
        private string _materialCode = string.Empty;

        /// <summary> 当前物料类型快照 </summary>
        private string _materialType = string.Empty;

        /// <summary> 暂存的流水码触发消息 </summary>
        private DeviceDataMessage? _pendingFlowTrigger;

        /// <summary> 暂存的物料码触发消息 </summary>
        private DeviceDataMessage? _pendingMaterialTrigger;

        /// <summary> 流水码等待取消令牌 </summary>
        private CancellationTokenSource? _flowWaitCts;

        /// <summary> 物料码等待取消令牌 </summary>
        private CancellationTokenSource? _materialWaitCts;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建码请求门控 </summary>
        public StationCodeRequestGate(
            int stationId,
            string interactionType,
            IDeviceTaskContext context,
            ILogger logger,
            Func<DeviceDataMessage, CancellationToken, ValueTask> forwardAsync,
            int scanIntervalMs,
            int? pendingTimeoutMs = null,
            int? pollIntervalMs = null)
        {
            _stationId = stationId;
            _interactionType = interactionType;
            _context = context;
            _logger = logger;
            _forwardAsync = forwardAsync;
            _pendingTimeoutMs = pendingTimeoutMs
                ?? Math.Max(DefaultPendingTimeoutMs, scanIntervalMs * 60);
            _pollIntervalMs = pollIntervalMs
                ?? Math.Min(DefaultPollIntervalMs, Math.Max(20, scanIntervalMs / 5));
        }

        #endregion

        #region ===================== 对外入口 =====================

        /// <summary> 异步接收消息：规划放行列表后逐条转发 </summary>
        public async ValueTask AcceptAsync(DeviceDataMessage message, CancellationToken token = default)
        {
            var toForward = new List<DeviceDataMessage>();
            lock (_lock)
            {
                PlanAccept(message, toForward);
            }

            foreach (var item in toForward)
                await _forwardAsync(item, token);
        }

        /// <summary> 同步接收消息：由调用方决定 TryWrite 是否成功 </summary>
        public bool TryAccept(DeviceDataMessage message, Func<DeviceDataMessage, bool> tryForward)
        {
            var toForward = new List<DeviceDataMessage>();
            lock (_lock)
            {
                PlanAccept(message, toForward);
            }

            foreach (var item in toForward)
            {
                if (!tryForward(item))
                    return false;
            }

            return true;
        }

        /// <summary> 断线后清空码快照与暂存请求 </summary>
        public void ResetSnapshot()
        {
            lock (_lock)
            {
                ClearCodeSnapshotLocked();
                ClearPendingLocked("连接重置");
            }
        }

        /// <summary> 停止时取消等待并清空暂存 </summary>
        public void Complete()
        {
            lock (_lock)
            {
                CancelFlowWaitLocked();
                CancelMaterialWaitLocked();
                _pendingFlowTrigger = null;
                _pendingMaterialTrigger = null;
            }
        }

        /// <summary> 释放等待资源 </summary>
        public void Dispose() => Complete();

        #endregion

        #region ===================== 消息规划 =====================

        /// <summary> 按 DataType 分发到对应规划逻辑 </summary>
        private void PlanAccept(DeviceDataMessage message, List<DeviceDataMessage> toForward)
        {
            switch (message.DataType)
            {
                case DataNameConstants.CurrentFlowCode:
                    UpdateFlowCode(message.Data?.ToString());
                    toForward.Add(message);
                    TryReleaseFlowTriggerLocked(toForward);
                    return;

                case DataNameConstants.MaterialCode:
                    UpdateMaterialCode(message.Data?.ToString());
                    toForward.Add(message);
                    TryReleaseMaterialTriggerLocked(toForward);
                    return;

                case DataNameConstants.MaterialType:
                    UpdateMaterialType(message.Data?.ToString());
                    toForward.Add(message);
                    TryReleaseMaterialTriggerLocked(toForward);
                    return;

                case DataNameConstants.RequestCode:
                    PlanRequestCode(message, toForward);
                    return;

                case DataNameConstants.FlowCodeDone:
                    PlanFlowCodeDone(message, toForward);
                    return;

                case DataNameConstants.MaterialCodeDone:
                    PlanMaterialCodeDone(message, toForward);
                    return;

                default:
                    toForward.Add(message); // 非码相关消息直接放行
                    return;
            }
        }

        /// <summary> 处理 RequestCode（100/200/500 等指令） </summary>
        private void PlanRequestCode(DeviceDataMessage message, List<DeviceDataMessage> toForward)
        {
            if (!TryParseRequestCode(message.Data, out var requestCode))
            {
                toForward.Add(message);
                return;
            }

            if (requestCode == 0)
            {
                ClearPendingLocked("请求指令归零");
                toForward.Add(message);
                return;
            }

            if (requestCode == (int)PLCRequestCode.Handshake)
            {
                ClearCodeSnapshotLocked();
                ClearPendingLocked("收到握手 100");
                toForward.Add(message);
                return;
            }

            // 指令型 200/500 不在门控层暂存，立即交给消费者
            if (requestCode == (int)PLCRequestCode.FlowCodeVerify)
                EnsureFlowCodeMessageLocked(toForward);

            if (requestCode == (int)PLCRequestCode.MaterialVerify)
                EnsureMaterialMessagesLocked(toForward);

            toForward.Add(message);
        }

        /// <summary> 处理流水码扫码完成信号（信号型交互） </summary>
        private void PlanFlowCodeDone(DeviceDataMessage message, List<DeviceDataMessage> toForward)
        {
            if (!IsSignalInteraction() || !ConvertToBool(message.Data))
            {
                toForward.Add(message);
                return;
            }

            if (HasFlowCode)
            {
                EnsureFlowCodeMessageLocked(toForward);
                toForward.Add(message);
                return;
            }

            HoldFlowTrigger(message, "流水码扫码完成信号");
        }

        /// <summary> 处理物料码扫码完成信号（信号型交互） </summary>
        private void PlanMaterialCodeDone(DeviceDataMessage message, List<DeviceDataMessage> toForward)
        {
            if (!IsSignalInteraction() || !ConvertToBool(message.Data))
            {
                toForward.Add(message);
                return;
            }

            if (HasMaterial)
            {
                EnsureMaterialMessagesLocked(toForward);
                toForward.Add(message);
                return;
            }

            HoldMaterialTrigger(message, "物料码扫码完成信号");
        }

        #endregion

        #region ===================== 暂存与放行 =====================

        /// <summary> 暂存流水码触发并启动后台等待 </summary>
        private void HoldFlowTrigger(DeviceDataMessage message, string reason)
        {
            ReplaceFlowTrigger(message);
            _logger.DeviceLog(_stationId, "码请求门控",
                $"{reason} 暂存等待流水码（超时 {_pendingTimeoutMs}ms，轮询 {_pollIntervalMs}ms）",
                LogLevel.Info);
            RestartFlowWaitLocked();
        }

        /// <summary> 暂存物料码触发并启动后台等待 </summary>
        private void HoldMaterialTrigger(DeviceDataMessage message, string reason)
        {
            ReplaceMaterialTrigger(message);
            _logger.DeviceLog(_stationId, "码请求门控",
                $"{reason} 暂存等待物料码/类型（超时 {_pendingTimeoutMs}ms，轮询 {_pollIntervalMs}ms）",
                LogLevel.Info);
            RestartMaterialWaitLocked();
        }

        /// <summary> 流水码就绪时尝试放行暂存触发 </summary>
        private void TryReleaseFlowTriggerLocked(List<DeviceDataMessage> toForward)
        {
            if (_pendingFlowTrigger == null || !HasFlowCode)
                return;

            AppendFlowReleaseLocked(toForward);
        }

        /// <summary> 物料码就绪时尝试放行暂存触发 </summary>
        private void TryReleaseMaterialTriggerLocked(List<DeviceDataMessage> toForward)
        {
            if (_pendingMaterialTrigger == null || !HasMaterial)
                return;

            AppendMaterialReleaseLocked(toForward);
        }

        /// <summary> 放行流水码暂存：先补码消息再转发触发 </summary>
        private void AppendFlowReleaseLocked(List<DeviceDataMessage> toForward)
        {
            var pending = _pendingFlowTrigger!;
            _pendingFlowTrigger = null;
            CancelFlowWaitLocked();
            _logger.DeviceLog(_stationId, "码请求门控",
                $"流水码就绪={_flowCode}，放行暂存请求 [{pending.DataType}]", LogLevel.Info);
            EnsureFlowCodeMessageLocked(toForward);
            toForward.Add(pending);
        }

        /// <summary> 放行物料暂存：先补码/类型消息再转发触发 </summary>
        private void AppendMaterialReleaseLocked(List<DeviceDataMessage> toForward)
        {
            var pending = _pendingMaterialTrigger!;
            _pendingMaterialTrigger = null;
            CancelMaterialWaitLocked();
            _logger.DeviceLog(_stationId, "码请求门控",
                $"物料就绪，码={_materialCode}，类型={_materialType}，放行暂存请求 [{pending.DataType}]",
                LogLevel.Info);
            EnsureMaterialMessagesLocked(toForward);
            toForward.Add(pending);
        }

        /// <summary> 放行前确保消费者先收到流水码消息 </summary>
        private void EnsureFlowCodeMessageLocked(List<DeviceDataMessage> toForward)
        {
            if (!HasFlowCode)
                return;

            if (toForward.Any(m =>
                    m.DataType == DataNameConstants.CurrentFlowCode &&
                    string.Equals(NormalizeCode(m.Data?.ToString()), _flowCode, StringComparison.Ordinal)))
                return;

            toForward.Add(CreateCodeMessage(DataNameConstants.CurrentFlowCode, _flowCode));
        }

        /// <summary> 放行前确保消费者先收到物料码与类型消息 </summary>
        private void EnsureMaterialMessagesLocked(List<DeviceDataMessage> toForward)
        {
            if (!string.IsNullOrEmpty(_materialCode) &&
                !toForward.Any(m => m.DataType == DataNameConstants.MaterialCode))
            {
                toForward.Add(CreateCodeMessage(DataNameConstants.MaterialCode, _materialCode));
            }

            if (!string.IsNullOrEmpty(_materialType) &&
                !toForward.Any(m => m.DataType == DataNameConstants.MaterialType))
            {
                toForward.Add(CreateCodeMessage(DataNameConstants.MaterialType, _materialType));
            }
        }

        /// <summary> 构造码消息 </summary>
        private DeviceDataMessage CreateCodeMessage(string dataType, string value) =>
            new()
            {
                StationId = _stationId,
                DataType = dataType,
                Data = value
            };

        #endregion

        #region ===================== 快照与暂存清理 =====================

        /// <summary> 替换流水码暂存触发 </summary>
        private void ReplaceFlowTrigger(DeviceDataMessage message)
        {
            CancelFlowWaitLocked();
            _pendingFlowTrigger = message;
        }

        /// <summary> 替换物料码暂存触发 </summary>
        private void ReplaceMaterialTrigger(DeviceDataMessage message)
        {
            CancelMaterialWaitLocked();
            _pendingMaterialTrigger = message;
        }

        /// <summary> 清空码快照 </summary>
        private void ClearCodeSnapshotLocked()
        {
            _flowCode = string.Empty;
            _materialCode = string.Empty;
            _materialType = string.Empty;
        }

        /// <summary> 清除所有暂存请求 </summary>
        private void ClearPendingLocked(string reason)
        {
            if (_pendingFlowTrigger != null || _pendingMaterialTrigger != null)
            {
                _logger.DeviceLog(_stationId, "码请求门控",
                    $"{reason}，清除暂存流水码/物料请求", LogLevel.Info);
            }

            CancelFlowWaitLocked();
            CancelMaterialWaitLocked();
            _pendingFlowTrigger = null;
            _pendingMaterialTrigger = null;
        }

        #endregion

        #region ===================== 后台等待 =====================

        /// <summary> 重启流水码后台等待 Task </summary>
        private void RestartFlowWaitLocked()
        {
            CancelFlowWaitLocked();
            _flowWaitCts = new CancellationTokenSource();
            var token = _flowWaitCts.Token;
            _ = Task.Run(() => RunFlowPendingWaitAsync(token), CancellationToken.None);
        }

        /// <summary> 重启物料码后台等待 Task </summary>
        private void RestartMaterialWaitLocked()
        {
            CancelMaterialWaitLocked();
            _materialWaitCts = new CancellationTokenSource();
            var token = _materialWaitCts.Token;
            _ = Task.Run(() => RunMaterialPendingWaitAsync(token), CancellationToken.None);
        }

        /// <summary> 流水码等待循环：轮询读 PLC 直至就绪或超时 </summary>
        private async Task RunFlowPendingWaitAsync(CancellationToken token)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(_pendingTimeoutMs);
            while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (await TryRefreshFlowCodeFromPlcAsync(token))
                    return;

                try
                {
                    await Task.Delay(_pollIntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (!token.IsCancellationRequested)
                await ReleaseFlowTriggerOnTimeoutAsync();
        }

        /// <summary> 物料码等待循环：轮询读 PLC 直至就绪或超时 </summary>
        private async Task RunMaterialPendingWaitAsync(CancellationToken token)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(_pendingTimeoutMs);
            while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (await TryRefreshMaterialFromPlcAsync(token))
                    return;

                try
                {
                    await Task.Delay(_pollIntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (!token.IsCancellationRequested)
                await ReleaseMaterialTriggerOnTimeoutAsync();
        }

        /// <summary> 取消流水码等待 </summary>
        private void CancelFlowWaitLocked()
        {
            _flowWaitCts?.Cancel();
            _flowWaitCts?.Dispose();
            _flowWaitCts = null;
        }

        /// <summary> 取消物料码等待 </summary>
        private void CancelMaterialWaitLocked()
        {
            _materialWaitCts?.Cancel();
            _materialWaitCts?.Dispose();
            _materialWaitCts = null;
        }

        #endregion

        #region ===================== 主动读 PLC =====================

        /// <summary> 主动读流水码并尝试放行暂存 </summary>
        private async Task<bool> TryRefreshFlowCodeFromPlcAsync(CancellationToken token)
        {
            var code = await ReadFlowCodeFromPlcWithTimeoutAsync(token);
            if (string.IsNullOrEmpty(code))
                return false;

            List<DeviceDataMessage> toForward;
            lock (_lock)
            {
                if (_pendingFlowTrigger == null || token.IsCancellationRequested)
                    return false;

                UpdateFlowCode(code);
                if (!HasFlowCode)
                    return false;

                toForward = new List<DeviceDataMessage>();
                AppendFlowReleaseLocked(toForward);
            }

            foreach (var item in toForward)
                await _forwardAsync(item, token);

            return true;
        }

        /// <summary> 主动读物料码/类型并尝试放行暂存 </summary>
        private async Task<bool> TryRefreshMaterialFromPlcAsync(CancellationToken token)
        {
            var (code, type) = await ReadMaterialFromPlcWithTimeoutAsync(token);
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(type))
                return false;

            List<DeviceDataMessage> toForward;
            lock (_lock)
            {
                if (_pendingMaterialTrigger == null || token.IsCancellationRequested)
                    return false;

                UpdateMaterialCode(code);
                UpdateMaterialType(type);
                if (!HasMaterial)
                    return false;

                toForward = new List<DeviceDataMessage>();
                AppendMaterialReleaseLocked(toForward);
            }

            foreach (var item in toForward)
                await _forwardAsync(item, token);

            return true;
        }

        /// <summary> 流水码等待超时仍放行（由业务层判定） </summary>
        private async Task ReleaseFlowTriggerOnTimeoutAsync()
        {
            DeviceDataMessage? pending;
            lock (_lock)
            {
                pending = _pendingFlowTrigger;
                if (pending == null)
                    return;

                _pendingFlowTrigger = null;
                CancelFlowWaitLocked();
            }

            _logger.DeviceLog(_stationId, "码请求门控",
                $"等待流水码超时，仍放行暂存请求 [{pending.DataType}]，由业务层判定", LogLevel.Warning);
            await _forwardAsync(pending, CancellationToken.None);
        }

        /// <summary> 物料码等待超时仍放行（由业务层判定） </summary>
        private async Task ReleaseMaterialTriggerOnTimeoutAsync()
        {
            DeviceDataMessage? pending;
            lock (_lock)
            {
                pending = _pendingMaterialTrigger;
                if (pending == null)
                    return;

                _pendingMaterialTrigger = null;
                CancelMaterialWaitLocked();
            }

            _logger.DeviceLog(_stationId, "码请求门控",
                $"等待物料码/类型超时，仍放行暂存请求 [{pending.DataType}]，由业务层判定", LogLevel.Warning);
            await _forwardAsync(pending, CancellationToken.None);
        }

        /// <summary> 带超时的流水码读取 </summary>
        private async Task<string> ReadFlowCodeFromPlcWithTimeoutAsync(CancellationToken token)
        {
            var readTask = ReadFlowCodeFromPlcCoreAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(PlcReadTimeoutMs, token));
            if (completed != readTask)
                return string.Empty;

            return await readTask;
        }

        /// <summary> 带超时的物料码/类型读取 </summary>
        private async Task<(string MaterialCode, string MaterialType)> ReadMaterialFromPlcWithTimeoutAsync(
            CancellationToken token)
        {
            var readTask = ReadMaterialFromPlcCoreAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(PlcReadTimeoutMs, token));
            if (completed != readTask)
                return (string.Empty, string.Empty);

            return await readTask;
        }

        /// <summary> 从映射地址读取流水码 </summary>
        private async Task<string> ReadFlowCodeFromPlcCoreAsync()
        {
            if (_context.HasMapping(DataNameConstants.CurrentFlowCode))
            {
                var raw = await _context.ReadAsync(DataNameConstants.CurrentFlowCode);
                var code = NormalizeCode(raw?.ToString());
                if (!string.IsNullOrEmpty(code))
                    return code;
            }

            if (_context.HasMapping(DataNameConstants.DataPayload))
            {
                var raw = await _context.ReadAsync(DataNameConstants.DataPayload);
                return NormalizeCode(raw?.ToString());
            }

            return string.Empty;
        }

        /// <summary> 从映射地址读取物料码与类型 </summary>
        private async Task<(string MaterialCode, string MaterialType)> ReadMaterialFromPlcCoreAsync()
        {
            string code = string.Empty;
            string type = string.Empty;

            if (_context.HasMapping(DataNameConstants.MaterialCode))
            {
                var raw = await _context.ReadAsync(DataNameConstants.MaterialCode);
                code = NormalizeCode(raw?.ToString());
            }

            if (_context.HasMapping(DataNameConstants.MaterialType))
            {
                var raw = await _context.ReadAsync(DataNameConstants.MaterialType);
                type = NormalizeCode(raw?.ToString());
            }

            return (code, type);
        }

        #endregion

        #region ===================== 快照更新与工具 =====================

        /// <summary> 更新流水码快照 </summary>
        private void UpdateFlowCode(string? value) =>
            _flowCode = NormalizeCode(value);

        /// <summary> 更新物料码快照 </summary>
        private void UpdateMaterialCode(string? value) =>
            _materialCode = NormalizeCode(value);

        /// <summary> 更新物料类型快照 </summary>
        private void UpdateMaterialType(string? value) =>
            _materialType = NormalizeCode(value);

        /// <summary> 是否已有有效流水码 </summary>
        private bool HasFlowCode => !string.IsNullOrEmpty(_flowCode);

        /// <summary> 是否已有有效物料码与类型 </summary>
        private bool HasMaterial =>
            !string.IsNullOrEmpty(_materialCode) && !string.IsNullOrEmpty(_materialType);

        /// <summary> 是否为信号型交互 </summary>
        private bool IsSignalInteraction() =>
            string.Equals(_interactionType, InteractionTypeConstants.Signal, StringComparison.Ordinal);

        /// <summary> 规范化码字符串（空值 / "--" 视为空） </summary>
        private static string NormalizeCode(string? value) =>
            string.IsNullOrWhiteSpace(value) || value == "--" ? string.Empty : value.Trim();

        /// <summary> 解析 RequestCode 整型值 </summary>
        private static bool TryParseRequestCode(object? value, out int requestCode)
        {
            requestCode = 0;
            if (value == null)
                return false;

            return int.TryParse(value.ToString(), out requestCode);
        }

        /// <summary> 将 PLC 值转换为 bool </summary>
        private static bool ConvertToBool(object? value) =>
            value switch
            {
                bool b => b,
                int i => i != 0,
                short s => s != 0,
                byte by => by != 0,
                string str => bool.TryParse(str, out var parsed) ? parsed : str == "1",
                _ => false
            };

        #endregion
    }
}
