using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Router.Deduplication
{
    class AsyncManualResetEvent
    {
        volatile TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();

        public Task WaitAsync()
        {
            return completionSource.Task;
        }

        public void Set()
        {
            completionSource.TrySetResult(true);
        }

        public void Cancel()
        {
            completionSource.SetCanceled();
        }

        public void Reset()
        {
            while (true)
            {
                var tcs = completionSource;
                if (!tcs.Task.IsCompleted ||
                    Interlocked.CompareExchange(ref completionSource, new TaskCompletionSource<bool>(), tcs) == tcs)
                    return;
            }
        }
    }
}