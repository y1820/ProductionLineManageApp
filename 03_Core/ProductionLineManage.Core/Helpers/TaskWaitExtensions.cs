using System.Runtime.CompilerServices;

namespace ProductionLineManage.Core.Helpers
{
    /// <summary>
    /// net48 没有 Task.WaitAsync，用 WhenAny 提供同样超时/取消语义。
    /// </summary>
    public static class TaskWaitExtensions
    {
        public static async Task WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (!cancellationToken.CanBeCanceled)
            {
                await task.ConfigureAwait(false);
                return;
            }

            var cancelTask = CreateCancelTask(cancellationToken);
            var completed = await Task.WhenAny(task, cancelTask).ConfigureAwait(false);
            if (completed != task)
                cancellationToken.ThrowIfCancellationRequested();
            await task.ConfigureAwait(false);
        }

        public static Task WaitAsync(this Task task, TimeSpan timeout) =>
            WaitAsync(task, timeout, CancellationToken.None);

        public static async Task WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            try
            {
                await WaitAsync(task, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }

        public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout)
        {
            await WaitAsync((Task)task, timeout, CancellationToken.None).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            await WaitAsync((Task)task, timeout, cancellationToken).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        private static Task CreateCancelTask(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(static s => ((TaskCompletionSource<bool>)s!).TrySetCanceled(), tcs);
            return tcs.Task;
        }
    }
}
