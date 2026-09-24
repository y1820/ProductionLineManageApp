using System.Threading.Channels;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 工位级业务消息队列：扫描/订阅回调经码请求门控后入队，单消费者串行交给 Mediator。
    /// </summary>
    internal sealed class StationBusinessChannel : IDisposable
    {
        #region ===================== 常量 =====================

        /// <summary> 默认队列容量 </summary>
        private const int DefaultCapacity = 256;

        #endregion

        #region ===================== 字段 =====================

        /// <summary> 有界 Channel，SingleReader 保证串行消费 </summary>
        private readonly Channel<DeviceDataMessage> _channel;

        /// <summary> 业务中介，转发给 Interaction 逻辑 </summary>
        private readonly IDeviceBusinessMediator _mediator;

        /// <summary> 工位 Task 上下文（码门控主动读 PLC 时使用） </summary>
        private readonly IDeviceTaskContext _context;

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        /// <summary> 工位 Id </summary>
        private readonly int _stationId;

        /// <summary> 流水码/物料码请求门控 </summary>
        private readonly StationCodeRequestGate _codeGate;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建业务队列并初始化码请求门控 </summary>
        public StationBusinessChannel(
            IDeviceBusinessMediator mediator,
            IDeviceTaskContext context,
            ILogger logger,
            string interactionType,
            int scanIntervalMs,
            int capacity = DefaultCapacity)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
            _stationId = context.StationId;
            _channel = Channel.CreateBounded<DeviceDataMessage>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait, // 队列满时阻塞生产者
                SingleReader = true,
                SingleWriter = false
            });
            _codeGate = new StationCodeRequestGate(
                _stationId,
                interactionType,
                context,
                logger,
                ForwardToChannelAsync,
                scanIntervalMs);
        }

        #endregion

        #region ===================== 入队 =====================

        /// <summary> 异步入队：经码门控配对后写入 Channel </summary>
        public ValueTask EnqueueAsync(DeviceDataMessage message, CancellationToken token = default) =>
            _codeGate.AcceptAsync(message, token);

        /// <summary> 同步尝试入队（OPC 回调等无 CancellationToken 场景） </summary>
        public bool TryEnqueue(DeviceDataMessage message)
        {
            return _codeGate.TryAccept(message, msg =>
            {
                if (_channel.Writer.TryWrite(msg))
                    return true;

                _logger.DeviceLog(_stationId, "业务队列",
                    $"业务消息队列已满，丢弃 [{msg.DataType}]", LogLevel.Warning);
                return false;
            });
        }

        #endregion

        #region ===================== 生命周期 =====================

        /// <summary> 断线后重置码门控快照 </summary>
        public void ResetCodeGate() => _codeGate.ResetSnapshot();

        /// <summary> 停止时完成门控与 Channel 写入端 </summary>
        public void Complete()
        {
            _codeGate.Complete();
            _channel.Writer.TryComplete();
        }

        /// <summary> 释放码门控资源 </summary>
        public void Dispose()
        {
            Complete();
            _codeGate.Dispose();
        }

        #endregion

        #region ===================== 消费者 =====================

        /// <summary> 业务消费线程：逐条 Publish 给 Mediator </summary>
        public async Task RunConsumerAsync(CancellationToken token)
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var message))
                    {
                        try
                        {
                            await _mediator.PublishAsync(message, _context);
                        }
                        catch (Exception ex)
                        {
                            _logger.DeviceLog(_stationId, "业务队列",
                                $"处理业务消息失败 [{message.DataType}]: {ex.Message}", LogLevel.Error);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // StopAsync Cancel 时正常退出
            }
        }

        #endregion

        #region ===================== 内部转发 =====================

        /// <summary> 码门控放行后写入 Channel </summary>
        private ValueTask ForwardToChannelAsync(DeviceDataMessage message, CancellationToken token) =>
            _channel.Writer.WriteAsync(message, token);

        #endregion
    }
}
