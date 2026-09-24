using System.Threading;

namespace ProductionLineManage.Host
{
    /// <summary>
    /// 单实例：已有进程在跑时，唤醒已有窗口并退出本次启动。
    /// </summary>
    public sealed class SingleInstanceGuard : IDisposable
    {
        private const string MutexName = @"Local\ProductionLineManage.Host.SingleInstance";
        private const string ActivateEventName = @"Local\ProductionLineManage.Host.Activate";

        private Mutex? _mutex;
        private EventWaitHandle? _activateEvent;
        private CancellationTokenSource? _listenCts;
        private bool _ownsMutex;

        /// <summary>
        /// 尝试成为主实例。失败时会通知已有进程把窗口提到前台。
        /// </summary>
        public bool TryAcquire(Action onActivateRequested)
        {
            try
            {
                _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
                _ownsMutex = createdNew;
            }
            catch (AbandonedMutexException ex)
            {
                // 上次进程异常退出，互斥量被遗弃，当前进程接管
                _mutex = ex.Mutex ?? _mutex;
                _ownsMutex = true;
            }

            if (!_ownsMutex)
            {
                SignalExistingInstance();
                return false;
            }

            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _listenCts = new CancellationTokenSource();
            var token = _listenCts.Token;
            _ = Task.Run(() => ListenForActivation(onActivateRequested, token));
            return true;
        }

        public void Dispose()
        {
            try { _listenCts?.Cancel(); } catch { }

            try { _activateEvent?.Set(); } catch { }
            try { _activateEvent?.Dispose(); } catch { }
            _activateEvent = null;

            if (_ownsMutex)
            {
                try { _mutex?.ReleaseMutex(); } catch { }
            }
            try { _mutex?.Dispose(); } catch { }
            _mutex = null;
            _ownsMutex = false;

            try { _listenCts?.Dispose(); } catch { }
            _listenCts = null;
        }

        private static void SignalExistingInstance()
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    using var ev = EventWaitHandle.OpenExisting(ActivateEventName);
                    ev.Set();
                    return;
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    Thread.Sleep(50);
                }
            }
        }

        private void ListenForActivation(Action onActivateRequested, CancellationToken token)
        {
            var ev = _activateEvent;
            if (ev == null)
                return;

            while (!token.IsCancellationRequested)
            {
                var signaled = WaitHandle.WaitAny(new WaitHandle[] { ev, token.WaitHandle });
                if (token.IsCancellationRequested || signaled != 0)
                    return;

                try { onActivateRequested(); }
                catch { }
            }
        }
    }
}
